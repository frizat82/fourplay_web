using FourPlayWebApp.Shared.Helpers;
using FourPlayWebApp.Shared.Models;

namespace FourPlayWebApp.Server.Services;

/// <summary>
/// When a score poller (NFL EspnCacheService / CFB CfbCacheService — one instance each, one
/// implementation) wakes up next. Fast while a game is on; otherwise asleep until the next kickoff
/// it knows about — an unplayed game in the scoreboard it holds, or the schedule table's next
/// week/slate (<see cref="SetNextScheduledKickoff"/>) — capped at <see cref="IdleCap"/> so a
/// schedule change still gets noticed. Replaces "every 5 min whenever nothing is live", which
/// polled all week and all off-season.
/// </summary>
public sealed class ScorePollSchedule {
    /// <summary>The longest a poller sleeps without a known reason to wake.</summary>
    public static readonly TimeSpan IdleCap = TimeSpan.FromHours(3);
    // A game still in progress past the usual live window (weather delay, OT) is polled slowly
    // until it goes final, for at most this long after kickoff (a stuck ESPN status can't poll forever).
    private static readonly TimeSpan OverlongGameLimit = TimeSpan.FromHours(12);

    private long _nextScheduledKickoffTicks; // UTC ticks; 0 = none known

    /// <summary>The next first-kickoff in the schedule table (next week/slate or next season), if any.</summary>
    public void SetNextScheduledKickoff(DateTimeOffset? kickoff) =>
        Interlocked.Exchange(ref _nextScheduledKickoffTicks, kickoff?.UtcTicks ?? 0);

    /// <summary>The earliest of each week's/slate's first kickoff that's still ahead of <paramref name="now"/>.</summary>
    public void SetNextScheduledKickoff(IEnumerable<DateTimeOffset> firstKickoffs, DateTimeOffset now) =>
        SetNextScheduledKickoff(firstKickoffs.Where(k => k > now).Select(k => (DateTimeOffset?)k).Min());

    public TimeSpan NextInterval(EspnScores? current, DateTimeOffset now) {
        var games = current?.Events?.SelectMany(e => e.Competitions).ToList() ?? [];
        var unfinished = games.Where(g => !GameHelpers.IsGameOver(g)).ToList();

        if (unfinished.Any(g => g.Date <= now && now <= g.Date + EspnPollCadence.LiveGameDuration))
            return EspnPollCadence.FastPollInterval;

        var wakeAt = new List<DateTimeOffset>();
        if (unfinished.Any(g => GameHelpers.IsGameStarted(g) && now <= g.Date + OverlongGameLimit))
            wakeAt.Add(now + EspnPollCadence.SlowPollInterval);
        wakeAt.AddRange(unfinished.Where(g => g.Date > now).Select(g => g.Date));
        var scheduledTicks = Interlocked.Read(ref _nextScheduledKickoffTicks);
        if (scheduledTicks > 0 && scheduledTicks > now.UtcTicks) wakeAt.Add(new DateTimeOffset(scheduledTicks, TimeSpan.Zero));

        if (wakeAt.Count == 0) return current is null ? EspnPollCadence.SlowPollInterval : IdleCap;
        var untilNext = wakeAt.Min() - now;
        return untilNext < EspnPollCadence.FastPollInterval ? EspnPollCadence.FastPollInterval
            : untilNext > IdleCap ? IdleCap
            : untilNext;
    }
}
