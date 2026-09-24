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
/// another poll's data.
/// </summary>
public sealed class PolledItemSnapshot {
    private sealed record Entry(string Key, EspnScores Scores);
    private volatile Entry? _latest;

    /// <summary>Records a poll's result for <paramref name="key"/> (a null result clears it) and returns the result.</summary>
    public EspnScores? Record(string key, EspnScores? scores) {
        _latest = scores is null ? null : new Entry(key, scores);
        return scores;
    }

    /// <summary>Clears the snapshot (the poller fetched nothing, e.g. off-season) and returns null.</summary>
    public EspnScores? Clear() {
        _latest = null;
        return null;
    }

    public bool TryGet(string key, out EspnScores? scores) {
        var latest = _latest;
        scores = latest is not null && latest.Key == key ? latest.Scores : null;
        return scores is not null;
    }
}
