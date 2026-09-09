using FourPlayWebApp.Server.Data;
using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Services;
using FourPlayWebApp.Shared.Models;
using FourPlayWebApp.Shared.Models.Data;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace FourPlayWebApp.Server.UnitTests;

/// <summary>
/// frizat-fe2: DemoCfbCacheService must serve real ESPN-shaped situation data for the seeded
/// CFB Championship game (event 401800011, IU vs MIA), the same way DemoEspnCacheService serves
/// sample_espn_nfl.json for NFL — not the always-null stub it was after CFB_DEMO_SITUATION was
/// ripped out in PR #163.
///
/// No IWebHostEnvironment/working-directory setup needed here (unlike before) — the fixture is
/// an embedded resource, not a loose file resolved via a relative path off ContentRootPath. That
/// prior approach only worked locally by coincidence and silently returned null on Railway,
/// where ContentRootPath points at the deployed app's own directory.
///
/// frizat-d0t: GetSlateScoresAsync mirrors DemoEspnCacheServiceTests' GetWeekScoresAsync coverage
/// — same IDbContextFactory/TimeProvider constructor shape, same DB-first-for-non-current-slate
/// pattern.
/// </summary>
public class DemoCfbCacheServiceTests
{
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

    private static DemoCfbCacheService BuildService(ApplicationDbContext db) =>
        new(BuildFactory(db), TimeProvider.System);

    [Fact]
    public async Task GetScoresAsync_ReturnsRealSituationData_ForChampionshipGame()
    {
        var service = BuildService(BuildDb(nameof(GetScoresAsync_ReturnsRealSituationData_ForChampionshipGame)));

        var scores = await service.GetScoresAsync();

        var competition = Assert.Single(scores!.Events!).Competitions[0];
        Assert.NotNull(competition.Situation);
        Assert.Equal(2, competition.Situation.Down);
        Assert.Equal(7, competition.Situation.Distance);
        Assert.Equal(35, competition.Situation.YardLine);
        Assert.Equal("2nd & 7 at IU 35", competition.Situation.DownDistanceText);
    }

    [Fact]
    public async Task GetScoresAsync_ReturnsRealScoreAndTeams_ForChampionshipGame()
    {
        var service = BuildService(BuildDb(nameof(GetScoresAsync_ReturnsRealScoreAndTeams_ForChampionshipGame)));

        var scores = await service.GetScoresAsync();

        var competition = Assert.Single(scores!.Events!).Competitions[0];
        Assert.Equal("401800011", competition.Id);
        var home = Assert.Single(competition.Competitors, c => c.HomeAway == Shared.Models.HomeAway.Home);
        var away = Assert.Single(competition.Competitors, c => c.HomeAway == Shared.Models.HomeAway.Away);
        Assert.Equal("IU", home.Team.Abbreviation);
        Assert.Equal(14, home.Score);
        Assert.Equal("MIA", away.Team.Abbreviation);
        Assert.Equal(7, away.Score);
        Assert.Equal(Shared.Models.TypeName.StatusInProgress, competition.Status.Type.Name);
    }

    // ── GetSlateScoresAsync ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetSlateScoresAsync_WhenNoSlateMatchesTheRequestedId_ReturnsNull()
    {
        var service = BuildService(BuildDb(nameof(GetSlateScoresAsync_WhenNoSlateMatchesTheRequestedId_ReturnsNull)));

        var result = await service.GetSlateScoresAsync(999);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetSlateScoresAsync_ForANonCurrentSlateWithPersistedRows_ReturnsDbBuiltScores()
    {
        var db = BuildDb(nameof(GetSlateScoresAsync_ForANonCurrentSlateWithPersistedRows_ReturnsDbBuiltScores));
        db.CfbSeasonWeekConfigs.Add(new CfbSeasonWeekConfig {
            Season = 2025, EspnWeekNumber = 4, IvLeagueWeekNumber = 4,
            WeekStartDate = new DateOnly(2025, 9, 27), WeekEndDate = new DateOnly(2025, 9, 28),
            InScopeIvLeague = true, SpreadLockDatetime = new DateTime(2025, 9, 25, 0, 0, 0, DateTimeKind.Utc),
        });
        db.CfbSlates.Add(new CfbSlates {
            Id = 42, Season = 2025, SlateNumber = 4, Label = "Week 4", SlateType = "RegularSeason",
            StartDate = new DateOnly(2025, 9, 27), EndDate = new DateOnly(2025, 9, 28),
            EspnWeekNumber = 4, ScoringFormat = "Spread",
        });
        db.CfbScores.Add(new CfbScores {
            CfbSlateId = 42, HomeTeam = "OSU", AwayTeam = "NEB", HomeTeamScore = 28, AwayTeamScore = 14,
            GameStatus = TypeName.StatusFinal, GameTime = new DateTimeOffset(2025, 9, 27, 18, 0, 0, TimeSpan.Zero),
        });
        // A later (still real-world-past) slate the resolver treats as "current" — without this,
        // the Week 4 slate above would trivially resolve as its own "current" slate (nothing
        // else to compare against), exempting it from the DB-first shortcut this test exists to
        // verify (mirrors EspnCacheServiceTests' identical NFL fixture-seeding pattern).
        db.CfbSeasonWeekConfigs.Add(new CfbSeasonWeekConfig {
            Season = 2025, EspnWeekNumber = 5, IvLeagueWeekNumber = 5,
            WeekStartDate = new DateOnly(2025, 10, 4), WeekEndDate = new DateOnly(2025, 10, 5),
            InScopeIvLeague = true, SpreadLockDatetime = new DateTime(2025, 10, 2, 0, 0, 0, DateTimeKind.Utc),
        });
        db.CfbSlates.Add(new CfbSlates {
            Id = 43, Season = 2025, SlateNumber = 5, Label = "Week 5", SlateType = "RegularSeason",
            StartDate = new DateOnly(2025, 10, 4), EndDate = new DateOnly(2025, 10, 5),
            EspnWeekNumber = 5, ScoringFormat = "Spread",
        });
        await db.SaveChangesAsync();
        var service = BuildService(db);

        var result = await service.GetSlateScoresAsync(42);

        Assert.NotNull(result);
        var comp = result!.Events!.Single().Competitions[0];
        var home = comp.Competitors.Single(c => c.HomeAway == Shared.Models.HomeAway.Home);
        Assert.Equal("OSU", home.Team.Abbreviation);
        Assert.Equal(28, home.Score);
        Assert.Equal(Shared.Models.TypeName.StatusFinal, comp.Status.Type.Name);
    }
}
