using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Services.Repositories;
using FourPlayWebApp.Shared.Models.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FourPlayWebApp.Server.UnitTests;

// TDD, written RED first (frizat-pxy follow-on plan, Phase 1): CfbRepository.UpsertAsync must
// dedupe by EspnEventId (no blind insert -> no duplicate rows on a re-fired spread job) and must
// preserve the original DateCreated on update, since that's the "line first posted" timestamp
// shown to users, not a re-fire timestamp.
public class CfbRepositoryTests
{
    [Fact]
    public async Task UpsertAsync_NewEspnEventId_Inserts()
    {
        var factory = new DbContextFactoryStub(nameof(UpsertAsync_NewEspnEventId_Inserts));
        var repo = new CfbRepository(factory);

        await repo.UpsertAsync([
            new CfbSpreads { CfbSlateId = 1, EspnEventId = 100, HomeTeam = "A", AwayTeam = "B", HomeTeamSpread = -3.5, AwayTeamSpread = 3.5 },
        ]);

        var saved = await factory.CreateDbContext().CfbSpreads.SingleAsync(s => s.EspnEventId == 100);
        Assert.Equal("A", saved.HomeTeam);
        Assert.Equal(-3.5, saved.HomeTeamSpread);
    }

    [Fact]
    public async Task UpsertAsync_ExistingEspnEventId_UpdatesInPlace_NoDuplicateRow()
    {
        var factory = new DbContextFactoryStub(nameof(UpsertAsync_ExistingEspnEventId_UpdatesInPlace_NoDuplicateRow));
        var repo = new CfbRepository(factory);

        await repo.UpsertAsync([
            new CfbSpreads { CfbSlateId = 1, EspnEventId = 100, HomeTeam = "A", AwayTeam = "B", HomeTeamSpread = -3.5, AwayTeamSpread = 3.5 },
        ]);

        // Re-fire with the same EspnEventId (e.g. a catch-up run re-processing an already-saved week)
        await repo.UpsertAsync([
            new CfbSpreads { CfbSlateId = 1, EspnEventId = 100, HomeTeam = "A", AwayTeam = "B", HomeTeamSpread = -4.0, AwayTeamSpread = 4.0 },
        ]);

        var all = await factory.CreateDbContext().CfbSpreads.Where(s => s.EspnEventId == 100).ToListAsync();
        Assert.Single(all);
        Assert.Equal(-4.0, all[0].HomeTeamSpread);
    }

    [Fact]
    public async Task UpsertAsync_ExistingEspnEventId_PreservesOriginalDateCreated()
    {
        var factory = new DbContextFactoryStub(nameof(UpsertAsync_ExistingEspnEventId_PreservesOriginalDateCreated));
        var repo = new CfbRepository(factory);
        var originalPostTime = new DateTimeOffset(2026, 9, 1, 9, 0, 0, TimeSpan.Zero);

        await repo.UpsertAsync([
            new CfbSpreads { CfbSlateId = 1, EspnEventId = 100, HomeTeam = "A", AwayTeam = "B", DateCreated = originalPostTime },
        ]);

        await repo.UpsertAsync([
            new CfbSpreads { CfbSlateId = 1, EspnEventId = 100, HomeTeam = "A", AwayTeam = "B", HomeTeamSpread = -1, DateCreated = DateTimeOffset.UtcNow },
        ]);

        var saved = await factory.CreateDbContext().CfbSpreads.SingleAsync(s => s.EspnEventId == 100);
        Assert.Equal(originalPostTime, saved.DateCreated);
    }

    // frizat-2lc, moved down to the repository per /simplify's altitude review: guarding only in
    // CfbSlateSeederJob protected that one caller, but left CfbRepository.DeleteSlatesAsync itself
    // unguarded for any future caller. The guard now lives where the destructive RemoveRange
    // actually happens, so it can't be bypassed by a caller that forgets to check first.
    [Fact]
    public async Task DeleteSlatesAsync_SlateHasDependentSpread_SkipsDeleteAndReturnsFalse()
    {
        var factory = new DbContextFactoryStub(nameof(DeleteSlatesAsync_SlateHasDependentSpread_SkipsDeleteAndReturnsFalse));
        var seedDb = factory.CreateDbContext();
        var slate = new CfbSlates { Id = 1, Season = 2026, SlateNumber = 3, Label = "Week 3", SlateType = "RegularSeason" };
        seedDb.CfbSlates.Add(slate);
        seedDb.CfbSpreads.Add(new CfbSpreads { CfbSlateId = 1, EspnEventId = 100, HomeTeam = "A", AwayTeam = "B" });
        await seedDb.SaveChangesAsync();
        var repo = new CfbRepository(factory);

        var deleted = await repo.DeleteSlatesAsync([slate]);

        Assert.False(deleted);
        Assert.True(await factory.CreateDbContext().CfbSlates.AnyAsync(s => s.Id == 1));
    }

    [Fact]
    public async Task DeleteSlatesAsync_NoDependentData_DeletesAndReturnsTrue()
    {
        var factory = new DbContextFactoryStub(nameof(DeleteSlatesAsync_NoDependentData_DeletesAndReturnsTrue));
        var seedDb = factory.CreateDbContext();
        var slate = new CfbSlates { Id = 1, Season = 2026, SlateNumber = 3, Label = "Week 3", SlateType = "RegularSeason" };
        seedDb.CfbSlates.Add(slate);
        await seedDb.SaveChangesAsync();
        var repo = new CfbRepository(factory);

        var deleted = await repo.DeleteSlatesAsync([slate]);

        Assert.True(deleted);
        Assert.False(await factory.CreateDbContext().CfbSlates.AnyAsync(s => s.Id == 1));
    }

    [Fact]
    public async Task GetWeeksWithSpreadDataAsync_ReturnsSeasonSlateNumberPairsWithSpreads()
    {
        var factory = new DbContextFactoryStub(nameof(GetWeeksWithSpreadDataAsync_ReturnsSeasonSlateNumberPairsWithSpreads));
        var seedDb = factory.CreateDbContext();
        seedDb.CfbSlates.Add(new CfbSlates { Id = 1, Season = 2026, SlateNumber = 3, Label = "Week 3", SlateType = "RegularSeason" });
        seedDb.CfbSlates.Add(new CfbSlates { Id = 2, Season = 2026, SlateNumber = 4, Label = "Week 4", SlateType = "RegularSeason" });
        seedDb.CfbSpreads.Add(new CfbSpreads { CfbSlateId = 1, EspnEventId = 100, HomeTeam = "A", AwayTeam = "B" });
        await seedDb.SaveChangesAsync();

        var repo = new CfbRepository(factory);
        var weeksWithData = await repo.GetWeeksWithSpreadDataAsync();

        Assert.Contains((2026, 3), weeksWithData);
        Assert.DoesNotContain((2026, 4), weeksWithData);
    }
}
