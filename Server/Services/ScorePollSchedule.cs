using FourPlayWebApp.Shared.Models;

namespace FourPlayWebApp.Server.Services;

/// <summary>
/// Runs one score poll and decides when the next happens — one implementation for the NFL
/// (EspnCacheService) and CFB (CfbCacheService) pollers, one instance each. Polls only while a
/// game is on; once every game is final it stops (the scores jobs persist finals) and sleeps until
/// the next thing that can change what the poller serves — a kickoff in the scoreboard it holds,
/// or a schedule wake point (a week/slate flipping current, a first kickoff, next season) — capped
/// at <see cref="IdleCap"/> so an out-of-band schedule change still gets noticed. A failed poll
/// retries at the slow cadence.
/// </summary>
public sealed class ScorePollSchedule {
    /// <summary>The longest a poller sleeps without a known reason to wake.</summary>
    public static readonly TimeSpan IdleCap = TimeSpan.FromHours(3);

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
        if (games.Any(g => EspnPollCadence.IsLive(g, now))) return EspnPollCadence.FastPollInterval;
        if (_lastPollFailed) return EspnPollCadence.SlowPollInterval;

        var wakeAt = games.Where(g => EspnPollCadence.IsUpcoming(g, now)).Select(g => g.Date).ToList();
        var scheduledTicks = Interlocked.Read(ref _nextWakePointTicks);
        if (scheduledTicks > now.UtcTicks) wakeAt.Add(new DateTimeOffset(scheduledTicks, TimeSpan.Zero));

        if (wakeAt.Count == 0) return IdleCap;
        var untilNext = wakeAt.Min() - now;
        return untilNext < EspnPollCadence.FastPollInterval ? EspnPollCadence.FastPollInterval
            : untilNext > IdleCap ? IdleCap
            : untilNext;
    }
}
