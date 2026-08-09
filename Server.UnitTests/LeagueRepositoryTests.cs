using FourPlayWebApp.Server.Data;
using FourPlayWebApp.Server.Services.Repositories;
using FourPlayWebApp.Shared.Models.Data;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace FourPlayWebApp.Server.UnitTests;

// TDD, written RED first (frizat-pxy follow-on plan, Phase 1): LeagueRepository.UpsertAsync is
// AddNewNflSpreadsAsync renamed to satisfy the shared ISpreadRepository<NflSpreads> contract —
// behavior is unchanged (already-correct upsert-by-key), this just pins it under the new name and
// adds coverage for the new GetWeeksWithSpreadDataAsync query needed by the data-driven catch-up
// scheduler (Phase 2/3).
public class LeagueRepositoryTests
{
    private static ApplicationDbContext BuildDb(string name) =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(name)
            .Options);

    // LeagueRepository disposes its DbContext (`await using`) after every call, so the factory
    // must hand out a fresh context per call — all backed by the same named in-memory database —
    // rather than one shared instance the repo would dispose out from under a second call.
    private static IDbContextFactory<ApplicationDbContext> BuildFactory(string dbName)
    {
        var factory = Substitute.For<IDbContextFactory<ApplicationDbContext>>();
        factory.CreateDbContextAsync().Returns(_ => Task.FromResult(BuildDb(dbName)));
        return factory;
    }

    [Fact]
    public async Task UpsertAsync_NewWeek_Inserts()
    {
        var dbName = nameof(UpsertAsync_NewWeek_Inserts);
        var repo = new LeagueRepository(BuildFactory(dbName));

        await repo.UpsertAsync([
            new NflSpreads { Season = 2026, NflWeek = 3, HomeTeam = "A", AwayTeam = "B", HomeTeamSpread = -3.5, AwayTeamSpread = 3.5 },
        ]);

        await using var verifyDb = BuildDb(dbName);
        var saved = await verifyDb.NflSpreads.SingleAsync(s => s.Season == 2026 && s.NflWeek == 3);
        Assert.Equal(-3.5, saved.HomeTeamSpread);
    }

    [Fact]
    public async Task GetWeeksWithSpreadDataAsync_ReturnsSeasonWeekPairsWithSpreads()
    {
        var dbName = nameof(GetWeeksWithSpreadDataAsync_ReturnsSeasonWeekPairsWithSpreads);
        await using (var seedDb = BuildDb(dbName)) {
            seedDb.NflSpreads.Add(new NflSpreads { Season = 2026, NflWeek = 3, HomeTeam = "A", AwayTeam = "B" });
            await seedDb.SaveChangesAsync();
        }

        var repo = new LeagueRepository(BuildFactory(dbName));
        var weeksWithData = await repo.GetWeeksWithSpreadDataAsync();

        Assert.Contains((2026, 3), weeksWithData);
        Assert.DoesNotContain((2026, 4), weeksWithData);
    }
}
