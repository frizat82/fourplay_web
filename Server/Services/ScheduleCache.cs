using Microsoft.Extensions.Caching.Memory;

namespace FourPlayWebApp.Server.Services;

/// <summary>
/// In-memory cache for the schedule tables (NflSeasonWeekConfigs, CfbSlates, CfbSeasonWeekConfigs)
/// — shared by LeagueRepository (NFL) and CfbRepository (CFB). They're read on every score poll and
/// most requests ("what week is it?") but change a few times a season; reading them from the DB
/// every time was ~5k full-table reads a day and kept Neon from ever suspending. Each table is
/// cached whole; per-season/by-id reads filter the cached list. Writers going through the
/// repositories evict (<see cref="Invalidate"/>); the TTL bounds how long an out-of-band change
/// (migration data, hand-run SQL) can go unseen — deploys restart the app, which clears it anyway.
/// </summary>
public static class ScheduleCache {
    public const string NflWeekConfigs = "schedule:nfl-week-configs";
    public const string CfbSlates = "schedule:cfb-slates";
    public const string CfbWeekConfigs = "schedule:cfb-week-configs";
    public static readonly TimeSpan Ttl = TimeSpan.FromHours(1);

    /// <summary>
    /// The whole table, cached; each caller gets its own copy of the list so no caller's in-place
    /// sort/filter changes what the next one sees. With no cache (tests), reads straight through.
    /// </summary>
    public static async Task<List<T>> GetAsync<T>(IMemoryCache? cache, string key, Func<Task<List<T>>> load) {
        if (cache is null) return await load();
        var rows = await cache.GetOrCreateAsync(key, entry => {
            CacheGenerations.Track(cache, entry, key);
            entry.AbsoluteExpirationRelativeToNow = Ttl;
            return load();
        });
        return [.. rows!];
    }

    public static void Invalidate(IMemoryCache? cache, string key) {
        if (cache is null) return;
        CacheGenerations.Invalidate(cache, key);
        cache.Remove(key);
    }
}
