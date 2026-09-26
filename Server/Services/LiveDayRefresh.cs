using FourPlayWebApp.Shared.Helpers;
using FourPlayWebApp.Shared.Models;

namespace FourPlayWebApp.Server.Services;

/// <summary>
/// How the NFL and CFB score pollers refresh a week/slate scoreboard — one implementation for both.
/// While games are live (polled every <see cref="EspnPollCadence.FastPollInterval"/>), only the ESPN
/// day bucket(s) holding those games are fetched and swapped into the scoreboard already held:
/// games on other days are final or not started, so re-fetching them every 30s was wasted ESPN
/// traffic (ESPN only serves single-day queries now, so a full window is one request per day).
/// Everything else — first poll, a new week, slow polls, nothing live — is a full-window fetch,
/// which is also what picks up schedule changes and adds new games.
/// </summary>
public static class LiveDayRefresh {
    /// <summary>
    /// ESPN day buckets of games inside their live window. Both the Eastern and the UTC calendar
    /// date of each kickoff: a late game (8:15pm ET = 00:15 UTC the next day) can sit in either
    /// bucket, and fetching both costs at most one extra request.
    /// </summary>
    public static IReadOnlyCollection<DateOnly> LiveDays(EspnScores scoreboard, DateTimeOffset now) =>
        EspnPollCadence.KickoffTimes(scoreboard)
            .Where(kickoff => kickoff <= now && now <= kickoff + EspnPollCadence.LiveGameDuration)
            .SelectMany(kickoff => new[] {
                DateOnly.FromDateTime(TimeZoneHelpers.ConvertTimeToEt(kickoff)),
                DateOnly.FromDateTime(kickoff.UtcDateTime),
            })
            .Distinct()
            .ToList();

    /// <param name="previous">The scoreboard the last poll produced for this same week/slate, if any.</param>
    /// <param name="fetchDays">Fetches just these ESPN days for the week/slate.</param>
    /// <param name="fetchAll">The full-window fetch.</param>
    public static async Task<EspnScores?> FetchAsync(EspnScores? previous, DateTimeOffset now,
        Func<IReadOnlyCollection<DateOnly>, Task<EspnScores?>> fetchDays, Func<Task<EspnScores?>> fetchAll) {
        if (previous?.Events is not { Length: > 0 } previousEvents) return await fetchAll();
        var liveDays = LiveDays(previous, now);
        if (liveDays.Count == 0) return await fetchAll();

        var fresh = await fetchDays(liveDays);
        if (fresh?.Events is not { Length: > 0 } freshEvents) return await fetchAll();

        // Only refresh games the week already holds — the full-window fetch owns the game list.
        var freshById = freshEvents.GroupBy(e => e.Id).ToDictionary(g => g.Key, g => g.First());
        return GameHelpers.WithEvents(previous,
            [.. previousEvents.Select(e => freshById.GetValueOrDefault(e.Id) ?? e)]);
    }
}
