using FourPlayWebApp.Server.Jobs;
using FourPlayWebApp.Shared.Models;
using Xunit;

namespace FourPlayWebApp.Server.UnitTests;

// frizat-bo1: NflSpreadJob and CfbSpreadJob each duplicated the "fetch odds, prefer DraftKings,
// fall back to any available provider, parse the American spread strings" logic — extracted here
// as the one piece that was genuinely identical, while each job keeps its own sport-specific
// current-week/slate resolution and CfbSpreads/NflSpreads construction untouched.
public class SpreadOddsFetcherTests
{
    private static EspnCoreOddsItem MakeOdds(string homeAmerican, string awayAmerican, double overUnder = 45.5, string providerName = "ESPN BET") => new() {
        Provider = new EspnCoreOddsProvider { Name = providerName },
        OverUnder = overUnder,
        HomeTeamOdds = new EspnCoreTeamOdds { Current = new EspnCoreTeamOddsDetail { PointSpread = new EspnCorePointSpread { American = homeAmerican } } },
        AwayTeamOdds = new EspnCoreTeamOdds { Current = new EspnCoreTeamOddsDetail { PointSpread = new EspnCorePointSpread { American = awayAmerican } } },
    };

    [Fact]
    public async Task FetchAsync_UsesDraftKingsResult_WhenAvailable()
    {
        var draftKingsOdds = MakeOdds("-3.5", "+3.5");

        var result = await SpreadOddsFetcher.FetchAsync(
            getByProvider: (_, _) => Task.FromResult<EspnCoreOddsItem?>(draftKingsOdds),
            getAny: _ => Task.FromResult<EspnCoreOddsApiResponse?>(null),
            eventId: 1,
            gameLabel: "TEST");

        Assert.NotNull(result);
        Assert.Equal(-3.5, result.Value.HomeSpread);
        Assert.Equal(3.5, result.Value.AwaySpread);
        Assert.Equal(45.5, result.Value.OverUnder);
    }

    [Fact]
    public async Task FetchAsync_FallsBackToFirstAvailableProvider_WhenDraftKingsUnavailable()
    {
        var fallbackOdds = MakeOdds("-6.5", "+6.5", overUnder: 51.0, providerName: "Other Book");

        var result = await SpreadOddsFetcher.FetchAsync(
            getByProvider: (_, _) => Task.FromResult<EspnCoreOddsItem?>(null),
            getAny: _ => Task.FromResult<EspnCoreOddsApiResponse?>(new EspnCoreOddsApiResponse { Count = 1, Items = [fallbackOdds] }),
            eventId: 1,
            gameLabel: "TEST");

        Assert.NotNull(result);
        Assert.Equal(-6.5, result.Value.HomeSpread);
        Assert.Equal(6.5, result.Value.AwaySpread);
        Assert.Equal(51.0, result.Value.OverUnder);
    }

    [Fact]
    public async Task FetchAsync_ReturnsNull_WhenNoProviderHasOdds()
    {
        var result = await SpreadOddsFetcher.FetchAsync(
            getByProvider: (_, _) => Task.FromResult<EspnCoreOddsItem?>(null),
            getAny: _ => Task.FromResult<EspnCoreOddsApiResponse?>(new EspnCoreOddsApiResponse { Items = [] }),
            eventId: 1,
            gameLabel: "TEST");

        Assert.Null(result);
    }

    [Fact]
    public async Task FetchAsync_StripsLeadingPlusSign_FromPositiveAmericanSpreads()
    {
        var odds = MakeOdds("-2.5", "+2.5");

        var result = await SpreadOddsFetcher.FetchAsync(
            getByProvider: (_, _) => Task.FromResult<EspnCoreOddsItem?>(odds),
            getAny: _ => Task.FromResult<EspnCoreOddsApiResponse?>(null),
            eventId: 1,
            gameLabel: "TEST");

        Assert.NotNull(result);
        Assert.Equal(2.5, result.Value.AwaySpread);
    }

    [Fact]
    public async Task FetchAsync_ReturnsNull_WhenSpreadIsNotParseable()
    {
        var odds = MakeOdds("PK", "PK"); // pick'em games can render as non-numeric text

        var result = await SpreadOddsFetcher.FetchAsync(
            getByProvider: (_, _) => Task.FromResult<EspnCoreOddsItem?>(odds),
            getAny: _ => Task.FromResult<EspnCoreOddsApiResponse?>(null),
            eventId: 1,
            gameLabel: "TEST");

        Assert.Null(result);
    }
}
