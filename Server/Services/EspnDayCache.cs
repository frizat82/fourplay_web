using FourPlayWebApp.Shared.Helpers;
using FourPlayWebApp.Shared.Models;
using Microsoft.Extensions.Caching.Memory;

namespace FourPlayWebApp.Server.Services;

/// <summary>
/// Caches each ESPN single-day scoreboard response (and the CFP bucket) for as long as it can't
/// change — shared by the NFL and CFB API services. ESPN only answers single-day queries now, so a
/// week/slate window is one request per day, and the pollers re-fetch the window every 30s during
/// games and every 5 min otherwise. With this cache those polls only reach ESPN for days that can
/// actually have changed. The ESPN-reading jobs opt out with <see cref="Fresh"/>.
/// <para>Cached responses are shared by every reader: treat them as read-only (nothing mutates
/// them today — the fetchers build new scoreboards via GameHelpers.WithEvents).</para>
/// </summary>
public sealed class EspnDayCache(IMemoryCache cache, TimeProvider time) {
    /// <summary>A day with a game on: always refetched by the next fast poll.</summary>
    public static readonly TimeSpan LiveTtl = TimeSpan.FromSeconds(20);
    /// <summary>A day whose last game only just finished: re-checked for post-final score corrections.</summary>
    public static readonly TimeSpan RecentlyFinishedTtl = TimeSpan.FromMinutes(15);
    /// <summary>A day with no games — or a response that came back empty; re-checked every few slow polls.</summary>
    public static readonly TimeSpan EmptyTtl = TimeSpan.FromMinutes(15);
    /// <summary>A day that finished long ago, or upcoming games far off.</summary>
    public static readonly TimeSpan SettledTtl = TimeSpan.FromHours(6);
    private static readonly TimeSpan RecentlyFinishedWindow = TimeSpan.FromHours(12);

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
        // Postponed/canceled games stay "scheduled" at ESPN; hours past kickoff they're done, not pending.
        var pending = games.Where(g => !GameHelpers.IsGameOver(g) && now <= g.Date + EspnPollCadence.LiveGameDuration).ToList();
        if (pending.Count == 0)
            return now - games.Max(g => g.Date) < RecentlyFinishedWindow ? RecentlyFinishedTtl : SettledTtl;
        // In progress, or past its kickoff time but not started yet (a delayed start).
        if (pending.Any(g => GameHelpers.IsGameStarted(g) || g.Date <= now)) return LiveTtl;
        var untilKickoff = pending.Min(g => g.Date) - now;
        return untilKickoff < SettledTtl ? untilKickoff : SettledTtl;
    }
}

/// <summary>The API services' optional cache: straight to ESPN when there's none (tests).</summary>
public static class EspnDayCacheExtensions {
    public static Task<EspnScores?> GetOrFetchDayAsync(this EspnDayCache? cache, string key, Func<Task<EspnScores?>> fetch) =>
        cache is null ? fetch() : cache.GetOrFetchAsync(key, fetch);
}
