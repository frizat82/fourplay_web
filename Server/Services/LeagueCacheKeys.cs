using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;

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

    // One cancellation "generation" per (league, season). Every derived entry carries the token that
    // was current when its read STARTED; invalidation cancels it. So a read already in flight when
    // the settings change stores an already-expired entry instead of re-caching the old value for
    // its full TTL after the Remove below has run.
    private static readonly ConcurrentDictionary<(int LeagueId, long Season), CancellationTokenSource> Generations = new();

    /// <summary>Tie a cache entry to the league-season's settings; call before reading them.</summary>
    public static void Track(ICacheEntry entry, int leagueId, long season) =>
        entry.AddExpirationToken(new CancellationChangeToken(
            Generations.GetOrAdd((leagueId, season), _ => new CancellationTokenSource()).Token));

    /// <summary>
    /// Call after a league's settings for <paramref name="season"/> change. LeagueRepository's
    /// juice writers do, so every writer (endpoints, jobs, league creation) is covered.
    /// </summary>
    public static void InvalidateLeagueSeason(IMemoryCache cache, int leagueId, int season) {
        if (Generations.TryRemove((leagueId, season), out var generation)) generation.Cancel();
        cache.Remove(Juice(leagueId, season));
        cache.Remove(Leaderboard(leagueId, season));
    }
}
