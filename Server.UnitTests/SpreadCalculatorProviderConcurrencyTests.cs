using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Services;
using FourPlayWebApp.Server.Services.Repositories.Interfaces;
using FourPlayWebApp.Shared.Models.Data;
using Microsoft.Extensions.Caching.Memory;
using NSubstitute;

namespace FourPlayWebApp.Server.UnitTests;

/// <summary>
/// Regression tests for Issue #3: the old SpreadCalculatorBuilder carried mutable fields
/// (_leagueId/_week/_season) set by fluent With* calls, so one instance shared across concurrent
/// callers could build week 1's calculator with week 2's spreads. It had to be registered Scoped
/// and never shared. SpreadCalculatorProvider takes every input as a parameter, so ONE instance
/// shared across concurrent calls — the case the builder couldn't survive — must stay correct.
/// </summary>
public class SpreadCalculatorProviderConcurrencyTests
{
    private static ILeagueRepository BuildMockRepo()
    {
        var week1Spreads = new List<NflSpreads>
        {
            new() { Season = 2024, NflWeek = 1, HomeTeam = "W1Home", AwayTeam = "W1Away", HomeTeamSpread = -3, AwayTeamSpread = 3, OverUnder = 45 }
        };
        var week2Spreads = new List<NflSpreads>
        {
            new() { Season = 2024, NflWeek = 2, HomeTeam = "W2Home", AwayTeam = "W2Away", HomeTeamSpread = -7, AwayTeamSpread = 7, OverUnder = 52 }
        };
        var juice = new LeagueJuiceMapping { WeeklyCost = 5, Juice = 5 };

        var repo = Substitute.For<ILeagueRepository>();
        repo.GetNflSpreadsAsync(2024, 1).Returns(Task.FromResult<List<NflSpreads>?>(week1Spreads));
        repo.GetNflSpreadsAsync(2024, 2).Returns(Task.FromResult<List<NflSpreads>?>(week2Spreads));
        repo.GetLeagueJuiceMappingAsync(Arg.Any<int>(), Arg.Any<int>()).Returns(Task.FromResult<LeagueJuiceMapping?>(juice));

        return repo;
    }

    [Fact]
    public async Task GetForNflWeekAsync_OneSharedInstanceUnderConcurrency_NeverReturnsAnotherWeeksSpreads()
    {
        var provider = new SpreadCalculatorProvider(BuildMockRepo(), new MemoryCache(new MemoryCacheOptions()));

        var results = await Task.WhenAll(Enumerable.Range(0, 40).Select(i => Task.Run(async () =>
        {
            int week = (i % 2) + 1; // alternates week 1 and week 2
            var calc = await provider.GetForNflWeekAsync(leagueId: 1, season: 2024, week: week);
            return (Week: week,
                HasOwn: calc.GetSpread(week == 1 ? "W1Home" : "W2Home") is not null,
                HasOther: calc.GetSpread(week == 1 ? "W2Home" : "W1Home") is not null);
        })));

        foreach (var r in results)
        {
            Assert.True(r.HasOwn, $"Week {r.Week}: calculator was missing its own week's team.");
            Assert.False(r.HasOther, $"Week {r.Week}: calculator contained the other week's team.");
        }
    }
}
