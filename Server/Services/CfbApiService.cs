using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Shared.Models;
using System.Text.Json;

namespace FourPlayWebApp.Server.Services;

public class CfbApiService(HttpClient httpClient) : ICfbApiService {
    private static readonly JsonSerializerOptions _opts = new() { PropertyNameCaseInsensitive = true };

    public async Task<EspnScores?> GetScoresByDateAsync(DateOnly date) {
        var dateStr = date.ToString("yyyyMMdd");
        var url = $"/apis/site/v2/sports/football/college-football/scoreboard?dates={dateStr}&limit=25";
        var response = await httpClient.GetAsync(url);
        if (!response.IsSuccessStatusCode) return null;
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<EspnScores>(json, _opts);
    }

    // Regular season + conference championships — groups=80 filters to Top 25 matchups only
    public async Task<EspnScores?> GetTop25ByDateAsync(DateOnly date) {
        var dateStr = date.ToString("yyyyMMdd");
        var url = $"/apis/site/v2/sports/football/college-football/scoreboard?dates={dateStr}&groups=80&limit=50";
        var response = await httpClient.GetAsync(url);
        if (!response.IsSuccessStatusCode) return null;
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<EspnScores>(json, _opts);
    }

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
}
