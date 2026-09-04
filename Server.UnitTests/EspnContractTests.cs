using FourPlayWebApp.Server.Services;
using FourPlayWebApp.Shared.Helpers.Extensions;
using Microsoft.Extensions.Logging;

namespace FourPlayWebApp.Server.UnitTests;

/// <summary>
/// frizat-703.5: contract tests against the REAL ESPN + odds APIs (no mocks, real network).
/// Proves our DTOs/parsers still match ESPN's actual wire shape, not just our frozen fixtures.
/// Excluded from the default `dotnet test` run via the "Contract" trait — see ci.yml/dotnet.yml
/// (--filter Category!=Contract) and .github/workflows/espn-contract.yml (weekly + on-demand).
///
/// Consolidates two pre-existing untagged live-API tests (EspnApiServiceIntegrationTests,
/// EspnCoreOddsServiceLiveApiTests) that were previously running in every default `dotnet test`
/// with a hardcoded 2024 season and a pinned event ID — both removed here in favor of
/// data-tolerant assertions that don't rot as ESPN's historical data ages.
/// </summary>
[Trait("Category", "Contract")]
public class EspnContractTests
{
    // NFL's fixed 32-team universe — ground truth for the zero-unmapped-abbreviation check.
    private static readonly HashSet<string> KnownNflAbbreviations = new(StringComparer.OrdinalIgnoreCase)
    {
        "ARI", "ATL", "BAL", "BUF", "CAR", "CHI", "CIN", "CLE", "DAL", "DEN", "DET", "GB",
        "HOU", "IND", "JAC", "KC", "LAC", "LAR", "LV", "MIA", "MIN", "NE", "NO", "NYG",
        "NYJ", "PHI", "PIT", "SEA", "SF", "TB", "TEN", "WAS",
    };

    // site.api.espn.com started 403-ing requests with no User-Agent (or a normal branded one) as
    // of 2026-08-05 — see the identical comment/fix in Program.cs's AddHttpClient registrations.
    // These tests build their own HttpClient rather than going through DI, so they need the same
    // header or they get 403'd while production (which does have it) doesn't.
    private const string EspnUserAgent = "curl/8.14.1";

    private static HttpClient BuildEspnHttpClient(string baseAddress) {
        var client = new HttpClient { BaseAddress = new Uri(baseAddress) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(EspnUserAgent);
        return client;
    }

    private static EspnApiService BuildEspnApiService() =>
        new(BuildEspnHttpClient("http://site.api.espn.com"),
            new LoggerFactory().CreateLogger<EspnApiService>());

    private static CfbApiService BuildCfbApiService() =>
        new(BuildEspnHttpClient("http://site.api.espn.com"));

    private static EspnCoreOddsService BuildOddsService() =>
        new(BuildEspnHttpClient("https://sports.core.api.espn.com"));

    [Fact]
    public async Task NflScoreboard_RecentRegularSeasonWeek_DeserializesRealWireShape()
    {
        var scores = await BuildEspnApiService().GetWeekScores(10, 2025);

        Assert.NotNull(scores);
        Assert.NotEmpty(scores!.Events);
        Assert.NotEmpty(scores.Leagues!.First().Name);

        var competition = scores.Events.First().Competitions.First();
        var competitor = competition.Competitors.First();
        Assert.False(string.IsNullOrWhiteSpace(competitor.Team.Abbreviation));
        Assert.NotNull(competition.Status?.Type?.Name);
    }

    [Fact]
    public async Task NflScoreboard_PostseasonWeek_DeserializesRealWireShapeAndFlagsPostSeason()
    {
        // Week 1 postseason = Wild Card round of the completed 2025 season.
        var scores = await BuildEspnApiService().GetWeekScores(1, 2025, postSeason: true);

        Assert.NotNull(scores);
        Assert.NotEmpty(scores!.Events);
        Assert.True(scores.IsPostSeason());
    }

    [Fact]
    public async Task CfbScoreboard_RegularSeasonWeek_DeserializesRealWireShape()
    {
        // frizat-11t: date-range now, not week=N — 2025 season Week 10's real window (matches
        // DemoDataSeeder's CfbSlates seed row for the same week).
        var scores = await BuildCfbApiService().GetScoresByDateRangeAsync(
            new DateOnly(2025, 10, 25), new DateOnly(2025, 11, 1));

        Assert.NotNull(scores);
        Assert.NotEmpty(scores!.Events);
        var competitor = scores.Events.First().Competitions.First().Competitors.First();
        Assert.False(string.IsNullOrWhiteSpace(competitor.Team.Abbreviation));
    }

    [Fact]
    public async Task CfbScoreboard_CfpWeek999Bucket_DeserializesRealWireShape()
    {
        var scores = await BuildCfbApiService().GetCfpGamesAsync();

        Assert.NotNull(scores);
        Assert.NotEmpty(scores!.Events);
        // The week=999 bucket spans every CFP round at once — confirm it isn't collapsed to one date.
        var distinctDates = scores.Events.Select(e => e.Date.Date).Distinct().Count();
        Assert.True(distinctDates > 1, "Expected the CFP bucket to span more than one date across rounds.");
    }

    [Fact]
    public async Task NflTeamAbbreviations_FullSeason_AllMapToKnown32TeamSet()
    {
        var scores = await BuildEspnApiService().GetSeasonScores(2025);
        Assert.NotNull(scores);

        var seenAbbreviations = scores!.Events
            .SelectMany(e => e.Competitions)
            .SelectMany(c => c.Competitors)
            .Select(c => c.Team.Abbreviation)
            .Distinct()
            .ToList();

        var unmapped = seenAbbreviations.Where(a => !KnownNflAbbreviations.Contains(a)).ToList();
        Assert.True(unmapped.Count == 0,
            $"Unmapped NFL abbreviations found in live data: {string.Join(", ", unmapped)}");
    }

    [Fact]
    public async Task Odds_ForRealNflEvent_DeserializesRealWireShape()
    {
        var scores = await BuildEspnApiService().GetWeekScores(10, 2025);
        Assert.NotNull(scores);
        var eventId = int.Parse(scores!.Events.First().Id);

        var odds = await BuildOddsService().GetEventsWithOddsAsync(eventId);

        Assert.NotNull(odds);
        Assert.NotNull(odds!.Items);
    }

    [Fact]
    public async Task Odds_ForUnknownProvider_ReturnsNullNotException()
    {
        var scores = await BuildEspnApiService().GetWeekScores(10, 2025);
        Assert.NotNull(scores);
        var eventId = int.Parse(scores!.Events.First().Id);

        var odds = await BuildOddsService().GetEventsWithOddsAsync(eventId, providerId: -1);

        Assert.Null(odds);
    }
}
