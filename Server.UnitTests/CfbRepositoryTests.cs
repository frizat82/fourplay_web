using FourPlayWebApp.Server.Data;
using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Services.Repositories;
using FourPlayWebApp.Shared.Models.Data;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace FourPlayWebApp.Server.UnitTests;

// TDD, written RED first (frizat-pxy follow-on plan, Phase 1): CfbRepository.UpsertAsync must
// dedupe by EspnEventId (no blind insert -> no duplicate rows on a re-fired spread job) and must
// preserve the original DateCreated on update, since that's the "line first posted" timestamp
// shown to users, not a re-fire timestamp.
public class CfbRepositoryTests
{
    private static ApplicationDbContext BuildDb(string name) =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(name)
            .Options);

    // CfbRepository disposes its DbContext (`await using`) after every call, so the factory must
    // hand out a fresh context per call — all backed by the same named in-memory database — rather
    // than one shared instance the repo would dispose out from under a second call.
    private static IDbContextFactory<ApplicationDbContext> BuildFactory(string dbName)
    {
        var factory = Substitute.For<IDbContextFactory<ApplicationDbContext>>();
        factory.CreateDbContextAsync().Returns(_ => Task.FromResult(BuildDb(dbName)));
        return factory;
    }

    [Fact]
    public async Task UpsertAsync_NewEspnEventId_Inserts()
    {
        var dbName = nameof(UpsertAsync_NewEspnEventId_Inserts);
        var repo = new CfbRepository(BuildFactory(dbName));

        await repo.UpsertAsync([
            new CfbSpreads { CfbSlateId = 1, EspnEventId = 100, HomeTeam = "A", AwayTeam = "B", HomeTeamSpread = -3.5, AwayTeamSpread = 3.5 },
        ]);

        await using var verifyDb = BuildDb(dbName);
        var saved = await verifyDb.CfbSpreads.SingleAsync(s => s.EspnEventId == 100);
        Assert.Equal("A", saved.HomeTeam);
        Assert.Equal(-3.5, saved.HomeTeamSpread);
    }

    [Fact]
    public async Task UpsertAsync_ExistingEspnEventId_UpdatesInPlace_NoDuplicateRow()
    {
        var dbName = nameof(UpsertAsync_ExistingEspnEventId_UpdatesInPlace_NoDuplicateRow);
        var repo = new CfbRepository(BuildFactory(dbName));

        await repo.UpsertAsync([
            new CfbSpreads { CfbSlateId = 1, EspnEventId = 100, HomeTeam = "A", AwayTeam = "B", HomeTeamSpread = -3.5, AwayTeamSpread = 3.5 },
        ]);

        // Re-fire with the same EspnEventId (e.g. a catch-up run re-processing an already-saved week)
        await repo.UpsertAsync([
            new CfbSpreads { CfbSlateId = 1, EspnEventId = 100, HomeTeam = "A", AwayTeam = "B", HomeTeamSpread = -4.0, AwayTeamSpread = 4.0 },
        ]);

        await using var verifyDb = BuildDb(dbName);
        var all = await verifyDb.CfbSpreads.Where(s => s.EspnEventId == 100).ToListAsync();
        Assert.Single(all);
        Assert.Equal(-4.0, all[0].HomeTeamSpread);
    }

    [Fact]
    public async Task UpsertAsync_ExistingEspnEventId_PreservesOriginalDateCreated()
    {
        var dbName = nameof(UpsertAsync_ExistingEspnEventId_PreservesOriginalDateCreated);
        var repo = new CfbRepository(BuildFactory(dbName));
        var originalPostTime = new DateTimeOffset(2026, 9, 1, 9, 0, 0, TimeSpan.Zero);

        await repo.UpsertAsync([
            new CfbSpreads { CfbSlateId = 1, EspnEventId = 100, HomeTeam = "A", AwayTeam = "B", DateCreated = originalPostTime },
        ]);

        await repo.UpsertAsync([
            new CfbSpreads { CfbSlateId = 1, EspnEventId = 100, HomeTeam = "A", AwayTeam = "B", HomeTeamSpread = -1, DateCreated = DateTimeOffset.UtcNow },
        ]);

        await using var verifyDb = BuildDb(dbName);
        var saved = await verifyDb.CfbSpreads.SingleAsync(s => s.EspnEventId == 100);
        Assert.Equal(originalPostTime, saved.DateCreated);
    }

    [Fact]
    public async Task GetWeeksWithSpreadDataAsync_ReturnsSeasonSlateNumberPairsWithSpreads()
    {
        var dbName = nameof(GetWeeksWithSpreadDataAsync_ReturnsSeasonSlateNumberPairsWithSpreads);
        await using (var seedDb = BuildDb(dbName)) {
            seedDb.CfbSlates.Add(new CfbSlates { Id = 1, Season = 2026, SlateNumber = 3, Label = "Week 3", SlateType = "RegularSeason" });
            seedDb.CfbSlates.Add(new CfbSlates { Id = 2, Season = 2026, SlateNumber = 4, Label = "Week 4", SlateType = "RegularSeason" });
            seedDb.CfbSpreads.Add(new CfbSpreads { CfbSlateId = 1, EspnEventId = 100, HomeTeam = "A", AwayTeam = "B" });
            await seedDb.SaveChangesAsync();
        }

        var repo = new CfbRepository(BuildFactory(dbName));
        var weeksWithData = await repo.GetWeeksWithSpreadDataAsync();

        Assert.Contains((2026, 3), weeksWithData);
        Assert.DoesNotContain((2026, 4), weeksWithData);
    }
}
