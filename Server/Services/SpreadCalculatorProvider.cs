using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Server.Services.Repositories.Interfaces;
using FourPlayWebApp.Shared.Models.Enum;
using Microsoft.Extensions.Caching.Memory;

namespace FourPlayWebApp.Server.Services;

public class SpreadCalculatorProvider(ILeagueRepository repository, IMemoryCache cache) : ISpreadCalculatorProvider {
    private const string _spreadsCacheKey = "spreads_{0}_{1}"; // season_week

    public async Task<ISpreadCalculator> GetForNflWeekAsync(int leagueId, int season, int week) {
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

        // Per (league, season): the mapping is season-specific. LeagueRepository's juice writers evict
        // it on any change (and Track expires a read that was in flight when that happened).
        var juiceMapping = await cache.GetOrCreateAsync(LeagueCacheKeys.Juice(leagueId, season), async entry => {
            LeagueCacheKeys.Track(entry, leagueId, season);
            var result = await repository.GetLeagueJuiceMappingAsync(leagueId, season);
            if (result != null) {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
                return result;
            }
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(1);
            return new LeagueJuiceMapping();
        });

        return new SpreadCalculator(odds ?? [], JuiceTiers.For(LeagueType.Nfl, week, juiceMapping!));
    }
}
