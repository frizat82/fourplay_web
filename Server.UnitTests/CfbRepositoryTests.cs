using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Services.Repositories;
using FourPlayWebApp.Shared.Models;
using FourPlayWebApp.Shared.Models.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FourPlayWebApp.Server.UnitTests;

// TDD, written RED first (frizat-pxy follow-on plan, Phase 1): CfbRepository.UpsertAsync must
// dedupe by (CfbSlateId, HomeTeam) — a natural key mirroring NflSpreadsConfiguration's
// (Season, NflWeek, HomeTeam), not an ESPN-specific id — (no blind insert -> no duplicate rows on
// a re-fired spread job) and must preserve the original DateCreated on update, since that's the
// "line first posted" timestamp shown to users, not a re-fire timestamp.
public class CfbRepositoryTests
{
    [Fact]
    public async Task UpsertAsync_NewGame_Inserts()
    {
        var factory = new DbContextFactoryStub(nameof(UpsertAsync_NewGame_Inserts));
        var repo = new CfbRepository(factory);

        await repo.UpsertAsync([
            new CfbSpreads { CfbSlateId = 1, HomeTeam = "A", AwayTeam = "B", HomeTeamSpread = -3.5, AwayTeamSpread = 3.5 },
        ]);

        var saved = await factory.CreateDbContext().CfbSpreads.SingleAsync(s => s.CfbSlateId == 1 && s.HomeTeam == "A");
        Assert.Equal("A", saved.HomeTeam);
        Assert.Equal(-3.5, saved.HomeTeamSpread);
    }

    [Fact]
    public async Task UpsertAsync_ExistingGame_UpdatesInPlace_NoDuplicateRow()
    {
        var factory = new DbContextFactoryStub(nameof(UpsertAsync_ExistingGame_UpdatesInPlace_NoDuplicateRow));
        var repo = new CfbRepository(factory);

        await repo.UpsertAsync([
            new CfbSpreads { CfbSlateId = 1, HomeTeam = "A", AwayTeam = "B", HomeTeamSpread = -3.5, AwayTeamSpread = 3.5 },
        ]);

        // Re-fire with the same (CfbSlateId, HomeTeam) (e.g. a catch-up run re-processing an
        // already-saved week)
        await repo.UpsertAsync([
            new CfbSpreads { CfbSlateId = 1, HomeTeam = "A", AwayTeam = "B", HomeTeamSpread = -4.0, AwayTeamSpread = 4.0 },
        ]);

        var all = await factory.CreateDbContext().CfbSpreads.Where(s => s.CfbSlateId == 1 && s.HomeTeam == "A").ToListAsync();
        Assert.Single(all);
        Assert.Equal(-4.0, all[0].HomeTeamSpread);
    }

    [Fact]
    public async Task UpsertAsync_ExistingGame_PreservesOriginalDateCreated()
    {
        var factory = new DbContextFactoryStub(nameof(UpsertAsync_ExistingGame_PreservesOriginalDateCreated));
        var repo = new CfbRepository(factory);
        var originalPostTime = new DateTimeOffset(2026, 9, 1, 9, 0, 0, TimeSpan.Zero);

        await repo.UpsertAsync([
            new CfbSpreads { CfbSlateId = 1, HomeTeam = "A", AwayTeam = "B", DateCreated = originalPostTime },
        ]);

        await repo.UpsertAsync([
            new CfbSpreads { CfbSlateId = 1, HomeTeam = "A", AwayTeam = "B", HomeTeamSpread = -1, DateCreated = DateTimeOffset.UtcNow },
        ]);

        var saved = await factory.CreateDbContext().CfbSpreads.SingleAsync(s => s.CfbSlateId == 1 && s.HomeTeam == "A");
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
        seedDb.CfbSpreads.Add(new CfbSpreads { CfbSlateId = 1, HomeTeam = "A", AwayTeam = "B" });
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
        seedDb.CfbSpreads.Add(new CfbSpreads { CfbSlateId = 1, HomeTeam = "A", AwayTeam = "B" });
        await seedDb.SaveChangesAsync();

        var repo = new CfbRepository(factory);
        var weeksWithData = await repo.GetWeeksWithSpreadDataAsync();

        Assert.Contains((2026, 3), weeksWithData);
        Assert.DoesNotContain((2026, 4), weeksWithData);
    }

    // GetAllSlatesAsync mirrors the existing all-seasons GetAllWeekConfigsAsync pattern —
    // added so CfbCurrentSlateService can resolve the current slate across every season in
    // the DB instead of a single hardcoded ConfiguredSeason (frizat plan: wobbly-chasing-lynx).
    [Fact]
    public async Task GetAllSlatesAsync_ReturnsSlatesAcrossEverySeason_OrderedBySeasonThenSlateNumber()
    {
        var factory = new DbContextFactoryStub(nameof(GetAllSlatesAsync_ReturnsSlatesAcrossEverySeason_OrderedBySeasonThenSlateNumber));
        var seedDb = factory.CreateDbContext();
        seedDb.CfbSlates.Add(new CfbSlates { Id = 1, Season = 2026, SlateNumber = 2, Label = "Slate 2", SlateType = "RegularSeason" });
        seedDb.CfbSlates.Add(new CfbSlates { Id = 2, Season = 2025, SlateNumber = 4, Label = "Slate 4", SlateType = "Championship" });
        seedDb.CfbSlates.Add(new CfbSlates { Id = 3, Season = 2026, SlateNumber = 1, Label = "Slate 1", SlateType = "RegularSeason" });
        await seedDb.SaveChangesAsync();
        var repo = new CfbRepository(factory);

        var all = (await repo.GetAllSlatesAsync()).ToList();

        Assert.Equal(3, all.Count);
        Assert.Equal([(2025, 4), (2026, 1), (2026, 2)], all.Select(s => (s.Season, s.SlateNumber)));
    }

