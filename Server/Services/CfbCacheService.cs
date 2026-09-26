using FourPlayWebApp.Server.Jobs;
using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Server.Services.Repositories.Interfaces;
using FourPlayWebApp.Shared.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace FourPlayWebApp.Server.Services;

// Service that periodically refreshes the CURRENT CFB slate's scores from ESPN and caches them in
// memory, so concurrent controller requests share one poll instead of each triggering its own
// ESPN call — the same problem EspnCacheService solves for NFL. Wraps the same shared
// PeriodicRefreshCache engine (frizat-703.6); differs from NFL only in how it resolves "what to
// fetch" (current CFB slate + CFP/ranked-team filtering via ICfbLiveScoreFetcher).
//
// ICfbCurrentSlateService/ICfbRepository are registered Scoped (matches their use in per-request
// controllers), but this service is a long-lived Singleton — resolving them directly in the
// constructor would be a captive-dependency DI violation. Creates a fresh scope per refresh
// instead, the standard pattern for a singleton background service consuming scoped dependencies.
public class CfbCacheService : ICfbCacheService, IAsyncDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ICfbLiveScoreFetcher _fetcher;
    private readonly SettledScoreCache _settledCache;
    private readonly PeriodicRefreshCache<EspnScores> _cache;
    // Fresh for 2x the slow poll: during games the poller refreshes every 15s. Between games it
    // sleeps for hours, so the snapshot lapses and requests fetch for themselves — through
    // EspnDayCache, so that's an in-memory read for any day that can't have changed.
    private readonly PolledItemSnapshot _polled = new(2 * EspnPollCadence.SlowPollInterval);
    private readonly ScorePollSchedule _pollSchedule = new();

    private static string SlateCacheKey(int slateId) => $"cfb-slate-scores_{slateId}";

    public event Action? ScoresChanged
    {
        add => _cache.Changed += value;
        remove => _cache.Changed -= value;
    }

    public CfbCacheService(
        IServiceScopeFactory scopeFactory,
        ICfbLiveScoreFetcher fetcher,
        IMemoryCache settledCache,
        TimeSpan? initialDelay = null)
    {
        _scopeFactory = scopeFactory;
        _fetcher = fetcher;
        _settledCache = new SettledScoreCache(settledCache);
        _cache = new PeriodicRefreshCache<EspnScores>(
            fetch: () => _pollSchedule.PollAsync(async () => {
                using var scope = scopeFactory.CreateScope();
                var currentSlateService = scope.ServiceProvider.GetRequiredService<ICfbCurrentSlateService>();
                var cfbRepo = scope.ServiceProvider.GetRequiredService<ICfbRepository>();

                // When to wake next once nothing is on: each slate's window and first kickoff —
                // enough to catch a season starting. Spread-lock points are added once in season
                // (BuildSlateWindows needs every slate's config row, and throws when one's missing).
                var slates = (await cfbRepo.GetAllSlatesAsync()).ToList();
                var wakePoints = slates
                    .SelectMany(s => new[] { s.StartDate.ToDateTime(TimeOnly.MinValue), s.EndDate.ToDateTime(TimeOnly.MaxValue) })
                    .Select(t => new DateTimeOffset(DateTime.SpecifyKind(t, DateTimeKind.Utc)))
                    .Concat(slates.Where(s => s.FirstGameUtc.HasValue).Select(s => s.FirstGameUtc!.Value))
                    .ToList();

                // CfbCurrentSlateService always resolves *something* now (most-recently-completed
                // or soonest-upcoming slate, for UI-default purposes) — so its null-ness alone can
                // no longer be used to gate off-season ESPN polling. IsSeasonActiveAsync is the
                // purpose-built, season-level check for that (see SeasonWindowResolver).
                if (!await currentSlateService.IsSeasonActiveAsync()) return new(_polled.Clear(), wakePoints, ExpectedGames: false);

                wakePoints.AddRange(SeasonWindowResolver.ChangePoints(
                        CfbCurrentSlateService.BuildSlateWindows(slates, await cfbRepo.GetAllWeekConfigsAsync()).Select(sw => sw.Window))
                    .Select(t => new DateTimeOffset(DateTime.SpecifyKind(t, DateTimeKind.Utc))));

                var currentSlate = await currentSlateService.GetCurrentSlateAsync();
                var slate = currentSlate is null ? null : await cfbRepo.GetSlateByIdAsync(currentSlate.Id);
                if (slate is null) return new(_polled.Clear(), wakePoints, ExpectedGames: false);
                // A slate has games once its first kickoff is known (a CFP round's matchups may not be yet).
                return new(_polled.Record(SlateCacheKey(slate.Id), await fetcher.FetchForSlateAsync(slate, isCurrentSlate: true)),
                    wakePoints, ExpectedGames: slate.FirstGameUtc.HasValue);
            }, DateTimeOffset.UtcNow),
            fingerprint: EspnScoresFingerprint.Compute,
            intervalSelector: current => _pollSchedule.NextInterval(current, DateTimeOffset.UtcNow),
            initialDelay: initialDelay);
    }

    public Task<EspnScores?> GetScoresAsync() => Task.FromResult(_cache.Current);

    // Settled/live CFB scores for a SPECIFIC (typically non-current) slate — mirrors
    // EspnCacheService.GetWeekScoresAsync(season, nflWeek) exactly (frizat-d0t unification).
    // Fresh scope for the same reason the periodic-poll fetch above needs one — Scoped
    // dependencies consumed from a Singleton.
    public async Task<EspnScores?> GetSlateScoresAsync(int slateId) {
        var cacheKey = SlateCacheKey(slateId);
        // Fast path: skip the slate/current-slate DB resolution entirely on a cache hit — a
        // settled slate's response never changes, so repeat requests for it shouldn't pay for
        // any of the lookups below just to re-derive a cache key it turns out we already have.
        if (_settledCache.TryGet(cacheKey, out var cached)) return cached;
        // The slate the background poller is tracking (the current one): answer from its latest
        // poll — mirrors EspnCacheService.GetWeekScoresAsync (see PolledItemSnapshot).
        if (_polled.TryGet(cacheKey, out var polled)) return polled;

        using var scope = _scopeFactory.CreateScope();
        var cfbRepo = scope.ServiceProvider.GetRequiredService<ICfbRepository>();
        var currentSlateService = scope.ServiceProvider.GetRequiredService<ICfbCurrentSlateService>();

        var slate = await cfbRepo.GetSlateByIdAsync(slateId);
        if (slate is null) return null;

        // A missing EspnWeekNumber is a control-table seeding bug, not a case to fall back on —
        // mirrors CfbLiveScoreFetcher's identical guard, needed here too since FinalScoresEspnMapper.
        // Build requires a real week number for the DB-first branch below.
        if (!slate.EspnWeekNumber.HasValue) {
            Log.Warning("CfbCacheService: slate {SlateId} has no EspnWeekNumber — skipping", slate.Id);
            return null;
        }

        var currentSlate = await currentSlateService.GetCurrentSlateAsync();
        var isCurrentSlate = currentSlate?.Id == slate.Id;

        return await _settledCache.GetOrBuildAsync(
            cacheKey, isCurrentSlate, slate.EndDate.ToDateTime(TimeOnly.MaxValue),
            buildFromRowsAsync: async () => {
                var rows = (await cfbRepo.GetScoresForSlateAsync(slate.Id)).ToList();
                if (rows.Count == 0) return null;
                var games = rows.Select(FinalScoresEspnMapper.FromCfbScores);
                return FinalScoresEspnMapper.Build(games, slate.Season, slate.EspnWeekNumber.Value, CfbSlateHelpers.IsCfpSlate(slate.ScoringFormat));
            },
            // isCurrentSlate is already resolved above — pass it through instead of letting the
            // fetcher re-derive it via a second ICfbCurrentSlateService.GetCurrentSlateAsync()
            // call (a second full-table DB round trip for the identical fact every live fetch).
            liveFetchAsync: () => _fetcher.FetchForSlateAsync(slate, isCurrentSlate));
    }

    public void InvalidateSlateCache(int slateId) => _settledCache.Invalidate(SlateCacheKey(slateId));

    public ValueTask DisposeAsync() => _cache.DisposeAsync();
}
