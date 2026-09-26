using FourPlayWebApp.Server.Services;
using FourPlayWebApp.Server.UnitTests.TestHelpers;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

namespace FourPlayWebApp.Server.UnitTests;

// frizat-4k9: FixEspnProbBowlWeek's week 5->4 postseason rewrite must be gated by
// GameHelpers.LastSeasonEspnSkippedProBowlWeek — the same season threshold
// GameHelpers.GetWeekFromEspnWeek already uses — instead of carrying its own
// independent, unconditional copy of the quirk-correction.
public class ESPNApiServiceTests {
    private static (EspnApiService sut, CapturingHandler handler) Build() {
        var handler = new CapturingHandler();
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://site.api.espn.com") };
        return (new EspnApiService(httpClient, NullLogger<EspnApiService>.Instance), handler);
    }

    private static string PostSeasonResponseBody(int seasonYear, int weekNumber) => $$"""
    {
      "season": { "type": 3, "year": {{seasonYear}} },
      "week": { "number": {{weekNumber}} },
      "events": [
        {
          "id": "401547405",
          "season": { "type": 3, "year": {{seasonYear}} },
          "week": { "number": {{weekNumber}} },
          "date": "{{seasonYear}}-02-08T23:30Z",
          "competitions": [
            {
              "id": "401547405",
              "date": "{{seasonYear}}-02-08T23:30Z",
              "competitors": [
                { "id": "1", "homeAway": "home", "score": "0", "team": { "abbreviation": "KC" } },
                { "id": "2", "homeAway": "away", "score": "0", "team": { "abbreviation": "SF" } }
              ],
              "status": {
                "clock": 0, "displayClock": "0:00", "period": 0,
                "type": { "id": "1", "name": "STATUS_SCHEDULED", "state": "pre", "completed": false, "description": "Scheduled" }
              },
              "odds": []
            }
          ]
        }
      ]
    }
    """;

    [Theory]
    [InlineData(2025, 4)] // season <= LastSeasonEspnSkippedProBowlWeek: raw week 5 rewritten to 4
    [InlineData(2026, 5)] // season > LastSeasonEspnSkippedProBowlWeek: raw week 5 left untouched
    public async Task GetScoresByDateRangeAsync_PostSeasonWeek5_RewrittenOnlyForGatedSeason(int seasonYear, int expectedWeekNumber) {
        var (sut, handler) = Build();
        handler.ResponseBody = PostSeasonResponseBody(seasonYear, 5);

        var result = await sut.GetScoresByDateRangeAsync(new DateOnly(seasonYear + 1, 2, 8), new DateOnly(seasonYear + 1, 2, 9), postSeason: true);

        Assert.NotNull(result);
        Assert.Equal(expectedWeekNumber, result!.Week.Number);
        Assert.Equal(expectedWeekNumber, Assert.Single(result.Events!).Week.Number);
    }

    // A day whose games are all final isn't requested again (EspnDayCache); the season type is part
    // of the cached day, so a regular-season and a postseason query for the same date don't collide.
    [Fact]
    public async Task FinishedDays_AreServedFromTheDayCache_PerSeasonType() {
        var handler = new CapturingHandler { ResponseBody = FinalDayBody };
        var sut = new EspnApiService(new HttpClient(handler) { BaseAddress = new Uri("http://site.api.espn.com") },
            NullLogger<EspnApiService>.Instance, new EspnDayCache(new MemoryCache(new MemoryCacheOptions()), TimeProvider.System));
        var day = new DateOnly(2025, 9, 14);

        await sut.GetScoresByDateRangeAsync(day, day);
        await sut.GetScoresByDateRangeAsync(day, day);
        await sut.GetScoresByDateRangeAsync(day, day, postSeason: true);

        Assert.Equal(2, handler.RequestUris.Count);
        Assert.Contains(handler.RequestUris, u => u.Query.Contains("seasontype=3"));
    }

    private const string FinalDayBody = """
    {
      "season": { "type": 2, "year": 2025 },
      "week": { "number": 2 },
      "events": [
        {
          "id": "401772000", "season": { "type": 2, "year": 2025 }, "week": { "number": 2 },
          "date": "2025-09-14T17:00Z",
          "competitions": [
            {
              "id": "401772000", "date": "2025-09-14T17:00Z",
              "competitors": [
                { "id": "1", "homeAway": "home", "score": "24", "team": { "abbreviation": "KC" } },
                { "id": "2", "homeAway": "away", "score": "17", "team": { "abbreviation": "BUF" } }
              ],
              "status": {
                "clock": 0, "displayClock": "0:00", "period": 4,
                "type": { "id": "3", "name": "STATUS_FINAL", "state": "post", "completed": true, "description": "Final" }
              },
              "odds": []
            }
          ]
        }
      ]
    }
    """;
}
