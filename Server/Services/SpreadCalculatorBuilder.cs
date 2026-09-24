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
        // The calculator itself isn't cached — building one is trivial next to the two lookups it
        // needs, and caching it (keyed without season, and never evicted on a settings change) is
        // exactly what let a stale tease outlive a commissioner's edit or leak across seasons.
        var spreadsKey = string.Format(_spreadsCacheKey, season, week);
        var odds = await cache.GetOrCreateAsync(spreadsKey, async entry => {
            var result = await repository.GetNflSpreadsAsync(season, week);
            if (result != null && result.Count != 0) {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
                return result;
            }
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(1);
            return [];
        });

        // Per (league, season): the mapping is season-specific. LeagueController evicts this key
        // whenever a commissioner saves or rolls forward that season's settings.
        var juiceMapping = await cache.GetOrCreateAsync(LeagueCacheKeys.Juice(leagueId, season), async entry => {
            var result = await repository.GetLeagueJuiceMappingAsync(leagueId, season);
            if (result != null) {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
                return result;
            }
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(1);
            return new LeagueJuiceMapping();
        });

        return new SpreadCalculator(odds ?? [], juiceMapping!, week);
    }
}
