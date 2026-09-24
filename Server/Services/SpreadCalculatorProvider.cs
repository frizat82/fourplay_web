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
        var odds = await CacheIfFound(string.Format(_spreadsCacheKey, season, week), _ => { },
            async () => { var rows = await repository.GetNflSpreadsAsync(season, week); return rows is { Count: > 0 } ? rows : null; }, []);

        // Per (league, season): the mapping is season-specific. LeagueRepository's juice writers evict
        // it on any change (and Track expires a read that was in flight when that happened).
        var juiceMapping = await CacheIfFound(LeagueCacheKeys.Juice(leagueId, season),
            entry => LeagueCacheKeys.Track(cache, entry, leagueId, season),
            () => repository.GetLeagueJuiceMappingAsync(leagueId, season), new LeagueJuiceMapping());

        return new SpreadCalculator(odds, JuiceTiers.For(LeagueType.Nfl, week, juiceMapping));
    }

    // A hit is cached for an hour; a miss only for a second, so data posted moments later shows up.
    private async Task<T> CacheIfFound<T>(string key, Action<ICacheEntry> track, Func<Task<T?>> load, T missing) where T : class =>
        (await cache.GetOrCreateAsync(key, async entry => {
            track(entry);
            var found = await load();
            entry.AbsoluteExpirationRelativeToNow = found is null ? TimeSpan.FromSeconds(1) : TimeSpan.FromHours(1);
            return found ?? missing;
        }))!;
}
