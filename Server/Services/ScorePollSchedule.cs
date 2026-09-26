using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Shared.Models;
using Serilog;

namespace FourPlayWebApp.Server.Services;

/// <summary>
/// Runs one score poll and decides when the next happens — one implementation for the NFL
/// (EspnCacheService) and CFB (CfbCacheService) pollers, one instance each. Polls only while a
/// game is on; once every game is final it stops (the scores jobs persist finals) and sleeps until
/// the next thing that can change what the poller serves — a kickoff in the scoreboard it holds,
/// or a schedule wake point (a week/slate flipping current, a first kickoff, next season) — capped
/// at <see cref="IdleCap"/> so an out-of-band schedule change still gets noticed.
/// <para>A failed poll — anything thrown, any ESPN day request failing (EspnDayCache then serves
/// the last good copy), or no scoreboard for a week with games — backs off exponentially from 30s
/// to the slow cadence, even with a game on, so a block (403/429) or outage isn't hammered every
/// 15s. After <see cref="OutageAlertAfter"/> of failures, the outage goes to the job-failure
/// channel under a per-outage key: the notifier's dedupe sends one message per outage and retries a
/// send that failed, and a later outage is a new key rather than swallowed by that dedupe.</para>
/// </summary>
public sealed class ScorePollSchedule(string name = "ESPN score poller", IJobFailureNotifier? outageNotifier = null) {
    /// <summary>The longest a poller sleeps without a known reason to wake.</summary>
    public static readonly TimeSpan IdleCap = TimeSpan.FromHours(3);
    /// <summary>How long polls must keep failing before someone is told.</summary>
    public static readonly TimeSpan OutageAlertAfter = TimeSpan.FromMinutes(10);

    /// <summary>
    /// What a poll produced: its scoreboard, the schedule's wake points, and whether the week/slate
    /// it fetched has games. The API services turn ESPN errors into a null scoreboard, so null for a
    /// week that has games is a failed poll; null for one without (off-season, a CFP round before
    /// matchups post) isn't.
    /// </summary>
    public readonly record struct Outcome(EspnScores? Scores, IReadOnlyCollection<DateTimeOffset> WakePoints, bool ExpectedGames);

    private long _nextWakePointTicks; // UTC ticks; 0 = none known
    private int _consecutiveFailures;
    private DateTimeOffset? _failingSince;
    private bool _outageLogged;

    /// <summary>
    /// Runs the whole poll — scope setup, schedule reads, the ESPN fetch — and records whether it
    /// failed (anything thrown propagates to the refresh engine after being recorded). A poll that
    /// completes also records the schedule's wake points.
    /// </summary>
    public async Task<EspnScores?> PollAsync(Func<Task<Outcome>> poll, DateTimeOffset now) {
        using var dayRequests = EspnDayCache.TrackFailures();
        Outcome outcome;
        try {
            outcome = await poll();
        } catch (Exception ex) {
            await RecordAsync(ex.Message, now);
            throw;
        }
        var next = outcome.WakePoints.Where(p => p > now).Select(p => (DateTimeOffset?)p).Min();
        Interlocked.Exchange(ref _nextWakePointTicks, next?.UtcTicks ?? 0);
        var failure = dayRequests.AnyFailed ? "an ESPN scoreboard request failed (serving the last good copy)"
            : outcome.Scores is null && outcome.ExpectedGames ? "ESPN returned no scoreboard for a week with games"
            : null;
        await RecordAsync(failure, now);
        return outcome.Scores;
    }

    private async Task RecordAsync(string? failure, DateTimeOffset now) {
        if (failure is null) {
            if (_failingSince is { } since)
                Log.Information("{Poller}: ESPN answering again after {Minutes:F0} min of failed polls", name, (now - since).TotalMinutes);
            _failingSince = null;
            _outageLogged = false;
            Volatile.Write(ref _consecutiveFailures, 0);
            return;
        }
        var failures = Interlocked.Increment(ref _consecutiveFailures);
        _failingSince ??= now;
        var outageStart = _failingSince.Value;
        if (now - outageStart < OutageAlertAfter) return;
        var message = $"ESPN polls failing for {(now - outageStart).TotalMinutes:F0} min ({failures} in a row), last: {failure}. " +
                      $"Serving the last good scores and retrying every {EspnPollCadence.SlowPollInterval.TotalMinutes:F0} min until ESPN answers.";
        if (!_outageLogged) {
            _outageLogged = true;
            Log.Error("{Poller}: {Message}", name, message);
        }
        if (outageNotifier is not null)
            await outageNotifier.NotifyAsync($"{name} — ESPN outage since {outageStart:yyyy-MM-dd HH:mm} UTC", "score poll", message);
    }

    public TimeSpan NextInterval(EspnScores? current, DateTimeOffset now) {
        // A competition missing its status (malformed payload) is skipped, not thrown on.
        var games = current?.Events?.SelectMany(e => e.Competitions ?? []).Where(g => g.Status?.Type is not null).ToList() ?? [];
        var failures = Volatile.Read(ref _consecutiveFailures);
        if (failures > 0) return Backoff(failures);
        if (games.Any(g => EspnPollCadence.IsLive(g, now))) return EspnPollCadence.FastPollInterval;

        var wakeAt = games.Where(g => EspnPollCadence.IsUpcoming(g, now)).Select(g => g.Date).ToList();
        var scheduledTicks = Interlocked.Read(ref _nextWakePointTicks);
        if (scheduledTicks > now.UtcTicks) wakeAt.Add(new DateTimeOffset(scheduledTicks, TimeSpan.Zero));

        if (wakeAt.Count == 0) return IdleCap;
        var untilNext = wakeAt.Min() - now;
        return untilNext < EspnPollCadence.FastPollInterval ? EspnPollCadence.FastPollInterval
            : untilNext > IdleCap ? IdleCap
            : untilNext;
    }

    // 30s, 1m, 2m, 4m, then the slow cadence.
    private static TimeSpan Backoff(int failures) {
        var backoff = EspnPollCadence.FastPollInterval * Math.Pow(2, Math.Min(failures, 10));
        return backoff < EspnPollCadence.SlowPollInterval ? backoff : EspnPollCadence.SlowPollInterval;
    }
}
