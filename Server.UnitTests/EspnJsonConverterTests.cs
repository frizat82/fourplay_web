using System.Net;
using System.Text;
using FourPlayWebApp.Server.Services;
using FourPlayWebApp.Shared.Models;

namespace FourPlayWebApp.Server.UnitTests;

/// <summary>
/// frizat-703.5 code-review follow-up: the live Contract tests proved CfbApiService.cs threw on
/// real "homeAway"/CFB record-type wire values, and the fix was consolidating everything onto
/// EspnApiServiceJsonConverter.Settings. But Contract tests are excluded from the default `dotnet
/// test` run (network-dependent, weekly only) — without an offline test, a future revert of
/// CfbApiService's `_opts` field back to a bare options object, or a revert of the EspnRecordType
/// enum, would pass every default PR check and go undetected for up to a week. These tests close
/// that gap: fast, offline, no network, run on every `dotnet test`.
/// </summary>
public class EspnJsonConverterTests
{
    [Theory]
    [InlineData("away", HomeAway.Away)]
    [InlineData("home", HomeAway.Home)]
    public void HomeAwayConverter_ParsesRealWireValues(string wireValue, HomeAway expected)
    {
        var json = $$"""{"homeAway":"{{wireValue}}"}""";
        var competitor = System.Text.Json.JsonSerializer.Deserialize<Competitor>(json, EspnApiServiceJsonConverter.Settings);

        Assert.Equal(expected, competitor!.HomeAway);
    }

    [Theory]
    [InlineData("road", EspnRecordType.Road)] // NFL
    [InlineData("home", EspnRecordType.Home)] // NFL
    [InlineData("total", EspnRecordType.Total)] // NFL + CFB
    [InlineData("homerecord", EspnRecordType.HomeRecord)] // CFB
    [InlineData("awayrecord", EspnRecordType.AwayRecord)] // CFB
    [InlineData("vsconf", EspnRecordType.VsConf)] // CFB
    public void EspnRecordTypeConverter_ParsesRealWireValues(string wireValue, EspnRecordType expected)
    {
        var json = $$"""{"type":"{{wireValue}}"}""";
        var record = System.Text.Json.JsonSerializer.Deserialize<EspnRecord>(json, EspnApiServiceJsonConverter.Settings);

        Assert.Equal(expected, record!.Type);
    }

    private class StubHttpMessageHandler(string responseJson) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json"),
            });
    }

    // Minimal real-shaped CFB scoreboard payload: one event, one competition, two competitors
    // (homeAway "away"/"home"), one "homerecord" record. Regression guard for the exact defect
    // this PR fixed — CfbApiService throwing on real ESPN wire values.
    private const string MinimalCfbScoreboardJson = """
        {
          "events": [
            {
              "id": "1",
              "date": "2025-11-01T00:00Z",
              "competitions": [
                {
                  "id": "1",
                  "date": "2025-11-01T00:00Z",
                  "status": {
                    "clock": 0,
                    "displayClock": "0:00",
                    "period": 4,
                    "type": { "id": "3", "name": "STATUS_FINAL", "state": "post", "completed": true, "description": "Final", "detail": "Final", "shortDetail": "Final" }
                  },
                  "competitors": [
                    {
                      "id": "1",
                      "homeAway": "home",
                      "team": { "abbreviation": "IND" },
                      "score": "13",
                      "records": [ { "name": "overall", "type": "homerecord", "summary": "5-3" } ]
                    },
                    {
                      "id": "2",
                      "homeAway": "away",
                      "team": { "abbreviation": "ATL" },
                      "score": "14",
                      "records": [ { "name": "overall", "type": "awayrecord", "summary": "4-4" } ]
                    }
                  ]
                }
              ]
            }
          ]
        }
        """;

    [Fact]
    public async Task CfbApiService_GetScoresByWeekAsync_DeserializesRealWireValuesWithoutThrowing()
    {
        var httpClient = new HttpClient(new StubHttpMessageHandler(MinimalCfbScoreboardJson))
        {
            BaseAddress = new Uri("http://site.api.espn.com"),
        };
        var service = new CfbApiService(httpClient);

        var scores = await service.GetScoresByWeekAsync(week: 10, isPostSeason: false);

        var competitors = scores!.Events!.Single().Competitions.Single().Competitors;
        Assert.Equal(HomeAway.Home, competitors.Single(c => c.Team.Abbreviation == "IND").HomeAway);
        Assert.Equal(HomeAway.Away, competitors.Single(c => c.Team.Abbreviation == "ATL").HomeAway);
        Assert.Equal(EspnRecordType.HomeRecord, competitors.Single(c => c.Team.Abbreviation == "IND").Records.Single().Type);
    }
}
