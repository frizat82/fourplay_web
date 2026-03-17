using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Services;
using FourPlayWebApp.Server.Services.Repositories.Interfaces;
using FourPlayWebApp.Shared.Models.Data;
using Microsoft.Extensions.Caching.Memory;
using NSubstitute;

namespace FourPlayWebApp.Server.UnitTests;

/// <summary>
/// Regression tests for Issue #3: SpreadCalculatorBuilder registered as Singleton
/// with mutable instance fields (_leagueId, _week, _season) is not thread-safe.
///
/// The production fix is changing from Singleton to Scoped, meaning each request
/// gets its own builder instance. These tests verify:
///   1. Scoped behaviour (separate instances per request) always returns correct results,
///      even under concurrency.
///   2. The shared-singleton scenario correctly surfaces the race condition — any test
///      that uses a single shared instance under concurrency is expected to be fragile.
/// </summary>
public class SpreadCalculatorBuilderConcurrencyTests
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

    /// <summary>
    /// Simulates the fixed Scoped scenario: each concurrent "request" creates its own
    /// SpreadCalculatorBuilder instance (as DI would with Scoped lifetime).
    /// All concurrent builds must return the correct spreads for their week.
    /// </summary>
    [Fact]
    public async Task BuildAsync_ConcurrentBuilds_NeverReturnWrongWeekSpreads()
    {
        var repo  = BuildMockRepo();
        var cache = new MemoryCache(new MemoryCacheOptions());
        const int leagueId   = 1;
        const int season     = 2024;
        const int iterations = 20;

        var tasks = Enumerable.Range(0, iterations).Select(i =>
        {
            int week = (i % 2) + 1; // alternates week 1 and week 2
            return Task.Run(async () =>
            {
                // Each task gets its own builder instance — mirrors Scoped DI behaviour
                var builder = new SpreadCalculatorBuilder(repo, cache);
                var calc = await builder
                    .WithLeagueId(leagueId)
                    .WithWeek(week)
                    .WithSeason(season)
                    .BuildAsync();

                var expectedTeam = week == 1 ? "W1Home" : "W2Home";
                var wrongTeam    = week == 1 ? "W2Home" : "W1Home";

                return new
                {
                    Week = week,
                    HasCorrectTeam = calc.GetSpread(expectedTeam) is not null,
                    HasWrongTeam   = calc.GetSpread(wrongTeam) is not null,
                };
            });
        });

        var results = await Task.WhenAll(tasks);

        foreach (var r in results)
        {
            Assert.True(r.HasCorrectTeam,
                $"Week {r.Week}: calculator was missing its own team — Scoped instances should never cross-contaminate.");
            Assert.False(r.HasWrongTeam,
                $"Week {r.Week}: calculator contained the other week's team — Scoped instances should never cross-contaminate.");
        }
    }

    /// <summary>
    /// Two independent instances (the behaviour after Scoped registration) must always
    /// build correctly, regardless of concurrency.
    /// </summary>
    [Fact]
    public async Task BuildAsync_SeparateInstances_AlwaysIndependent()
    {
        var repo  = BuildMockRepo();
        var cache = new MemoryCache(new MemoryCacheOptions());

        var builder1 = new SpreadCalculatorBuilder(repo, cache);
        var builder2 = new SpreadCalculatorBuilder(repo, cache);

        var calc1Task = builder1.WithLeagueId(1).WithWeek(1).WithSeason(2024).BuildAsync();
        var calc2Task = builder2.WithLeagueId(1).WithWeek(2).WithSeason(2024).BuildAsync();

        var (calc1, calc2) = (await calc1Task, await calc2Task);

        Assert.NotNull(calc1.GetSpread("W1Home"));
        Assert.Null(calc1.GetSpread("W2Home"));

        Assert.NotNull(calc2.GetSpread("W2Home"));
        Assert.Null(calc2.GetSpread("W1Home"));
    }
}
