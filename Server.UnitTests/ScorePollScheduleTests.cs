using FourPlayWebApp.Server.Services;
using FourPlayWebApp.Server.Services.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using NSubstitute;
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
        schedule.PollAsync(() => Task.FromResult(new ScorePollSchedule.Outcome(null, scheduleWakePoints, ExpectedGames: false)), Now).GetAwaiter().GetResult();
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

    // A poll that throws (schedule read failed, scope couldn't be built, ESPN threw) retries soon —
    // never sleeps hours on a stale all-final scoreboard and misses a game.
    [Fact]
    public async Task AFailedPoll_RetriesSoon() {
        var schedule = Polled(Now.AddDays(4));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            schedule.PollAsync(() => throw new InvalidOperationException("db down"), Now));
        Assert.Equal(2 * EspnPollCadence.FastPollInterval, schedule.NextInterval(Day(Game(Now.AddHours(-20), TypeName.StatusFinal)), Now));
    }

    // Blocked by ESPN (403/429) or ESPN down: back off exponentially to the slow cadence rather than
    // hammering it every 15s — even with a game on — and snap back to 15s once it answers again.
    [Fact]
    public async Task RepeatedFailures_BackOffToTheSlowCadence_AndASuccessResets() {
        var schedule = new ScorePollSchedule();
        var gameOn = Day(Game(Now.AddHours(-1), TypeName.StatusInProgress));
        var intervals = new List<TimeSpan>();
        for (var i = 0; i < 6; i++) {
            await Assert.ThrowsAsync<HttpRequestException>(() => schedule.PollAsync(() => throw new HttpRequestException("429"), Now));
            intervals.Add(schedule.NextInterval(gameOn, Now));
        }
        Assert.Equal(new[] { 30, 60, 120, 240, 300, 300 }, intervals.Select(t => (int)t.TotalSeconds));

        await schedule.PollAsync(() => Task.FromResult(new ScorePollSchedule.Outcome(gameOn, [], ExpectedGames: true)), Now);
        Assert.Equal(EspnPollCadence.FastPollInterval, schedule.NextInterval(gameOn, Now));
    }

    // The trap: other days of the week come from the day cache, so a scoreboard still comes back
    // when the live day's request is blocked. That's still a failed poll.
    [Fact]
    public async Task ADayRequestFailing_IsAFailedPoll_EvenWhenAScoreboardCameBack() {
        var time = new FakeTimeProvider(Now);
        var days = new EspnDayCache(new MemoryCache(new MemoryCacheOptions()), time);
        var gameOn = Day(Game(Now.AddHours(-1), TypeName.StatusInProgress));
        await days.GetOrFetchAsync("today", () => Task.FromResult<EspnScores?>(gameOn));
        time.Advance(EspnDayCache.LiveTtl + TimeSpan.FromSeconds(1));

        var schedule = new ScorePollSchedule();
        var scores = await schedule.PollAsync(async () => new ScorePollSchedule.Outcome(
            await days.GetOrFetchAsync("today", () => Task.FromResult<EspnScores?>(null)), [], ExpectedGames: true), time.GetUtcNow());

        Assert.Same(gameOn, scores); // the last good copy is still served
        Assert.Equal(2 * EspnPollCadence.FastPollInterval, schedule.NextInterval(scores, time.GetUtcNow()));
    }

    // Someone should hear about it: once ESPN has been failing for 10 minutes straight, the outage
    // goes to the job-failure channel (Discord). Every failed poll after that is handed over under
    // the same outage key — the notifier's own dedupe turns that into one message, and a send that
    // failed (webhook down) is retried on the next poll instead of being lost.
    [Fact]
    public async Task AnOutage_IsReportedAfterTenMinutes_UnderOneKey() {
        var notifier = Substitute.For<IJobFailureNotifier>();
        var schedule = new ScorePollSchedule("CFB live scores", notifier);
        Task Fail(TimeSpan at) => Assert.ThrowsAsync<HttpRequestException>(() =>
            schedule.PollAsync(() => throw new HttpRequestException("403 (Forbidden)"), Now + at));

        await Fail(TimeSpan.Zero);
        await Fail(TimeSpan.FromMinutes(5));
        await notifier.DidNotReceiveWithAnyArgs().NotifyAsync(default!, default!, default!);

        await Fail(TimeSpan.FromMinutes(10));
        await Fail(TimeSpan.FromMinutes(15));
        var reports = notifier.ReceivedCalls().Select(c => c.GetArguments()).ToList();
        Assert.Equal(2, reports.Count);
        Assert.Single(reports.Select(a => (string)a[0]!).Distinct());
        Assert.StartsWith("CFB live scores", (string)reports[0][0]!);
        Assert.Contains("403", (string)reports[0][2]!);
    }

    // A recovered outage is over: the next one is a new key, so it isn't swallowed by the
    // notifier's 6h dedupe of the first.
    [Fact]
    public async Task AfterRecovery_ANewOutageIsReportedUnderANewKey() {
        var notifier = Substitute.For<IJobFailureNotifier>();
        var schedule = new ScorePollSchedule("NFL live scores", notifier);
        Task Fail(TimeSpan at) => Assert.ThrowsAsync<HttpRequestException>(() =>
            schedule.PollAsync(() => throw new HttpRequestException("timeout"), Now + at));

        await Fail(TimeSpan.Zero);
        await Fail(TimeSpan.FromMinutes(10));
        await schedule.PollAsync(() => Task.FromResult(new ScorePollSchedule.Outcome(Day(), [], ExpectedGames: true)), Now + TimeSpan.FromMinutes(11));
        await Fail(TimeSpan.FromMinutes(20));
        await Fail(TimeSpan.FromMinutes(30));

        var keys = notifier.ReceivedCalls().Select(c => (string)c.GetArguments()[0]!).ToList();
        Assert.Equal(2, keys.Count);
        Assert.NotEqual(keys[0], keys[1]);
    }

    // A week/slate with no games yet (e.g. a CFP round before matchups post) is a successful poll,
    // not a failure: sleep to the next wake point, don't retry every 5 min...
    [Fact]
    public async Task AnEmptyWeek_IsNotAFailure() {
        var schedule = new ScorePollSchedule();
        var scores = await schedule.PollAsync(() => Task.FromResult(new ScorePollSchedule.Outcome(null, [Now.AddHours(2)], ExpectedGames: false)), Now);
        Assert.Null(scores);
        Assert.Equal(TimeSpan.FromHours(2), schedule.NextInterval(null, Now));
    }

    // ...but no scoreboard for a week that has games means ESPN failed (the API services swallow
    // HTTP errors into null): retry soon, don't sleep through an outage.
    [Fact]
    public async Task NoScoreboardForAWeekWithGames_IsAFailure() {
        var schedule = new ScorePollSchedule();
        await schedule.PollAsync(() => Task.FromResult(new ScorePollSchedule.Outcome(null, [Now.AddDays(4)], ExpectedGames: true)), Now);
        Assert.Equal(2 * EspnPollCadence.FastPollInterval, schedule.NextInterval(Day(Game(Now.AddHours(-20), TypeName.StatusFinal)), Now));
    }

    // Once every game is final the poller stops: no re-checks for score corrections (ESPN doesn't
    // issue them in practice, and the scores job persists finals on its own schedule).
    [Fact]
    public void AllGamesFinal_SleepsUntilTheNextKickoff() =>
        Assert.Equal(TimeSpan.FromHours(2),
            Polled(Now.AddDays(4)).NextInterval(Day(Game(Now.AddMinutes(-230), TypeName.StatusFinal), Game(Now.AddHours(2), TypeName.StatusScheduled)), Now));

    // A game running past the usual 4h window (weather delay, OT) is still a game on: polled fast
    // until it goes final — but a status stuck "in progress" can't poll forever.
    [Fact]
    public void AnOverlongGame_IsPolledFastUntilFinal() {
        Assert.Equal(EspnPollCadence.FastPollInterval, Polled().NextInterval(Day(Game(Now.AddHours(-5), TypeName.StatusInProgress)), Now));
        Assert.Equal(ScorePollSchedule.IdleCap, Polled().NextInterval(Day(Game(Now.AddHours(-13), TypeName.StatusInProgress)), Now));
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
