using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Shared.Models;
using System.Text.Json;

namespace FourPlayWebApp.Server.Services;

public class CfbApiService(HttpClient httpClient) : ICfbApiService {
    // Must deserialize with EspnApiServiceJsonConverter.Settings — its converters handle ESPN's
    // wire values (e.g. "away" for HomeAway, "STATUS_IN_PROGRESS" for TypeName) that don't match
    // PascalCase enum member names under default System.Text.Json enum parsing (see
    // EspnContractTests + EspnJsonConverterTests, frizat-703.5).
    private static readonly JsonSerializerOptions _opts = EspnApiServiceJsonConverter.Settings;

    // Week-based query: seasontype=2 for regular/conf-champs, seasontype=3 for CFP rounds.
    // Fixes the broken groups=80 date-range approach — one call per slate, no date iteration.
    public async Task<EspnScores?> GetScoresByWeekAsync(int week, bool isPostSeason) {
        var seasonType = isPostSeason ? 3 : 2;
        var limit = isPostSeason ? 50 : 100;
        var url = $"/apis/site/v2/sports/football/college-football/scoreboard?week={week}&seasontype={seasonType}&limit={limit}";
        var response = await httpClient.GetAsync(url);
        if (!response.IsSuccessStatusCode) return null;
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<EspnScores>(json, _opts);
    }

    // ESPN week=999 is the explicit CFP-only bucket — returns all CFP playoff games regardless of round.
    // Use date filtering downstream to isolate the specific round.
    public async Task<EspnScores?> GetCfpGamesAsync() {
        var url = "/apis/site/v2/sports/football/college-football/scoreboard?week=999&seasontype=3&limit=100";
        var response = await httpClient.GetAsync(url);
        if (!response.IsSuccessStatusCode) return null;
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<EspnScores>(json, _opts);
    }
}
