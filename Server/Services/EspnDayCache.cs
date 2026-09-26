using FourPlayWebApp.Shared.Helpers;
using FourPlayWebApp.Shared.Models;
using Microsoft.Extensions.Caching.Memory;

namespace FourPlayWebApp.Server.Services;

/// <summary>
/// Caches each ESPN single-day scoreboard response (and the CFP bucket) for as long as it can't
/// change — shared by the NFL and CFB API services. ESPN only answers single-day queries now, so a
/// week/slate window is one request per day, re-fetched by every poll (every 15s during games —
/// see ScorePollSchedule) and by requests that fall back to their own fetch between polls. With
/// this cache those only reach ESPN for days that can actually have changed. The ESPN-reading jobs
/// opt out with <see cref="Fresh"/>.
/// <para>Cached responses are shared by every reader: treat them as read-only (nothing mutates
/// them today — the fetchers build new scoreboards via GameHelpers.WithEvents).</para>
/// </summary>
public sealed class EspnDayCache(IMemoryCache cache, TimeProvider time) {
    /// <summary>A day with a game on: always refetched by the next fast poll.</summary>
    public static readonly TimeSpan LiveTtl = TimeSpan.FromSeconds(10);
    /// <summary>A day with no games — or a response that came back empty; re-checked every 15 min when read.</summary>
    public static readonly TimeSpan EmptyTtl = TimeSpan.FromMinutes(15);
    /// <summary>A day whose games are all final (the scores jobs read fresh), or upcoming games far off.</summary>
    public static readonly TimeSpan SettledTtl = TimeSpan.FromHours(6);

    private static readonly AsyncLocal<bool> Bypass = new();

    /// <summary>
    /// Within this scope every day is fetched from ESPN (and the cache refreshed with it). For the
    /// jobs that persist what they read — scores, spreads, rankings — which run a few times a week
    /// and must not act on a cached snapshot.
    /// </summary>
    public static IDisposable Fresh() {
        var previous = Bypass.Value;
        Bypass.Value = true;
        return new Restore(previous);
    }

    private sealed class Restore(bool previous) : IDisposable {
        public void Dispose() => Bypass.Value = previous;
    }

    public async Task<EspnScores?> GetOrFetchAsync(string key, Func<Task<EspnScores?>> fetch) {
        if (!Bypass.Value && cache.TryGetValue(key, out EspnScores? cached)) return cached;
        var day = await fetch();
        // A failed fetch or a malformed ({}-style, no events array) response isn't cached: the next poll retries.
        if (day?.Events is not null) cache.Set(key, day, TtlFor(day, time.GetUtcNow()));
        return day;
    }

    public static TimeSpan TtlFor(EspnScores day, DateTimeOffset now) {
        var games = day.Events?.SelectMany(e => e.Competitions).ToList() ?? [];
        if (games.Count == 0) return EmptyTtl;
        if (games.Any(g => EspnPollCadence.IsLive(g, now))) return LiveTtl;
        var upcoming = games.Where(g => !GameHelpers.IsGameOver(g) && g.Date > now).ToList();
        if (upcoming.Count == 0) return SettledTtl; // all final, or postponed/canceled (ESPN keeps those "scheduled")
        var untilKickoff = upcoming.Min(g => g.Date) - now;
        return untilKickoff < SettledTtl ? untilKickoff : SettledTtl;
    }
}

/// <summary>The API services' optional cache: straight to ESPN when there's none (tests).</summary>
public static class EspnDayCacheExtensions {
    public static Task<EspnScores?> GetOrFetchDayAsync(this EspnDayCache? cache, string key, Func<Task<EspnScores?>> fetch) =>
        cache is null ? fetch() : cache.GetOrFetchAsync(key, fetch);
}
