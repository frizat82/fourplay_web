using FourPlayWebApp.Server.Data;
using FourPlayWebApp.Server.Services;
using FourPlayWebApp.Shared.Models;
using FourPlayWebApp.Shared.Models.Data;
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

    // ── GetScoresAsync (fixture-backed) ─────────────────────────────────────

    // frizat: regression test for the fixture silently failing to load on Railway.
    // DemoFixtureLoader used to resolve sample_espn_nfl.json via
    // Path.Combine(env.ContentRootPath, "..", fileName) — only correct locally by coincidence
    // (dotnet run from Server/ puts the repo root one level up); on Railway, ContentRootPath is
    // the deployed app's own directory, so ".." resolved to nothing and the fixture silently
    // never loaded. No IWebHostEnvironment/working-directory setup needed here at all now — the
    // fixture is an embedded resource, found by assembly-relative logical name regardless of
    // where or how the process is actually running.
    [Fact]
    public async Task GetScoresAsync_LoadsFixture_RegardlessOfWorkingDirectoryOrContentRoot()
    {
        var sut = new DemoEspnCacheService(BuildFactory(BuildDb(nameof(GetScoresAsync_LoadsFixture_RegardlessOfWorkingDirectoryOrContentRoot))));

        var scores = await sut.GetScoresAsync();

        Assert.NotNull(scores);
        Assert.NotNull(scores!.Events);
        Assert.NotEmpty(scores.Events!);
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

        var sut = new DemoEspnCacheService(BuildFactory(db));

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

        var sut = new DemoEspnCacheService(BuildFactory(db));

        // Week 5, regular season — nothing seeded for that combo.
        var result = await sut.GetWeekScoresAsync(5, 2025, postSeason: false);

        Assert.Null(result);
    }

    // frizat: nflAdapter.ts's current-week path now always calls this endpoint for whichever
    // week the control table resolves as current — that week must serve the frozen in-progress
    // fixture (same as GetScoresAsync), not DB-persisted final scores. Before this fix, this
    // endpoint always went DB-only regardless of "current," which silently dropped the live
    // situation/clock/down-distance data the demo e2e suite's Super Bowl scenarios depend on.
    [Fact]
    public async Task GetWeekScoresAsync_WhenWeekIsTheResolvedCurrentWeek_ReturnsTheFrozenFixture_NotDbBuiltScores()
    {
        await using var db = BuildDb(nameof(GetWeekScoresAsync_WhenWeekIsTheResolvedCurrentWeek_ReturnsTheFrozenFixture_NotDbBuiltScores));
        // A seeded final score that would otherwise be served by the DB-only path — proves the
        // frozen fixture wins instead once this week resolves as "current".
        db.NflScores.Add(new NflScores {
            Season = 2025,
            NflWeek = 22,
            HomeTeam = "SF",
            AwayTeam = "SEA",
            HomeTeamScore = 10,
            AwayTeamScore = 9,
            GameTime = new DateTimeOffset(2026, 1, 25, 18, 0, 0, TimeSpan.Zero),
        });
        db.NflSeasonWeekConfigs.Add(new Models.Data.NflSeasonWeekConfig {
            Season = 2025,
            WeekId = 22,
            WeekLabel = "Super Bowl",
            WeekType = "PostSeason",
            ScoringFormat = "Standard",
            WeekStartDatetime = new DateTime(2026, 1, 25),
            WeekEndDatetime = new DateTime(2026, 1, 26),
            SpreadLockDatetime = new DateTime(2026, 1, 25),
        });
        await db.SaveChangesAsync();

        var sut = new DemoEspnCacheService(BuildFactory(db));

        // Raw ESPN week 5 = Super Bowl = internal WeekId 22 (GameHelpers.GetWeekFromEspnWeek).
        var result = await sut.GetWeekScoresAsync(5, 2025, postSeason: true);

        Assert.NotNull(result);
        Assert.NotNull(result!.Events);
        Assert.NotEmpty(result.Events!);
        // The frozen fixture's teams, not the DB-persisted SF/SEA final score.
        Assert.DoesNotContain(result.Events!, e => e.Competitions[0].Competitors.Any(c => c.Team.Abbreviation == "SF"));
    }
}
