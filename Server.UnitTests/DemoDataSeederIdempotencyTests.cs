using DotNet.Testcontainers.Builders;
using FourPlayWebApp.Server.Data;
using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Models.Identity;
using FourPlayWebApp.Server.Services;
using FourPlayWebApp.Shared.Models.Data;
using FourPlayWebApp.Shared.Models.Enum;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using Testcontainers.PostgreSql;
using Xunit;

namespace FourPlayWebApp.Server.UnitTests;

/// <summary>
/// PostgreSQL-backed regression tests for DemoDataSeeder.SeedHistoricalWeeksAsync.
///
/// WHY POSTGRESQL (not SQLite): ExecuteDeleteAsync with int[] array Contains silently no-ops on
/// Npgsql but translates correctly on SQLite. The prior SQLite test gave false confidence and
/// allowed a crash to reach Railway dev on every redeploy. These tests use a real Postgres
/// container so provider-specific translation failures are caught before merge.
/// </summary>
public class PostgresSeederFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithCleanUp(true)
        .Build();

    public string ConnectionString { get; private set; } = "";

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        ConnectionString = _container.GetConnectionString();
        await using var db = OpenDb();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();

    public ApplicationDbContext OpenDb() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(ConnectionString)
            .Options);
}

public class DemoDataSeederIdempotencyTests : IClassFixture<PostgresSeederFixture>
{
    private const int DemoSeason = 2025;
    private const string AdminEmail = "admin@demo.local";
    private const string AdminId = "admin-seed-id";

    private readonly PostgresSeederFixture _fixture;

    public DemoDataSeederIdempotencyTests(PostgresSeederFixture fixture)
    {
        _fixture = fixture;
    }

    private ApplicationDbContext OpenDb() => _fixture.OpenDb();

    /// <summary>
    /// Wipes all seeder-managed data before each test so tests are isolated within the shared container.
    /// Deletes in FK-safe order: picks → scores → spreads → weeks → cfb slates → league → user.
    /// </summary>
    private async Task ResetAsync()
    {
        await using var db = OpenDb();
        // FK-safe order
        db.NflPicks.RemoveRange(await db.NflPicks.Where(p => p.Season == DemoSeason).ToListAsync());
        db.NflScores.RemoveRange(await db.NflScores.Where(s => s.Season == DemoSeason).ToListAsync());
        db.NflSpreads.RemoveRange(await db.NflSpreads.Where(s => s.Season == DemoSeason).ToListAsync());
        db.NflWeeks.RemoveRange(await db.NflWeeks.Where(w => w.Season == DemoSeason).ToListAsync());
        db.CfbSlates.RemoveRange(await db.CfbSlates.Where(c => c.Season == DemoSeason).ToListAsync());
        db.LeagueInfo.RemoveRange(await db.LeagueInfo.ToListAsync());
        db.Users.RemoveRange(await db.Users.Where(u => u.Id == AdminId).ToListAsync());
        await db.SaveChangesAsync();
    }

    private static UserManager<ApplicationUser> BuildUserManager(ApplicationUser adminUser)
    {
        var store = Substitute.For<IUserStore<ApplicationUser>>();
        var mgr = Substitute.For<UserManager<ApplicationUser>>(
            store, null, null, null, null, null, null, null, null);
        mgr.FindByEmailAsync(AdminEmail).Returns(adminUser);
        mgr.FindByNameAsync(Arg.Any<string>()).Returns((ApplicationUser?)null);
        return mgr;
    }

    private static IConfiguration BuildConfig() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ADMIN_EMAIL"] = AdminEmail })
            .Build();

    private async Task<(ApplicationUser admin, LeagueInfo league)> SeedPrerequisitesAsync(ApplicationDbContext db)
    {
        var admin = new ApplicationUser { Id = AdminId, UserName = "admin", Email = AdminEmail };
        db.Users.Add(admin);
        var league = new LeagueInfo { LeagueName = "Demo League", OwnerUserId = AdminId };
        db.LeagueInfo.Add(league);
        await db.SaveChangesAsync();
        return (admin, league);
    }

