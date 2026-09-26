using FourPlayWebApp.Server.Services;
using FourPlayWebApp.Shared.Models;
using static FourPlayWebApp.Server.UnitTests.EspnDayCacheTests;

namespace FourPlayWebApp.Server.UnitTests;

// When the NFL and CFB score pollers wake up next. They used to poll every 5 min whenever no game
// was live — all week, and all off-season. Now they poll fast only while a game is on, and
// otherwise sleep until the next thing that can change what they serve: a kickoff, or a schedule
// boundary where the current week/slate flips — capped so a schedule change still gets noticed.
public class ScorePollScheduleTests {
    private static readonly DateTimeOffset Now = new(2026, 9, 27, 18, 0, 0, TimeSpan.Zero);

    private static ScorePollSchedule Polled(params DateTimeOffset[] scheduleWakePoints) {
        var schedule = new ScorePollSchedule();
        schedule.PollAsync(() => Task.FromResult(new ScorePollSchedule.Outcome(null, scheduleWakePoints)), Now).GetAwaiter().GetResult();
        return schedule;
    }

    [Fact]
    public void AGameOn_PollsFast() {
        var week = Day(Game(Now.AddHours(-20), TypeName.StatusFinal), Game(Now.AddHours(-1), TypeName.StatusInProgress));
        Assert.Equal(EspnPollCadence.FastPollInterval, Polled().NextInterval(week, Now));
    }

    [Fact]
    public void NothingOn_SleepsUntilTheNextKickoffThisWeek() {
        var week = Day(Game(Now.AddHours(-20), TypeName.StatusFinal), Game(Now.AddHours(2), TypeName.StatusScheduled));
        Assert.Equal(TimeSpan.FromHours(2), Polled().NextInterval(week, Now));
    }

    [Fact]
    public void AKickoffSecondsAway_StillWaitsAtLeastOneFastPoll() =>
        Assert.Equal(EspnPollCadence.FastPollInterval, Polled().NextInterval(Day(Game(Now.AddSeconds(5), TypeName.StatusScheduled)), Now));

    // Week over: wake at the schedule's next boundary (the week flipping, next week's first kickoff)
    // — never sleep so long that a schedule change goes unnoticed.
    [Fact]
    public void WeekOver_SleepsUntilTheNextScheduleWakePoint_Capped() {
        var finished = Day(Game(Now.AddHours(-20), TypeName.StatusFinal));
        Assert.Equal(TimeSpan.FromHours(1), Polled(Now.AddHours(-3), Now.AddHours(1), Now.AddDays(2)).NextInterval(finished, Now));
        Assert.Equal(ScorePollSchedule.IdleCap, Polled(Now.AddDays(4)).NextInterval(finished, Now));
        Assert.Equal(ScorePollSchedule.IdleCap, Polled().NextInterval(finished, Now));
    }

    // Off-season: nothing held, nothing scheduled soon — a check every few hours, not every 5 min.
    [Fact]
    public void OffSeason_SleepsTheIdleCap() =>
        Assert.Equal(ScorePollSchedule.IdleCap, Polled(Now.AddMonths(4)).NextInterval(null, Now));

    // A poll that throws (schedule read failed, scope couldn't be built, ESPN threw) retries at the
    // slow cadence — never sleeps hours on a stale all-final scoreboard and misses a game.
    [Fact]
    public async Task AFailedPoll_RetriesSlow() {
        var schedule = Polled(Now.AddDays(4));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            schedule.PollAsync(() => throw new InvalidOperationException("db down"), Now));
        Assert.Equal(EspnPollCadence.SlowPollInterval, schedule.NextInterval(Day(Game(Now.AddHours(-20), TypeName.StatusFinal)), Now));
    }

    // A week/slate with no games yet (fetcher returns null — e.g. a CFP round before matchups post)
    // is a successful poll, not a failure: sleep to the next wake point, don't retry every 5 min.
    [Fact]
    public async Task AnEmptyWeek_IsNotAFailure() {
        var schedule = new ScorePollSchedule();
        var scores = await schedule.PollAsync(() => Task.FromResult(new ScorePollSchedule.Outcome(null, [Now.AddHours(2)])), Now);
        Assert.Null(scores);
        Assert.Equal(TimeSpan.FromHours(2), schedule.NextInterval(null, Now));
    }

    // ESPN sometimes corrects a score after marking the game final: keep checking for a while —
    // counted from kickoff over the same window EspnDayCache uses, so a long-delayed game still gets it.
    [Fact]
    public void JustFinished_KeepsCheckingForCorrections() {
        Assert.Equal(ScorePollSchedule.RecentlyFinishedInterval,
            Polled(Now.AddDays(4)).NextInterval(Day(Game(Now.AddHours(-9), TypeName.StatusFinal)), Now));
        Assert.Equal(ScorePollSchedule.IdleCap,
            Polled(Now.AddDays(4)).NextInterval(Day(Game(Now - EspnPollCadence.RecentlyFinishedWindow - TimeSpan.FromHours(1), TypeName.StatusFinal)), Now));
    }

    // A game running past the usual 4h window (weather delay, OT) keeps being polled until final.
    [Fact]
    public void AnOverlongGame_IsPolledSlowlyUntilFinal() {
        var week = Day(Game(Now.AddHours(-5), TypeName.StatusInProgress), Game(Now.AddDays(1), TypeName.StatusScheduled));
        Assert.Equal(EspnPollCadence.SlowPollInterval, Polled().NextInterval(week, Now));
    }

    // Postponed/canceled games stay "scheduled" at ESPN: hours past kickoff they're not live.
    [Fact]
    public void AGameStillScheduledLongAfterKickoff_DoesNotKeepPollingFast() {
        var week = Day(Game(Now.AddHours(-20), TypeName.StatusScheduled), Game(Now.AddHours(3), TypeName.StatusScheduled));
        Assert.Equal(TimeSpan.FromHours(3), Polled().NextInterval(week, Now));
    }

    // A malformed competition (no status) is skipped, not thrown on — the refresh loop must never die.
    [Fact]
    public void AGameWithNoStatus_IsIgnored() {
        var broken = Game(Now.AddHours(-1), TypeName.StatusInProgress);
        broken.Competitions[0].Status = null!;
        Assert.Equal(TimeSpan.FromHours(2), Polled().NextInterval(Day(broken, Game(Now.AddHours(2), TypeName.StatusScheduled)), Now));
    }
}
