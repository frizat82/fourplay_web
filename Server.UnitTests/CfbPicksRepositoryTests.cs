using FourPlayWebApp.Server.Services.Repositories;
using FourPlayWebApp.Shared.Models.Data;
using FourPlayWebApp.Shared.Models.Enum;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FourPlayWebApp.Server.UnitTests;

// frizat-bo1: CfbPicks.PickType was a raw string — migrated to reuse NflPicks' existing PickType
// enum (same values, same concept) rather than a CFB-only duplicate.
public class CfbPicksRepositoryTests
{
    [Fact]
    public async Task AddPicksAsync_PersistsPickTypeEnum_AndReadsItBack()
    {
        var factory = new DbContextFactoryStub(nameof(AddPicksAsync_PersistsPickTypeEnum_AndReadsItBack));
        var repo = new CfbPicksRepository(factory);

        await repo.AddPicksAsync([
            new CfbPicks { UserId = "u1", LeagueId = 1, CfbSlateId = 1, Team = "ALA", PickType = PickType.Over, Season = 2026 },
        ]);

        var saved = await factory.CreateDbContext().CfbPicks.SingleAsync(p => p.Team == "ALA");
        Assert.Equal(PickType.Over, saved.PickType);
    }
}