    // ── Test 1 ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task SeedHistoricalWeeksAsync_does_not_crash_when_all_historical_data_already_exists()
    {
        await ResetAsync();
        await using var db = OpenDb();
        var (admin, league) = await SeedPrerequisitesAsync(db);

        // Arrange — pre-populate all 21 historical weeks (simulates a prior successful Railway deploy)
        foreach (var week in Enumerable.Range(1, 17).Concat(Enumerable.Range(19, 4)))
        {
            var weekRow = new NflWeeks
            {
                Season = DemoSeason, NflWeek = week,
                StartDate = DateTimeOffset.UtcNow, EndDate = DateTimeOffset.UtcNow.AddDays(6),
            };
            db.NflWeeks.Add(weekRow);
            await db.SaveChangesAsync();

            db.NflSpreads.Add(new NflSpreads
            {
                Season = DemoSeason, NflWeek = week,
                HomeTeam = "KC", AwayTeam = "DEN",
                HomeTeamSpread = -7, AwayTeamSpread = 7, OverUnder = 45,
                GameTime = DateTimeOffset.UtcNow,
            });
            db.NflScores.Add(new NflScores
            {
                Season = DemoSeason, NflWeek = week,
                HomeTeam = "KC", AwayTeam = "DEN",
                HomeTeamScore = 24, AwayTeamScore = 14, GameTime = DateTimeOffset.UtcNow,
            });
            db.NflPicks.Add(new NflPicks
            {
                UserId = AdminId, LeagueId = league.Id, Team = "KC",
                Pick = PickType.Spread, NflWeek = week, Season = DemoSeason,
                DateCreated = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        // Act — second deploy re-runs the seeder
        var seeder = new DemoDataSeeder(db, BuildUserManager(admin), BuildConfig());
        var exception = await Record.ExceptionAsync(() => seeder.SeedHistoricalWeeksAsync(league));

        // Assert
        Assert.Null(exception);
        await using var verify = OpenDb();
        Assert.Equal(21, await verify.NflWeeks.CountAsync(w => w.Season == DemoSeason && w.NflWeek != 18));
    }

    // ── Test 2 ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task SeedHistoricalWeeksAsync_wipes_and_reseeds_on_repeated_calls_no_duplicate_rows()
    {
        await ResetAsync();
        await using var db = OpenDb();
        var (admin, league) = await SeedPrerequisitesAsync(db);

        var seeder = new DemoDataSeeder(db, BuildUserManager(admin), BuildConfig());

        // Act — run twice (simulates two Railway deploys against the same Neon DB)
        var ex1 = await Record.ExceptionAsync(() => seeder.SeedHistoricalWeeksAsync(league));
        var ex2 = await Record.ExceptionAsync(() => seeder.SeedHistoricalWeeksAsync(league));

        // Assert — no crash on either call, and rows are exactly 21 (not 42)
        Assert.Null(ex1);
        Assert.Null(ex2);
        await using var verify = OpenDb();
        var historicalWeekCount = await verify.NflWeeks
            .CountAsync(w => w.Season == DemoSeason && w.NflWeek != 18);
        Assert.Equal(21, historicalWeekCount);
    }

    // ── Test 3 ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task SeedHistoricalWeeksAsync_does_not_delete_week18_or_cfb_data()
    {
        await ResetAsync();
        await using var db = OpenDb();
        var (admin, league) = await SeedPrerequisitesAsync(db);

        // Arrange — week 18 is the live/current week (NOT in historicalWeekNums), and a CFB slate
        db.NflWeeks.Add(new NflWeeks
        {
            Season = DemoSeason, NflWeek = 18,
            StartDate = DateTimeOffset.UtcNow, EndDate = DateTimeOffset.UtcNow.AddDays(6),
        });
        db.CfbSlates.Add(new CfbSlates
        {
            Season = DemoSeason, SlateNumber = 1,
            Label = "Week 1", SlateType = "Regular",
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(6)),
        });
        await db.SaveChangesAsync();

        // Act
        var seeder = new DemoDataSeeder(db, BuildUserManager(admin), BuildConfig());
        await seeder.SeedHistoricalWeeksAsync(league);

        // Assert — week 18 NflWeeks row and CFB slate both survive
        await using var verify = OpenDb();
        Assert.True(await verify.NflWeeks.AnyAsync(w => w.Season == DemoSeason && w.NflWeek == 18),
            "Week 18 (live/current week) should not be wiped by historical seeder");
        Assert.True(await verify.CfbSlates.AnyAsync(c => c.Season == DemoSeason),
            "CFB slates should not be touched by NFL historical seeder");
    }
}
