using Microsoft.Extensions.Caching.Memory;
using FourPlayWebApp.Shared.Models;

namespace FourPlayWebApp.Server.Services;

// frizat-d0t: EspnCacheService.GetWeekScoresAsync (NFL) and CfbLiveScoreFetcher.
// FetchForSlateAsync's settled-cache branch (CFB) independently implemented the identical
// algorithm — check an IMemoryCache, else read persisted rows once the item's own window has
// ended and build a response, cache it forever, else fall through to a live fetch. One shared
// orchestration instead of two hand-copied ones.
//
// Callers still own resolving isCurrentItem (NFL: SeasonWindowResolver.ResolveCurrentWeek; CFB:
// ICfbCurrentSlateService.GetCurrentSlateAsync()?.Id == slate.Id — genuinely different mechanisms
// today, not unified here), windowEndUtc, and how to build a response from persisted rows
// (FinalScoresEspnMapper.Build with sport-specific row-mapping — this class deliberately doesn't
// know that type exists, only that a build attempt returns null on nothing-to-build). Not
// DI-registered itself — each cache service constructs one directly over its own
// already-injected IMemoryCache, mirroring how EspnCacheService already constructs its
// PeriodicRefreshCache<EspnScores> inline.
public class SettledScoreCache(IMemoryCache cache) {
    // 6-hour buffer past the item's own configured end — a late kickoff (CFB evening games
    // crossing UTC midnight, NFL Monday Night Football) can still be in progress after the
    // calendar window "ends"; this now gets cached forever once true, so the buffer matters more
    // than it would for a short-lived cache.
    private static readonly TimeSpan EndOfWindowBuffer = TimeSpan.FromHours(6);

    // Cheap fast-path exit for callers to check BEFORE doing any DB work to resolve the
    // config/slate needed for GetOrBuildAsync's other parameters — a cache hit needs none of
    // that. GetOrBuildAsync repeats this same check internally (a plain dictionary lookup, not
    // I/O — negligible) so it stays correct and usable standalone.
    public bool TryGet(string cacheKey, out EspnScores? cached) => cache.TryGetValue(cacheKey, out cached);

    public async Task<EspnScores?> GetOrBuildAsync(
        string cacheKey, bool isCurrentItem, DateTime windowEndUtc,
        Func<Task<EspnScores?>> buildFromRowsAsync,
        Func<Task<EspnScores?>> liveFetchAsync) {
        if (cache.TryGetValue<EspnScores>(cacheKey, out var cached)) return cached;

        // The control-table-resolved CURRENT item is always live-fetched, even if its own
        // calendar window already looks "ended" — SeasonWindowResolver/ICfbCurrentSlateService
        // can legitimately keep an old window as "current" well past its nominal end (e.g. the
        // off-season bootstrap case), and the frontend's current-item path always asks for
        // exactly that item's real live/final ESPN state, not a DB reconstruction with no live
        // situation/clock data.
        var windowHasEnded = !isCurrentItem && windowEndUtc + EndOfWindowBuffer < DateTime.UtcNow;

        if (windowHasEnded) {
            // Caller's own delegate returns null when there's nothing to build (e.g. zero
            // persisted rows) — a still-active item that merely hasn't had any games persisted
            // yet must not freeze an empty response in place once games actually finish and get
            // persisted, so only a real build gets cached.
            var built = await buildFromRowsAsync();
            if (built is not null) {
                cache.Set(cacheKey, built);
                return built;
            }
        }

        return await liveFetchAsync();
    }

    public void Invalidate(string cacheKey) => cache.Remove(cacheKey);
}
