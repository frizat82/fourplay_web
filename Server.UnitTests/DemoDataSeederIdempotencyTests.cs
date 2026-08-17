using FourPlayWebApp.Server.Data;
using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Models.Identity;
using FourPlayWebApp.Server.Services;
using FourPlayWebApp.Shared.Models.Data;
using FourPlayWebApp.Shared.Models.Enum;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using Xunit;

namespace FourPlayWebApp.Server.UnitTests;

/// <summary>
/// Regression guard: SeedHistoricalWeeksAsync must survive a re-deploy where NflSpreads/Scores/Picks
/// for historical weeks already exist (the crash that hit Railway dev when the seeder lacked wipe logic).
/// Uses SQLite in-memory because ExecuteDeleteAsync requires real SQL (EF InMemory throws).
/// </summary>
public class DemoDataSeederIdempotencyTests
{
    private const int DemoSeason = 2025;
    private const string AdminEmail = "admin@demo.local";
    private const string AdminId = "admin-seed-id";

    private static ApplicationDbContext OpenDb(string dbName) =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite($"Data Source={dbName};Mode=Memory;Cache=Shared")
            .Options);

    private static async Task WithDb(string dbName, Func<ApplicationDbContext, Task> action)
    {
        await using var keepAlive = new SqliteConnection($"Data Source={dbName};Mode=Memory;Cache=Shared");
        await keepAlive.OpenAsync();
        await using (var init = OpenDb(dbName)) { await init.Database.EnsureCreatedAsync(); }
        await using var db = OpenDb(dbName);
        await action(db);
    }

    private static UserManager<ApplicationUser> BuildUserManager(ApplicationUser? adminUser = null)
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

    [Fact]
    public async Task SeedHistoricalWeeksAsync_does_not_crash_when_historical_NflSpreads_already_exist()
    {
        var dbName = nameof(SeedHistoricalWeeksAsync_does_not_crash_when_historical_NflSpreads_already_exist);
        await WithDb(dbName, async db =>
        {
            // Arrange — pre-populate historical NflSpreads for weeks 1-17 and 19-22
            // (simulates the state of a Railway dev DB after the first successful deployment)
            var adminUser = new ApplicationUser { Id = AdminId, UserName = "admin", Email = AdminEmail };
            db.Users.Add(adminUser);
            var league = new LeagueInfo { LeagueName = "Demo League", OwnerUserId = AdminId };
            db.LeagueInfo.Add(league);
            await db.SaveChangesAsync();

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
                    NflWeekId = weekRow.Id, DateCreated = DateTimeOffset.UtcNow,
                });
                await db.SaveChangesAsync();
            }

            // Act — second run of seeder (simulates re-deploy)
            var seeder = new DemoDataSeeder(db, BuildUserManager(adminUser), BuildConfig());
            var exception = await Record.ExceptionAsync(() => seeder.SeedHistoricalWeeksAsync(league));

            // Assert — must not throw PK duplicate or any other exception
            Assert.Null(exception);
        });
    }
}
