using Microsoft.Extensions.Caching.Memory;

namespace FourPlayWebApp.Server.Services;

/// <summary>
/// IMemoryCache keys for data derived from a league's per-season settings (LeagueJuiceMapping), in
/// one place so the writers that change those settings can evict exactly what the readers cached.
/// </summary>
public static class LeagueCacheKeys {
    /// <summary>A league's juice mapping for one season (SpreadCalculatorProvider).</summary>
    public static string Juice(int leagueId, int season) => $"juice_{leagueId}_{season}";

    /// <summary>A league's built leaderboard for one season (LeaderboardController).</summary>
    public static string Leaderboard(int leagueId, long season) => $"{leagueId}-{season}";

    /// <summary>Tie a cache entry to the league-season's settings; call before reading them.</summary>
    public static void Track(IMemoryCache cache, ICacheEntry entry, int leagueId, long season) =>
        CacheGenerations.Track(cache, entry, (leagueId, season));

    /// <summary>
    /// Call after a league's settings for <paramref name="season"/> change. LeagueRepository's
    /// juice writers do, so every writer (endpoints, jobs, league creation) is covered.
    /// </summary>
    public static void InvalidateLeagueSeason(IMemoryCache cache, int leagueId, int season) {
        CacheGenerations.Invalidate(cache, (leagueId, (long)season));
        cache.Remove(Juice(leagueId, season));
        cache.Remove(Leaderboard(leagueId, season));
    }
}
