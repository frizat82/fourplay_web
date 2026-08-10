using FourPlayWebApp.Server.Services.Repositories;
using FourPlayWebApp.Shared.Models.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FourPlayWebApp.Server.UnitTests;

// TDD, written RED first (frizat-pxy follow-on plan, Phase 1): LeagueRepository.UpsertAsync is
// AddNewNflSpreadsAsync renamed to satisfy the shared ISpreadRepository<NflSpreads> contract —
// behavior is unchanged (already-correct upsert-by-key), this just pins it under the new name and
// adds coverage for the new GetWeeksWithSpreadDataAsync query needed by the data-driven catch-up
// scheduler (Phase 2/3).
public class LeagueRepositoryTests
{
    [Fact]
    public async Task UpsertAsync_NewWeek_Inserts()
    {
        var factory = new DbContextFactoryStub(nameof(UpsertAsync_NewWeek_Inserts));
        var repo = new LeagueRepository(factory);

        await repo.UpsertAsync([
            new NflSpreads { Season = 2026, NflWeek = 3, HomeTeam = "A", AwayTeam = "B", HomeTeamSpread = -3.5, AwayTeamSpread = 3.5 },
        ]);

        var saved = await factory.CreateDbContext().NflSpreads.SingleAsync(s => s.Season == 2026 && s.NflWeek == 3);
        Assert.Equal(-3.5, saved.HomeTeamSpread);
    }

    [Fact]
    public async Task GetWeeksWithSpreadDataAsync_ReturnsSeasonWeekPairsWithSpreads()
    {
        var factory = new DbContextFactoryStub(nameof(GetWeeksWithSpreadDataAsync_ReturnsSeasonWeekPairsWithSpreads));
        var seedDb = factory.CreateDbContext();
        seedDb.NflSpreads.Add(new NflSpreads { Season = 2026, NflWeek = 3, HomeTeam = "A", AwayTeam = "B" });
        await seedDb.SaveChangesAsync();

        var repo = new LeagueRepository(factory);
        var weeksWithData = await repo.GetWeeksWithSpreadDataAsync();

        Assert.Contains((2026, 3), weeksWithData);
        Assert.DoesNotContain((2026, 4), weeksWithData);
    }
}
