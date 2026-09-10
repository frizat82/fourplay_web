using FourPlayWebApp.Server.Data;
using FourPlayWebApp.Server.Jobs;
using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Shared.Helpers;
using FourPlayWebApp.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace FourPlayWebApp.Server.Services;

/// <summary>
/// Demo-only implementation of ICfbCacheService that serves frozen ESPN data (including real
/// down/distance/field-position) from sample_espn_cfb.json — mirrors DemoEspnCacheService's role
/// for NFL. Only registered when DEMO_MODE=true (the Railway "development" environment included —
/// DEMO_MODE is an app-config flag, not tied to any particular environment name). Real (non-demo)
/// prod never registers this class at all.
/// </summary>
public class DemoCfbCacheService : ICfbCacheService
{
    private readonly EspnScores? _scores;
    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;
    private readonly TimeProvider _timeProvider;

    public DemoCfbCacheService(IDbContextFactory<ApplicationDbContext> dbContextFactory,
        [FromKeyedServices(CurrentWeekClock.Key)] TimeProvider timeProvider)
    {
        _dbContextFactory = dbContextFactory;
        _timeProvider = timeProvider;
        _scores = DemoFixtureLoader.Load("sample_espn_cfb.json");
    }

    public event Action? ScoresChanged; // never fired — demo data is static

    public Task<EspnScores?> GetScoresAsync() => Task.FromResult(_scores);

    // Backs the "browse a non-current slate" path on the Scores page (GetCfbScoresForSlate in
    // EspnController) — mirrors DemoEspnCacheService.GetWeekScoresAsync's NFL pattern exactly,
    // adapted to CFB's slate model (frizat-d0t). Resolves "current slate" inline via
    // CfbCurrentSlateService.BuildSlateWindows (the SAME shared helper that class itself uses,
    // including its fail-loud throw on a slate with no matching config — an earlier version of
    // this method inlined its own copy that silently fell back to DateTime.MaxValue instead,
    // which could have let demo's "current slate" resolution silently disagree with prod's for
    // the exact data-integrity case that throw exists to catch) rather than
    // constructor-injecting that Scoped service into this Singleton — same captive-dependency
    // reasoning DemoEspnCacheService's own comment explains, and must use the same keyed clock
    // CfbCurrentSlateService resolves "current" against (frizat-tf2) so the two don't disagree.
    public async Task<EspnScores?> GetSlateScoresAsync(int slateId)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();

        var slate = await db.CfbSlates.FirstOrDefaultAsync(s => s.Id == slateId);
        if (slate is null || !slate.EspnWeekNumber.HasValue) return null;

        var allSlates = await db.CfbSlates.ToListAsync();
        var configs = await db.CfbSeasonWeekConfigs.ToListAsync();
        var slateWindows = CfbCurrentSlateService.BuildSlateWindows(allSlates, configs);
        var resolvedCurrent = SeasonWindowResolver.ResolveCurrentWeek(slateWindows.Select(sw => sw.Window), _timeProvider.GetUtcNow().UtcDateTime);
        var isCurrentSlate = resolvedCurrent is not null
            && resolvedCurrent.Value.Season == slate.Season
            && resolvedCurrent.Value.Start == slate.StartDate.ToDateTime(TimeOnly.MinValue)
            && resolvedCurrent.Value.End == slate.EndDate.ToDateTime(TimeOnly.MaxValue);

        if (isCurrentSlate) return _scores;

        var rows = await db.CfbScores.Where(s => s.CfbSlateId == slateId).ToListAsync();
        if (rows.Count == 0) return null;

        var games = rows.Select(FinalScoresEspnMapper.FromCfbScores);
        return FinalScoresEspnMapper.Build(games, slate.Season, slate.EspnWeekNumber.Value, CfbSlateHelpers.IsCfpSlate(slate.ScoringFormat));
    }

    // No-op: demo data is a frozen fixture, never refreshed by a live CfbScoresJob upsert.
    public void InvalidateSlateCache(int slateId) { }
}
