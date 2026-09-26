using FourPlayWebApp.Shared.Models;

namespace FourPlayWebApp.Server.Services;

/// <summary>
/// When a score poller (NFL EspnCacheService / CFB CfbCacheService — one instance each, one
/// implementation) wakes up next. Fast while a game is on; otherwise asleep until the next thing
/// that can change what it serves — a kickoff in the scoreboard it holds, or a schedule wake point
/// (a week/slate flipping current, a first kickoff, next season — recorded by
/// <see cref="PollSucceeded"/>) — capped at <see cref="IdleCap"/> so a schedule change still gets
/// noticed. A failed poll retries at the slow cadence. Replaces "every 5 min whenever nothing is
/// live", which polled all week and all off-season.
/// </summary>
public sealed class ScorePollSchedule {
    /// <summary>The longest a poller sleeps without a known reason to wake.</summary>
    public static readonly TimeSpan IdleCap = TimeSpan.FromHours(3);
    /// <summary>After a game goes final, ESPN sometimes corrects the score; keep checking this often for a while.</summary>
    public static readonly TimeSpan RecentlyFinishedInterval = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan RecentlyFinishedWindow = TimeSpan.FromHours(6);
    // A game still in progress past the usual live window (weather delay, OT) is polled slowly
    // until it goes final, for at most this long after kickoff (a stuck ESPN status can't poll forever).
    private static readonly TimeSpan OverlongGameLimit = TimeSpan.FromHours(12);

    private long _nextWakePointTicks; // UTC ticks; 0 = none known
    private volatile bool _lastPollFailed;

    /// <summary>Call as a poll starts: it counts as failed until <see cref="PollSucceeded"/>.</summary>
    public void PollStarted() => _lastPollFailed = true;

    /// <summary>
    /// Call once a poll has done its work (including "off-season, nothing to fetch"), with the
    /// schedule's wake points: every instant the current week/slate can flip and every first kickoff.
    /// </summary>
    public void PollSucceeded(IEnumerable<DateTimeOffset> scheduleWakePoints, DateTimeOffset now) {
        var next = scheduleWakePoints.Where(p => p > now).Select(p => (DateTimeOffset?)p).Min();
        Interlocked.Exchange(ref _nextWakePointTicks, next?.UtcTicks ?? 0);
        _lastPollFailed = false;
    }

    public TimeSpan NextInterval(EspnScores? current, DateTimeOffset now) {
        // A competition missing its status (malformed payload) is skipped, not thrown on.
        var games = current?.Events?.SelectMany(e => e.Competitions ?? []).Where(g => g.Status?.Type is not null).ToList() ?? [];
        var unfinished = games.Where(g => g.Status.Type.Name != TypeName.StatusFinal).ToList();

        if (unfinished.Any(g => g.Date <= now && now <= g.Date + EspnPollCadence.LiveGameDuration))
            return EspnPollCadence.FastPollInterval;
        if (_lastPollFailed) return EspnPollCadence.SlowPollInterval;

        var wakeAt = new List<DateTimeOffset>();
        if (unfinished.Any(g => g.Status.Type.Name != TypeName.StatusScheduled && now <= g.Date + OverlongGameLimit))
            wakeAt.Add(now + EspnPollCadence.SlowPollInterval);
        if (games.Any(g => g.Status.Type.Name == TypeName.StatusFinal && now <= g.Date + RecentlyFinishedWindow))
            wakeAt.Add(now + RecentlyFinishedInterval);
        wakeAt.AddRange(unfinished.Where(g => g.Date > now).Select(g => g.Date));
        var scheduledTicks = Interlocked.Read(ref _nextWakePointTicks);
        if (scheduledTicks > now.UtcTicks) wakeAt.Add(new DateTimeOffset(scheduledTicks, TimeSpan.Zero));

        if (wakeAt.Count == 0) return IdleCap;
        var untilNext = wakeAt.Min() - now;
        return untilNext < EspnPollCadence.FastPollInterval ? EspnPollCadence.FastPollInterval
            : untilNext > IdleCap ? IdleCap
            : untilNext;
    }
}
