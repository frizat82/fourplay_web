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

    // Date-range query (frizat-11t), scoped to the control table's own WeekStartDate/WeekEndDate —
    // NOT the old groups=80 date-range approach the previous week-based query's comment referenced
    // (that one silently ignored ESPN's Top-25 group filter). ESPN's own week=N bucketing doesn't
    // respect our slate boundaries — e.g. a team's early/"week 0" opener can land in the same
    // week=N response as their real week-N game, so "one game per team per slate" wasn't actually
    // true under the week-based query. dates=yyyyMMdd-yyyyMMdd scoped to our own slate window makes
    // that invariant hold for real, same reasoning as GetCfpGamesAsync's downstream date filter.
    // Regular season/conf-champs only — no isPostSeason branch, since CFP already has its own
    // correct, dedicated mechanism below (week=999 + downstream date filter) that this doesn't
    // need to duplicate or unify with; adding a postseason branch here that no caller would ever
    // exercise would just be dead code implying an equivalence that doesn't exist.
    public async Task<EspnScores?> GetScoresByDateRangeAsync(DateOnly startDate, DateOnly endDate) {
        var dates = $"{startDate:yyyyMMdd}-{endDate:yyyyMMdd}";
        var url = $"/apis/site/v2/sports/football/college-football/scoreboard?dates={dates}&seasontype=2&limit=100";
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
