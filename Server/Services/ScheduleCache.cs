using System.Reflection;
using FourPlayWebApp.Server.Data;
using FourPlayWebApp.Server.Models.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace FourPlayWebApp.Server.Services;

/// <summary>
/// In-memory cache for the schedule tables (NflSeasonWeekConfigs, CfbSlates, CfbSeasonWeekConfigs)
/// — shared by LeagueRepository (NFL) and CfbRepository (CFB). They're read on every score poll and
/// most requests ("what week is it?") but change a few times a season; reading them from the DB
/// every time was ~5k full-table reads a day and kept Neon from ever suspending. Each table is
/// cached whole; per-season/by-id reads filter the cached rows and copy only what they return.
/// Any save that touches one of these tables evicts it (ScheduleCacheInterceptor), whoever makes it.
/// The TTL bounds how long an out-of-band change (hand-run SQL) can go unseen — deploys restart the
/// app, which clears it anyway.
/// </summary>
public static class ScheduleCache {
    public const string NflWeekConfigs = "schedule:nfl-week-configs";
    public const string CfbSlates = "schedule:cfb-slates";
    public const string CfbWeekConfigs = "schedule:cfb-week-configs";
    public static readonly TimeSpan Ttl = TimeSpan.FromHours(1);

    /// <summary>
    /// The cached rows — shared, so never hand them out: return <see cref="Copies{T}"/> /
    /// <see cref="Copy{T}"/> of what the caller asked for. With no cache (tests), reads straight through.
    /// </summary>
    public static async Task<IReadOnlyList<T>> RowsAsync<T>(IMemoryCache? cache, string key, Func<Task<List<T>>> load) {
        if (cache is null) return await load();
        return (await cache.GetOrCreateAsync(key, entry => {
            CacheGenerations.Track(cache, entry, key);
            entry.AbsoluteExpirationRelativeToNow = Ttl;
            return load();
        }))!;
    }

    // The three tables, each loaded whole (ordered) — shared by LeagueRepository and CfbRepository
    // so every reader of a table goes through the one cached copy.
    public static Task<IReadOnlyList<NflSeasonWeekConfig>> NflWeekConfigRowsAsync(IMemoryCache? cache, IDbContextFactory<ApplicationDbContext> factory) =>
        RowsAsync(cache, NflWeekConfigs, async () => {
            await using var db = await factory.CreateDbContextAsync();
            return await db.NflSeasonWeekConfigs.AsNoTracking().OrderBy(c => c.Season).ThenBy(c => c.WeekId).ToListAsync();
        });

    public static Task<IReadOnlyList<CfbSlates>> CfbSlateRowsAsync(IMemoryCache? cache, IDbContextFactory<ApplicationDbContext> factory) =>
        RowsAsync(cache, CfbSlates, async () => {
            await using var db = await factory.CreateDbContextAsync();
            return await db.CfbSlates.AsNoTracking().OrderBy(s => s.Season).ThenBy(s => s.SlateNumber).ToListAsync();
        });

    public static Task<IReadOnlyList<CfbSeasonWeekConfig>> CfbWeekConfigRowsAsync(IMemoryCache? cache, IDbContextFactory<ApplicationDbContext> factory) =>
        RowsAsync(cache, CfbWeekConfigs, async () => {
            await using var db = await factory.CreateDbContextAsync();
            return await db.CfbSeasonWeekConfigs.AsNoTracking().OrderBy(c => c.Season).ThenBy(c => c.EspnWeekNumber).ToListAsync();
        });

    // Shallow copies: these are flat rows (no navigation data loaded), so a caller changing a field
    // or reordering its list can't alter what every other request and poller sees.
    private static readonly Func<object, object> MemberwiseClone = typeof(object)
        .GetMethod(nameof(MemberwiseClone), BindingFlags.Instance | BindingFlags.NonPublic)!
        .CreateDelegate<Func<object, object>>();

    public static List<T> Copies<T>(IEnumerable<T> rows) where T : class => [.. rows.Select(r => (T)MemberwiseClone(r))];

    public static T? Copy<T>(T? row) where T : class => row is null ? null : (T)MemberwiseClone(row);

    public static void Invalidate(IMemoryCache? cache, string key) {
        if (cache is null) return;
        CacheGenerations.Invalidate(cache, key);
        cache.Remove(key);
    }
}
