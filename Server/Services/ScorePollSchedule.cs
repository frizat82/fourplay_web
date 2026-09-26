using FourPlayWebApp.Shared.Helpers;
using FourPlayWebApp.Shared.Models;

namespace FourPlayWebApp.Server.Services;

/// <summary>
/// Runs one score poll and decides when the next happens — one implementation for the NFL
/// (EspnCacheService) and CFB (CfbCacheService) pollers, one instance each. Fast while a game is
/// on; otherwise asleep until the next thing that can change what the poller serves — a kickoff
/// in the scoreboard it holds, or a schedule wake point (a week/slate flipping current, a first
/// kickoff, next season) — capped at <see cref="IdleCap"/> so an out-of-band schedule change still
/// gets noticed. A poll that throws retries at the slow cadence. Replaces "every 5 min whenever
/// nothing is live", which polled all week and all off-season.
/// </summary>
public sealed class ScorePollSchedule {
    /// <summary>The longest a poller sleeps without a known reason to wake.</summary>
    public static readonly TimeSpan IdleCap = TimeSpan.FromHours(3);
    /// <summary>After a game goes final, ESPN sometimes corrects the score; keep checking this often for a while.</summary>
    public static readonly TimeSpan RecentlyFinishedInterval = TimeSpan.FromMinutes(15);
    // A game still in progress past the usual live window (weather delay, OT) is polled slowly
    // until it goes final, for at most this long after kickoff (a stuck ESPN status can't poll forever).
    private static readonly TimeSpan OverlongGameLimit = TimeSpan.FromHours(12);

    /// <summary>
    /// What a poll produced: its scoreboard, the schedule's wake points, and whether the week/slate
    /// it fetched has games. The API services turn ESPN errors into a null scoreboard, so null for a
    /// week that has games is a failed poll; null for one without (off-season, a CFP round before
    /// matchups post) isn't.
    /// </summary>
    public readonly record struct Outcome(EspnScores? Scores, IReadOnlyCollection<DateTimeOffset> WakePoints, bool ExpectedGames);

    private long _nextWakePointTicks; // UTC ticks; 0 = none known
    private volatile bool _lastPollFailed;

    /// <summary>
    /// Runs the whole poll — scope setup, schedule reads, the ESPN fetch. Anything it throws marks
    /// the poll failed (retried at the slow cadence) and propagates to the refresh engine; a poll
    /// that completes records the schedule's wake points, and fails only if a week that has games
    /// came back with no scoreboard.
    /// </summary>
    public async Task<EspnScores?> PollAsync(Func<Task<Outcome>> poll, DateTimeOffset now) {
        _lastPollFailed = true;
        var outcome = await poll();
        var next = outcome.WakePoints.Where(p => p > now).Select(p => (DateTimeOffset?)p).Min();
        Interlocked.Exchange(ref _nextWakePointTicks, next?.UtcTicks ?? 0);
        _lastPollFailed = outcome.Scores is null && outcome.ExpectedGames;
        return outcome.Scores;
    }

    public TimeSpan NextInterval(EspnScores? current, DateTimeOffset now) {
        // A competition missing its status (malformed payload) is skipped, not thrown on.
        var games = current?.Events?.SelectMany(e => e.Competitions ?? []).Where(g => g.Status?.Type is not null).ToList() ?? [];
        var unfinished = games.Where(g => !GameHelpers.IsGameOver(g)).ToList();

        if (unfinished.Any(g => g.Date <= now && now <= g.Date + EspnPollCadence.LiveGameDuration))
            return EspnPollCadence.FastPollInterval;
        if (_lastPollFailed) return EspnPollCadence.SlowPollInterval;

        var wakeAt = new List<DateTimeOffset>();
        if (unfinished.Any(g => GameHelpers.IsGameStarted(g) && now <= g.Date + OverlongGameLimit))
            wakeAt.Add(now + EspnPollCadence.SlowPollInterval);
        if (games.Any(g => GameHelpers.IsGameOver(g) && now <= g.Date + EspnPollCadence.RecentlyFinishedWindow))
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
