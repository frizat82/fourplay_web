using System.Text.Json;
using Serilog;
using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Shared.Helpers;
using FourPlayWebApp.Shared.Helpers.Extensions;
using FourPlayWebApp.Shared.Models;
using ILogger=Microsoft.Extensions.Logging.ILogger;

namespace FourPlayWebApp.Server.Services;

public class EspnApiService(HttpClient httpClient, ILogger<EspnApiService> logger)
    : IEspnApiService {
    private const string _scoreboardEndpoint = "/apis/site/v2/sports/football/nfl/scoreboard";

    // frizat-11t (NFL mirror): scoped to the caller's own control-table window, not week=N — see
    // IEspnApiService's doc comment for why. frizat-4gn: ESPN broke the dates=START-END range
    // query (HTTP 400 for every range, every date, every ESPN sport we tested — not transient),
    // so this now tries the (cheap, single-call) range query first, and falls back to fetching
    // one day at a time via EspnDateRangeFetcher only if that fails. If ESPN ever fixes the range
    // endpoint again, the range path resumes taking over with zero further changes needed.
    public async Task<EspnScores?> GetScoresByDateRangeAsync(DateOnly startDate, DateOnly endDate, bool postSeason = false) {
        var rangeResult = await TryGetScoresByRangeAsync(startDate, endDate, postSeason);
        if (rangeResult is not null) return rangeResult;

        logger.LogWarning("ESPN Date Range Not Working falling back to Days");
        return await EspnDateRangeFetcher.FetchRangeAsync(startDate, endDate, date => GetScoresForDayAsync(date, postSeason));
    }

    // Deliberately no Error-level logging here — right now every call takes this path and falls
    // back (ESPN's range endpoint is confirmed broken), so logging that as an error would just be
    // permanent noise. GetScoresForSingleDayAsync's own error logging below covers the case that
    // actually needs attention: the fallback itself failing.
    private async Task<EspnScores?> TryGetScoresByRangeAsync(DateOnly startDate, DateOnly endDate, bool postSeason) {
        try {
            var dates = $"{startDate:yyyyMMdd}-{endDate:yyyyMMdd}";
            var response = await httpClient.GetAsync(
                $"{_scoreboardEndpoint}?dates={dates}&seasontype={(postSeason ? 3 : 2)}&limit=100");
            if (!response.IsSuccessStatusCode) return null;
            return await ParseResponseAsync(response);
        }
        catch (HttpRequestException) {
            return null;
        }
    }

    // seasontype still needs to be passed explicitly (2=regular, 3=postseason) since ESPN's date
    // filter alone doesn't disambiguate a rescheduled/rare doubleheader week that straddles both
    // season types.
    public async Task<EspnScores?> GetScoresForDayAsync(DateOnly date, bool postSeason = false) {
        try {
            var response = await httpClient.GetAsync(
                $"{_scoreboardEndpoint}?dates={date:yyyyMMdd}&seasontype={(postSeason ? 3 : 2)}&limit=100");
            response.EnsureSuccessStatusCode();
            return await ParseResponseAsync(response);
        }
        catch (HttpRequestException e) {
            logger.LogError("HTTP Request error: {EMessage}", e.Message);
            return null;
        }
    }

    private async Task<EspnScores?> ParseResponseAsync(HttpResponseMessage response) {
        var responseString = await response.Content.ReadAsStringAsync();
        // Fix some strange team abbreviations from ESPN that don't match standard ones
        foreach (var map in NflTeamMappingHelpers.NflTeamAbbrMapping.Where(map =>
                     responseString.Contains($"\"{map.Key}\""))) {
            responseString = responseString.Replace($"\"{map.Key}\"", $"\"{map.Value}\"");
        }

        var deserializedObject = JsonSerializer.Deserialize<EspnScores>(responseString, EspnApiServiceJsonConverter.Settings);
        return FixEspnProbBowlWeek(deserializedObject);
    }

    private EspnScores? FixEspnProbBowlWeek(EspnScores? scores) {
        if (scores == null || !scores.Events.Any())
            return scores;

        // Remove teams with Abbr "NFC" or "AFC" — the old Pro Bowl exhibition's placeholder
        // competitors. Unconditional, not season-gated: this is data hygiene (an event that
        // shouldn't be treated as a real game), not the week-number quirk-correction below.
        foreach (var scoreEvent in scores.Events) {
            scoreEvent.Competitions = scoreEvent.Competitions
                .Where(c => !c.Competitors.Any(team => team.Team.Abbreviation == "NFC" || team.Team.Abbreviation == "AFC"))
                .ToArray();
        }

        // Move back Super Bowl to a proper week — gated by SEASON, matching
        // GameHelpers.GetWeekFromEspnWeek's identical condition exactly via the shared
        // NeedsProBowlWeekFix predicate (frizat-4k9: this used to be an independent,
        // unconditional copy of the same quirk-correction, which would have silently
        // mis-relabeled a real 2026+ week 5 game as week 4 instead of failing loudly).
        if (scores.NeedsProBowlWeekFix()) {
            scores.Week.Number = 4;
        }
        // Update SeasonType and Week number
        foreach (var scoreEvent in scores.Events) {
            if (scoreEvent.NeedsProBowlWeekFix()) {
                scoreEvent.Week.Number = 4;
            }
        }

        return scores;
    }
}
