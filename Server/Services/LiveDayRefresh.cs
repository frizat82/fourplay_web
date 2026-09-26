using FourPlayWebApp.Shared.Helpers;
using FourPlayWebApp.Shared.Models;

namespace FourPlayWebApp.Server.Services;

/// <summary>
/// The live-poll half of how the NFL and CFB score pollers refresh a week/slate scoreboard — one
/// implementation for both (PolledItemSnapshot.RefreshAsync decides when this applies). While games
/// are live, only the ESPN day bucket holding them is fetched and those games are swapped into the
/// scoreboard already held: games on other days are final or not started, so re-fetching them every
/// 30s was wasted ESPN traffic (ESPN only serves single-day queries now, so a full window costs one
/// request per day).
/// </summary>
public static class LiveDayRefresh {
    /// <summary>
    /// The scoreboard with its live games refreshed, or null when nothing is live or ESPN returned
    /// nothing — the caller then does a full-window fetch.
    /// </summary>
    public static async Task<EspnScores?> TryMergeLiveDaysAsync(EspnScores previous, DateTimeOffset now,
        Func<IReadOnlyCollection<DateOnly>, Task<EspnScores?>> fetchDays) {
        if (previous.Events is not { Length: > 0 } previousEvents) return null;
        var live = previousEvents
            .Select(e => (e.Id, Kickoff: e.Competitions.FirstOrDefault()?.Date ?? e.Date))
            .Where(g => AdaptivePollInterval.IsInWindow(g.Kickoff, EspnPollCadence.LiveGameDuration, now))
            .ToList();
        if (live.Count == 0) return null;

        // ESPN buckets games by their Eastern-time date (an 8:15pm ET kickoff is 00:15 UTC the next
        // day but sits in the ET day). If a live game is ever missing from that bucket, fetch its
        // UTC date too rather than let it silently stop updating.
        var etDays = DaysOf(live.Select(g => TimeZoneHelpers.ConvertTimeToEt(g.Kickoff)));
        var fresh = (await fetchDays(etDays))?.Events ?? [];
        var missing = live.Where(g => fresh.All(e => e.Id != g.Id)).ToList();
        if (missing.Count > 0) {
            var utcDays = DaysOf(missing.Select(g => g.Kickoff.UtcDateTime)).Except(etDays).ToList();
            if (utcDays.Count > 0) fresh = [.. fresh, .. (await fetchDays(utcDays))?.Events ?? []];
        }
        if (fresh.Length == 0) return null;

        // Only refresh games the week already holds — the full-window fetch owns the game list.
        var freshById = fresh.GroupBy(e => e.Id).ToDictionary(g => g.Key, g => g.First());
        return GameHelpers.WithEvents(previous, [.. previousEvents.Select(e => freshById.GetValueOrDefault(e.Id) ?? e)]);
    }

    private static List<DateOnly> DaysOf(IEnumerable<DateTime> times) => times.Select(DateOnly.FromDateTime).Distinct().ToList();
}