    // frizat-b2y: CapturedAtUtc changed from DateTime to DateTimeOffset for unambiguous instant
    // representation, matching DateCreated/UpdatedAt elsewhere in this codebase. Note: Postgres's
    // timestamptz (what this column maps to either way) always normalizes to UTC and never
    // preserves an arbitrary input offset — DateTimeOffset.Equals compares by absolute instant,
    // not literal offset, so a non-UTC input still round-trips correctly here without asserting
    // (falsely) that the original -5 offset itself survives storage.
    [Fact]
    public async Task AddRankingsAsync_PreservesCapturedAtUtcInstant()
    {
        var factory = new DbContextFactoryStub(nameof(AddRankingsAsync_PreservesCapturedAtUtcInstant));
        var repo = new CfbRepository(factory);
        var capturedAt = new DateTimeOffset(2026, 8, 26, 9, 0, 0, TimeSpan.FromHours(-5));

        await repo.AddRankingsAsync([
            new CfbRanking { Season = 2026, EspnWeekNumber = 1, EspnEventId = 1, TeamAbbreviation = "ALA", CuratedRank = 3, CapturedAtUtc = capturedAt },
        ]);

        var saved = await factory.CreateDbContext().CfbRankings.SingleAsync(r => r.TeamAbbreviation == "ALA");
        Assert.Equal(capturedAt, saved.CapturedAtUtc);
    }

    [Fact]
    public async Task GetLatestRankingsForWeekAsync_ReturnsOnlyMatchingSeasonAndWeek()
    {
        var factory = new DbContextFactoryStub(nameof(GetLatestRankingsForWeekAsync_ReturnsOnlyMatchingSeasonAndWeek));
        var repo = new CfbRepository(factory);

        await repo.AddRankingsAsync([
            new CfbRanking { Season = 2026, EspnWeekNumber = 1, EspnEventId = 1, TeamAbbreviation = "ALA", CuratedRank = 3 },
            new CfbRanking { Season = 2026, EspnWeekNumber = 2, EspnEventId = 2, TeamAbbreviation = "UGA", CuratedRank = 1 }, // different week
            new CfbRanking { Season = 2025, EspnWeekNumber = 1, EspnEventId = 3, TeamAbbreviation = "ORE", CuratedRank = 2 }, // different season
        ]);

        var result = await repo.GetLatestRankingsForWeekAsync(2026, 1);

        Assert.Single(result);
        Assert.Equal(3, result["ALA"]);
    }

    [Fact]
    public async Task AddRankingsAsync_UpsertsInPlace_WhenTheSameTeamWeekIsCapturedAgain()
    {
        // CfbRankingCaptureJob's earlier run and CfbSpreadJob's later run both capture the same
        // team-week. AddRankingsAsync must overwrite the existing row (rank doesn't change once
        // captured for a week) rather than appending a second row and violating the unique index
        // on (Season, EspnWeekNumber, TeamAbbreviation).
        var factory = new DbContextFactoryStub(nameof(AddRankingsAsync_UpsertsInPlace_WhenTheSameTeamWeekIsCapturedAgain));
        var repo = new CfbRepository(factory);
        var earlier = new DateTimeOffset(2026, 8, 25, 9, 0, 0, TimeSpan.Zero);
        var later = new DateTimeOffset(2026, 8, 27, 9, 0, 0, TimeSpan.Zero);

        await repo.AddRankingsAsync([
            new CfbRanking { Season = 2026, EspnWeekNumber = 1, EspnEventId = 1, TeamAbbreviation = "ALA", CuratedRank = 5, CapturedAtUtc = earlier },
        ]);
        await repo.AddRankingsAsync([
            new CfbRanking { Season = 2026, EspnWeekNumber = 1, EspnEventId = 2, TeamAbbreviation = "ALA", CuratedRank = 3, CapturedAtUtc = later },
        ]);

        var all = await factory.CreateDbContext().CfbRankings.Where(r => r.TeamAbbreviation == "ALA").ToListAsync();
        Assert.Single(all);
        Assert.Equal(3, all[0].CuratedRank);
        Assert.Equal(2, all[0].EspnEventId);

        var result = await repo.GetLatestRankingsForWeekAsync(2026, 1);
        Assert.Equal(3, result["ALA"]);
    }

    // frizat-bo1: CfbScores.GameStatus was a raw string ("StatusFinal", "StatusInProgress", ...) —
    // migrated to reuse the existing TypeName enum (the same one ESPN status parsing already uses
    // everywhere else) rather than either a loose string or a brand-new duplicate enum.
    [Fact]
    public async Task UpsertCfbScoresAsync_PersistsGameStatusEnum_AndReadsItBack()
    {
        var factory = new DbContextFactoryStub(nameof(UpsertCfbScoresAsync_PersistsGameStatusEnum_AndReadsItBack));
        var repo = new CfbRepository(factory);

        await repo.UpsertCfbScoresAsync([
            new CfbScores { CfbSlateId = 1, HomeTeam = "A", AwayTeam = "B", HomeTeamScore = 21, AwayTeamScore = 14, GameStatus = TypeName.StatusFinal },
        ]);

        var saved = (await repo.GetScoresForSlateAsync(1)).Single();
        Assert.Equal(TypeName.StatusFinal, saved.GameStatus);
    }
}
