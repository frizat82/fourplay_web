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

    // frizat-xbq: the commissioner count reads ONLY user ids (never a team), scoped to one
    // league + slate, one entry per submitted pick.
    [Fact]
    public async Task GetCfbPickUserIdsAsync_ReturnsOneUserIdPerPick_ScopedToLeagueAndSlate()
    {
        var factory = new DbContextFactoryStub(nameof(GetCfbPickUserIdsAsync_ReturnsOneUserIdPerPick_ScopedToLeagueAndSlate));
        var repo = new CfbPicksRepository(factory);
        await repo.AddPicksAsync([
            new CfbPicks { UserId = "u1", LeagueId = 1, CfbSlateId = 7, Team = "ALA", PickType = PickType.Spread, Season = 2026 },
            new CfbPicks { UserId = "u1", LeagueId = 1, CfbSlateId = 7, Team = "OSU", PickType = PickType.Spread, Season = 2026 },
            new CfbPicks { UserId = "u2", LeagueId = 1, CfbSlateId = 7, Team = "UGA", PickType = PickType.Over, Season = 2026 },
            new CfbPicks { UserId = "u3", LeagueId = 2, CfbSlateId = 7, Team = "TEX", PickType = PickType.Spread, Season = 2026 }, // other league
            new CfbPicks { UserId = "u4", LeagueId = 1, CfbSlateId = 8, Team = "MICH", PickType = PickType.Spread, Season = 2026 }, // other slate
        ]);

        var ids = await repo.GetCfbPickUserIdsAsync(1, 7);

        Assert.Equal(["u1", "u1", "u2"], ids.Order());
    }
}
