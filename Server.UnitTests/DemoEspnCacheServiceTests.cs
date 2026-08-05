using FourPlayWebApp.Server.Data;
using FourPlayWebApp.Server.Services;
using FourPlayWebApp.Shared.Models;
using FourPlayWebApp.Shared.Models.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace FourPlayWebApp.Server.UnitTests;

/// <summary>
/// TDD tests for DemoEspnCacheService.GetWeekScoresAsync (frizat fix — the "browse a non-current
/// week" ESPN scores endpoint used to always hit the real live ESPN API, even under DEMO_MODE,
/// bypassing the demo-aware cache abstraction every other ESPN-scores endpoint already used).
/// </summary>
public class DemoEspnCacheServiceTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static ApplicationDbContext BuildDb(string name) =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(name)
            .Options);

    private static IDbContextFactory<ApplicationDbContext> BuildFactory(ApplicationDbContext db)
    {
        var factory = Substitute.For<IDbContextFactory<ApplicationDbContext>>();
        factory.CreateDbContextAsync().Returns(db);
        return factory;
    }

    private static IWebHostEnvironment BuildFakeEnv()
    {
        var env = Substitute.For<IWebHostEnvironment>();
        env.EnvironmentName.Returns("Development");
        // Deliberately points nowhere — DemoFixtureLoader logs a warning and returns null when
        // the fixture file doesn't exist, which is fine since these tests only exercise
        // GetWeekScoresAsync (DB-backed), not GetScoresAsync (fixture-backed).
        env.ContentRootPath.Returns("/nonexistent");
        return env;
    }

    // ── GetWeekScoresAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task GetWeekScoresAsync_ReturnsEvents_ForSeededPostSeasonWeek()
    {
        // ESPN week 1 postseason = Wild Card = internal NflWeek 19 (GameHelpers.GetWeekFromEspnWeek).
        await using var db = BuildDb(nameof(GetWeekScoresAsync_ReturnsEvents_ForSeededPostSeasonWeek));
        db.NflScores.Add(new NflScores {
            Season = 2025,
            NflWeek = 19,
            HomeTeam = "KC",
            AwayTeam = "DEN",
            HomeTeamScore = 27,
            AwayTeamScore = 20,
            GameTime = new DateTimeOffset(2026, 1, 10, 18, 0, 0, TimeSpan.Zero),
        });
        db.NflScores.Add(new NflScores {
            Season = 2025,
            NflWeek = 19,
            HomeTeam = "BUF",
            AwayTeam = "MIA",
            HomeTeamScore = 31,
            AwayTeamScore = 17,
            GameTime = new DateTimeOffset(2026, 1, 10, 21, 30, 0, TimeSpan.Zero),
        });
        // A different week's row should never show up in the Wild Card query.
        db.NflScores.Add(new NflScores {
            Season = 2025,
            NflWeek = 18,
            HomeTeam = "SF",
            AwayTeam = "SEA",
            HomeTeamScore = 10,
            AwayTeamScore = 9,
            GameTime = new DateTimeOffset(2026, 1, 4, 18, 0, 0, TimeSpan.Zero),
        });
        await db.SaveChangesAsync();

        var sut = new DemoEspnCacheService(BuildFakeEnv(), BuildFactory(db));

        var result = await sut.GetWeekScoresAsync(1, 2025, postSeason: true);

        Assert.NotNull(result);
        Assert.NotNull(result!.Events);
        Assert.Equal(2, result.Events!.Length);

        var kcGame = result.Events.Single(e =>
            e.Competitions[0].Competitors.Any(c => c.Team.Abbreviation == "KC"));
        var home = kcGame.Competitions[0].Competitors.Single(c => c.HomeAway == HomeAway.Home);
        var away = kcGame.Competitions[0].Competitors.Single(c => c.HomeAway == HomeAway.Away);
        Assert.Equal("KC", home.Team.Abbreviation);
        Assert.Equal(27, home.Score);
        Assert.Equal("DEN", away.Team.Abbreviation);
        Assert.Equal(20, away.Score);

        foreach (var ev in result.Events)
        {
            var status = ev.Competitions[0].Status;
            Assert.Equal(TypeName.StatusFinal, status.Type.Name);
            Assert.Equal(State.Post, status.Type.State);
            Assert.True(status.Type.Completed);
        }
    }

    [Fact]
    public async Task GetWeekScoresAsync_ReturnsNull_WhenNoScoresSeededForWeek()
    {
        await using var db = BuildDb(nameof(GetWeekScoresAsync_ReturnsNull_WhenNoScoresSeededForWeek));
        db.NflScores.Add(new NflScores {
            Season = 2025,
            NflWeek = 1,
            HomeTeam = "KC",
            AwayTeam = "DEN",
            HomeTeamScore = 27,
            AwayTeamScore = 20,
            GameTime = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        var sut = new DemoEspnCacheService(BuildFakeEnv(), BuildFactory(db));

        // Week 5, regular season — nothing seeded for that combo.
        var result = await sut.GetWeekScoresAsync(5, 2025, postSeason: false);

        Assert.Null(result);
    }
}
