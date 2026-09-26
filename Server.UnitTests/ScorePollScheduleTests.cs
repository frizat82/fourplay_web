using FourPlayWebApp.Server.Services;
using FourPlayWebApp.Shared.Models;
using static FourPlayWebApp.Server.UnitTests.EspnDayCacheTests;

namespace FourPlayWebApp.Server.UnitTests;

// When the NFL and CFB score pollers wake up next. They used to poll every 5 min whenever no game
// was live — all week, and all off-season. Now they poll fast only while a game is on, and
// otherwise sleep until the next kickoff they know about (this week's scoreboard, or the schedule
// table's next week/slate), capped so a schedule change still gets noticed.
public class ScorePollScheduleTests {
    private static readonly DateTimeOffset Now = new(2026, 9, 27, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public void AGameOn_PollsFast() {
        var schedule = new ScorePollSchedule();
        var week = Day(Game(Now.AddHours(-20), TypeName.StatusFinal), Game(Now.AddHours(-1), TypeName.StatusInProgress));
        Assert.Equal(EspnPollCadence.FastPollInterval, schedule.NextInterval(week, Now));
    }

    [Fact]
    public void NothingOn_SleepsUntilTheNextKickoffThisWeek() {
        var schedule = new ScorePollSchedule();
        var week = Day(Game(Now.AddHours(-20), TypeName.StatusFinal), Game(Now.AddHours(2), TypeName.StatusScheduled));
        Assert.Equal(TimeSpan.FromHours(2), schedule.NextInterval(week, Now));
    }

    [Fact]
    public void AKickoffSeconds_Away_StillWaitsAtLeastOneFastPoll() {
        var schedule = new ScorePollSchedule();
        Assert.Equal(EspnPollCadence.FastPollInterval, schedule.NextInterval(Day(Game(Now.AddSeconds(5), TypeName.StatusScheduled)), Now));
    }

    // Week over: wake for next week's first kickoff (from the schedule table), but never sleep so
    // long that a schedule change goes unnoticed.
    [Fact]
    public void WeekOver_SleepsUntilNextWeeksFirstKickoff_Capped() {
        var finished = Day(Game(Now.AddHours(-20), TypeName.StatusFinal));
        var schedule = new ScorePollSchedule();

        schedule.SetNextScheduledKickoff(Now.AddHours(1));
        Assert.Equal(TimeSpan.FromHours(1), schedule.NextInterval(finished, Now));

        schedule.SetNextScheduledKickoff(Now.AddDays(4));
        Assert.Equal(ScorePollSchedule.IdleCap, schedule.NextInterval(finished, Now));

        schedule.SetNextScheduledKickoff(null);
        Assert.Equal(ScorePollSchedule.IdleCap, schedule.NextInterval(finished, Now));
    }

    // Off-season (or first boot): no scoreboard held. Sleep until the schedule says to, rather
    // than every 5 min all summer.
    [Fact]
    public void NoScoreboard_SleepsUntilTheScheduleSaysSo_Or5MinWhenNothingIsKnown() {
        var schedule = new ScorePollSchedule();
        Assert.Equal(EspnPollCadence.SlowPollInterval, schedule.NextInterval(null, Now));

        schedule.SetNextScheduledKickoff(Now.AddMonths(4));
        Assert.Equal(ScorePollSchedule.IdleCap, schedule.NextInterval(null, Now));
    }

    // A game running past the usual 4h window (weather delay, OT) keeps being polled until it
    // goes final — at the slow cadence, so the final score still lands.
    [Fact]
    public void AnOverlongGame_IsPolledSlowlyUntilFinal() {
        var schedule = new ScorePollSchedule();
        var week = Day(Game(Now.AddHours(-5), TypeName.StatusInProgress), Game(Now.AddDays(1), TypeName.StatusScheduled));
        Assert.Equal(EspnPollCadence.SlowPollInterval, schedule.NextInterval(week, Now));
    }

    // Postponed/canceled games stay "scheduled" at ESPN: hours past kickoff they're not live.
    [Fact]
    public void AGameStillScheduledLongAfterKickoff_DoesNotKeepPollingFast() {
        var schedule = new ScorePollSchedule();
        var week = Day(Game(Now.AddHours(-20), TypeName.StatusScheduled), Game(Now.AddHours(3), TypeName.StatusScheduled));
        Assert.Equal(TimeSpan.FromHours(3), schedule.NextInterval(week, Now));
    }
}
