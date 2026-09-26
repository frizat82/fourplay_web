using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Shared.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace FourPlayWebApp.Server.Services;

// dayCache: optional so tests that don't care can omit it (every day then goes to the HttpClient).
public class CfbApiService(HttpClient httpClient, ILogger<CfbApiService> logger, EspnDayCache? dayCache = null) : ICfbApiService {
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
    //
    // frizat-4gn: ESPN answers only single-day dates= queries (a dates=START-END range is HTTP 400),
    // so the window is fetched one day at a time — each day through EspnDayCache, so only days that
    // can still change reach ESPN.
    public Task<EspnScores?> GetScoresByDateRangeAsync(DateOnly startDate, DateOnly endDate) =>
        EspnDateRangeFetcher.FetchRangeAsync(startDate, endDate, GetScoresForSingleDayAsync);

    private Task<EspnScores?> GetScoresForSingleDayAsync(DateOnly date) =>
        GetCachedAsync($"/apis/site/v2/sports/football/college-football/scoreboard?dates={date:yyyyMMdd}&seasontype=2&limit=100");

    // ESPN week=999 is the explicit CFP-only bucket — returns all CFP playoff games regardless of
    // round. Use date filtering downstream to isolate the specific round. Not part of frizat-4gn's
    // range-query breakage (week=999 still works), but given the same logging parity fix while
    // touching this class.
    public Task<EspnScores?> GetCfpGamesAsync() => GetCachedAsync("/apis/site/v2/sports/football/college-football/scoreboard?week=999&seasontype=3&limit=100");

    // Same EspnDayCache rules whether the response is one day or the CFP bucket: cached until a
    // game in it can change.
    private Task<EspnScores?> GetCachedAsync(string url) => dayCache.GetOrFetchDayAsync(url, () => FetchAndParseAsync(url));

    private async Task<EspnScores?> FetchAndParseAsync(string url) {
        try {
            var response = await httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode) {
                logger.LogError("CfbApiService: Error {ReasonPhrase} fetching {Url}", response.ReasonPhrase, url);
                return null;
            }
            return await ParseAsync(response);
        } catch (HttpRequestException e) {
            logger.LogError(e, "CfbApiService: HTTP request error fetching {Url}", url);
            return null;
        }
    }

    private static async Task<EspnScores?> ParseAsync(HttpResponseMessage response) {
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<EspnScores>(json, _opts);
    }
}
