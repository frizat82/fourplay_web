using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Server.Services.Repositories.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace FourPlayWebApp.Server.Services;

public class SpreadCalculatorBuilder(ILeagueRepository repository, IMemoryCache cache) : ISpreadCalculatorBuilder {
    private int _leagueId;
    private int _week;
    private int _season;

    // Cache key templates
    private const string _spreadsCacheKey = "spreads_{0}_{1}"; // season_week
    private const string _juiceCacheKey = "juice_{0}"; // leagueId
    private const string _calculatorCacheKey = "calculator_{0}_{1}_{2}"; // leagueId_season_week

    public ISpreadCalculatorBuilder WithLeagueId(int leagueId)
    {
        _leagueId = leagueId;
        return this;
    }

    public ISpreadCalculatorBuilder WithWeek(int week)
    {
        _week = week;
        return this;
    }

    public ISpreadCalculatorBuilder WithSeason(int season)
    {
        _season = season;
        return this;
    }

    public async Task<ISpreadCalculator> BuildAsync() {
        // Snapshot instance fields immediately to avoid cross-contamination if the
        // builder is (incorrectly) shared across concurrent callers.
        var leagueId = _leagueId;
        var week     = _week;
        var season   = _season;

        if (leagueId == 0 || week == 0 || season == 0)
            throw new ArgumentException("League ID, week, and season must be set.");
        var cacheKey = string.Format(_calculatorCacheKey, leagueId, season, week);

        return await cache.GetOrCreateAsync(cacheKey, async entry => {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);

            // Load spreads from repository
            var spreadsKey = string.Format(_spreadsCacheKey, season, week);
            var odds = await cache.GetOrCreateAsync(spreadsKey, async innerEntry => {
                var result = await repository.GetNflSpreadsAsync(season, week);
                if (result != null && result.Count != 0) {
                    innerEntry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
                    return result;
                }
                // Skip caching if empty
                innerEntry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(1);
                return [];
            });

            // Load juice mapping from repository
            var juiceKey = string.Format(_juiceCacheKey, leagueId);
            var juiceMapping = await cache.GetOrCreateAsync(juiceKey, async innerEntry => {
                var result = await repository.GetLeagueJuiceMappingAsync(leagueId, season);
                if (result != null) {
                    innerEntry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
                    return result;
                }
                // Skip caching if null
                innerEntry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(1);
                return new LeagueJuiceMapping();
            });

            if (odds is null || odds.Count == 0) // no response
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(1);
            return new SpreadCalculator(odds, juiceMapping, week);
        });

    }
}
