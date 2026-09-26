using FourPlayWebApp.Shared.Helpers;
using FourPlayWebApp.Shared.Models;
using Microsoft.Extensions.Caching.Memory;

namespace FourPlayWebApp.Server.Services;

/// <summary>
/// Caches each ESPN single-day scoreboard response for as long as that day can't change — shared by
/// the NFL and CFB API services. ESPN only answers single-day queries now, so a week/slate window is
/// one request per day, and the pollers re-fetch the window every 30s during games and every 5 min
/// otherwise (the scores jobs fetch it too). With this cache, those window fetches only reach ESPN
/// for days that can actually have changed: a day with a game on (every fast poll), a day whose
/// first kickoff has arrived, and occasionally everything else.
/// </summary>
public sealed class EspnDayCache(IMemoryCache cache, TimeProvider time) {
    /// <summary>A day with a game on: always refetched by the next fast poll.</summary>
    public static readonly TimeSpan LiveTtl = TimeSpan.FromSeconds(20);
    /// <summary>A finished day, or upcoming games far off. Bounded so late score corrections or schedule changes still land.</summary>
    public static readonly TimeSpan SettledTtl = TimeSpan.FromHours(6);
    private static readonly TimeSpan EmptyTtl = TimeSpan.FromHours(1);

    public async Task<EspnScores?> GetOrFetchAsync(string key, Func<Task<EspnScores?>> fetch) {
        if (cache.TryGetValue(key, out EspnScores? cached)) return cached;
        var day = await fetch();
        // A failed fetch isn't cached: the next poll retries.
        if (day is not null) cache.Set(key, day, TtlFor(day, time.GetUtcNow()));
        return day;
    }

    public static TimeSpan TtlFor(EspnScores day, DateTimeOffset now) {
        var games = day.Events?.SelectMany(e => e.Competitions).ToList() ?? [];
        if (games.Count == 0) return EmptyTtl;
        var unfinished = games.Where(g => !GameHelpers.IsGameOver(g)).ToList();
        if (unfinished.Count == 0) return SettledTtl;
        // In progress, or past its kickoff time but not started yet (a delayed start).
        if (unfinished.Any(g => GameHelpers.IsGameStarted(g) || g.Date <= now)) return LiveTtl;
        var untilKickoff = unfinished.Min(g => g.Date) - now;
        return untilKickoff < SettledTtl ? untilKickoff : SettledTtl;
    }
}
