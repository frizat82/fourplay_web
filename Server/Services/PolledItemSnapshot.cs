using FourPlayWebApp.Shared.Models;

namespace FourPlayWebApp.Server.Services;

/// <summary>
/// Which item (an NFL week / CFB slate, by its settled-cache key) the background poller last
/// fetched, together with that result — so a request for that same item is answered from the poll
/// instead of its own live ESPN call. Before this, EspnCacheService.GetWeekScoresAsync and
/// CfbCacheService.GetSlateScoresAsync live-fetched the CURRENT week/slate on every client request
/// (prod p50 ~300-550ms, p95 up to 5s) — on every Picks/Scores/Dashboard load, and from every
/// connected client at once on each SSE push — while the poller already held exactly that data.
/// Key and result are stored as one immutable entry, so a reader never pairs one poll's key with
/// another poll's data. An entry older than maxAge is ignored — if the poller stops refreshing
/// (ESPN failing mid-game, the loop dying), requests fall back to their own live fetch instead of
/// being served the last poll's scores indefinitely.
/// </summary>
public sealed class PolledItemSnapshot(TimeSpan maxAge, TimeProvider? timeProvider = null) {
    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;
    // FullAt: when this item last had a full-window fetch (vs a live-day merge — see RefreshAsync).
    private sealed record Entry(string Key, EspnScores Scores, DateTimeOffset At, DateTimeOffset FullAt);
    private volatile Entry? _latest;

    /// <summary>Records a poll's result for <paramref name="key"/> (a null result clears it) and returns the result.</summary>
    public EspnScores? Record(string key, EspnScores? scores) {
        var now = _time.GetUtcNow();
        _latest = scores is null ? null : new Entry(key, scores, now, now);
        return scores;
    }

    /// <summary>Clears the snapshot (the poller fetched nothing, e.g. off-season) and returns null.</summary>
    public EspnScores? Clear() {
        _latest = null;
        return null;
    }

    /// <summary>
    /// One poll of <paramref name="key"/> (shared by the NFL and CFB pollers). While a game is live
    /// and this item had a full fetch within <see cref="EspnPollCadence.LiveFullRefreshInterval"/>,
    /// only the live day is fetched and merged (LiveDayRefresh); otherwise — first poll, a new
    /// week/slate, nothing live, the day fetch came back empty, or the full refresh is due — the
    /// full window is fetched. Returns (and records) the result.
    /// </summary>
    public async Task<EspnScores?> RefreshAsync(string key,
        Func<IReadOnlyCollection<DateOnly>, Task<EspnScores?>> fetchDays, Func<Task<EspnScores?>> fetchAll) {
        var now = _time.GetUtcNow();
        var latest = _latest;
        if (latest is not null && latest.Key == key && now - latest.At <= maxAge
            && now - latest.FullAt < EspnPollCadence.LiveFullRefreshInterval
            && await LiveDayRefresh.TryMergeLiveDaysAsync(latest.Scores, now, fetchDays) is { } merged) {
            _latest = latest with { Scores = merged, At = now };
            return merged;
        }
        return Record(key, await fetchAll());
    }

    public bool TryGet(string key, out EspnScores? scores) {
        var latest = _latest;
        scores = latest is not null && latest.Key == key && _time.GetUtcNow() - latest.At <= maxAge ? latest.Scores : null;
        return scores is not null;
    }
}
