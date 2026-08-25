using FourPlayWebApp.Server.Models;
using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Models.Identity;
using FourPlayWebApp.Server.Services.Repositories;
using FourPlayWebApp.Shared.Models.Data;
using FourPlayWebApp.Shared.Models.Enum;
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

    // ── DeleteLeagueAsync ──────────────────────────────────────────────────────

    // frizat: mirrors DemoDataSeeder.PurgeUnknownLeaguesAsync's explicit ordered RemoveRange —
    // every FK off LeagueInfo is DB-level Cascade already, but deleting explicitly (not relying
    // solely on cascade) keeps this correct against the in-memory provider too, which doesn't
    // enforce relational FK cascade the way real Postgres does.
    [Fact]
    public async Task DeleteLeagueAsync_RemovesLeagueAndAllDependentRows()
    {
        var factory = new DbContextFactoryStub(nameof(DeleteLeagueAsync_RemovesLeagueAndAllDependentRows));
        var seedDb = factory.CreateDbContext();

        var league = new LeagueInfo { LeagueName = "Doomed League", OwnerUserId = "owner-1" };
        var otherLeague = new LeagueInfo { LeagueName = "Untouched League", OwnerUserId = "owner-2" };
        seedDb.LeagueInfo.AddRange(league, otherLeague);
        await seedDb.SaveChangesAsync();

        seedDb.LeagueUserMapping.Add(new LeagueUserMapping { LeagueId = league.Id, UserId = "owner-1" });
        seedDb.LeagueUserMapping.Add(new LeagueUserMapping { LeagueId = otherLeague.Id, UserId = "owner-2" });
        seedDb.LeagueJuiceMapping.Add(new LeagueJuiceMapping {
            LeagueId = league.Id, Season = 2025, Juice = 13, JuiceDivisional = 10, JuiceConference = 6, WeeklyCost = 5,
        });
        seedDb.NflPicks.Add(new NflPicks { LeagueId = league.Id, UserId = "owner-1", Team = "BUF", NflWeek = 1, Season = 2025 });
        seedDb.CfbPicks.Add(new CfbPicks { LeagueId = league.Id, UserId = "owner-1", Team = "MICH", CfbSlateId = 1, Season = 2025 });
        seedDb.Invitations.Add(new Invitation { Email = "friend@example.com", LeagueId = league.Id, InvitedByUserId = "owner-1" });
        await seedDb.SaveChangesAsync();

        var repo = new LeagueRepository(factory);
        await repo.DeleteLeagueAsync(league.Id);

        var db = factory.CreateDbContext();
        Assert.False(await db.LeagueInfo.AnyAsync(l => l.Id == league.Id));
        Assert.False(await db.LeagueUserMapping.AnyAsync(m => m.LeagueId == league.Id));
        Assert.False(await db.LeagueJuiceMapping.AnyAsync(m => m.LeagueId == league.Id));
        Assert.False(await db.NflPicks.AnyAsync(p => p.LeagueId == league.Id));
        Assert.False(await db.CfbPicks.AnyAsync(p => p.LeagueId == league.Id));
        Assert.False(await db.Invitations.AnyAsync(i => i.LeagueId == league.Id));

        // Unrelated league and its own mapping survive untouched.
        Assert.True(await db.LeagueInfo.AnyAsync(l => l.Id == otherLeague.Id));
        Assert.True(await db.LeagueUserMapping.AnyAsync(m => m.LeagueId == otherLeague.Id));
    }

    // ── Soft-delete league membership ───────────────────────────────────────────
    // frizat: removing a member must keep their pick history for audit purposes — flip IsActive
    // instead of hard-deleting the LeagueUserMapping row.

    [Fact]
    public async Task RemoveLeagueUserMappingAsync_SetsInactive_DoesNotDeleteRow()
    {
        var factory = new DbContextFactoryStub(nameof(RemoveLeagueUserMappingAsync_SetsInactive_DoesNotDeleteRow));
        var seedDb = factory.CreateDbContext();
        seedDb.LeagueUserMapping.Add(new LeagueUserMapping { LeagueId = 1, UserId = "user-1" });
        await seedDb.SaveChangesAsync();

        var repo = new LeagueRepository(factory);
        await repo.RemoveLeagueUserMappingAsync(1, "user-1");

        var db = factory.CreateDbContext();
        var mapping = await db.LeagueUserMapping.SingleAsync(m => m.LeagueId == 1 && m.UserId == "user-1");
        Assert.False(mapping.IsActive);
        Assert.NotNull(mapping.RemovedAt);
    }

    // frizat: (LeagueId, UserId) is unique — a removed-then-re-added user must reactivate their
    // existing row, not insert a second one (would violate the unique index against real Postgres).
    [Fact]
    public async Task AddLeagueUserMappingAsync_ReactivatesExistingInactiveRow_InsteadOfInserting()
    {
        var factory = new DbContextFactoryStub(nameof(AddLeagueUserMappingAsync_ReactivatesExistingInactiveRow_InsteadOfInserting));
        var seedDb = factory.CreateDbContext();
        var originalJoinDate = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        seedDb.LeagueUserMapping.Add(new LeagueUserMapping {
            LeagueId = 1, UserId = "user-1", IsActive = false,
            RemovedAt = DateTimeOffset.UtcNow, DateCreated = originalJoinDate,
        });
        await seedDb.SaveChangesAsync();

        var repo = new LeagueRepository(factory);
        await repo.AddLeagueUserMappingAsync(new LeagueUserMapping { LeagueId = 1, UserId = "user-1" });

        var db = factory.CreateDbContext();
        var mappings = await db.LeagueUserMapping.Where(m => m.LeagueId == 1 && m.UserId == "user-1").ToListAsync();
        Assert.Single(mappings);
        Assert.True(mappings[0].IsActive);
        Assert.Null(mappings[0].RemovedAt);
        Assert.Equal(originalJoinDate, mappings[0].DateCreated); // original join date preserved, not clobbered
    }

    [Fact]
    public async Task GetLeagueUserMappingsAsync_ByLeagueId_ExcludesInactiveMembers()
    {
        var factory = new DbContextFactoryStub(nameof(GetLeagueUserMappingsAsync_ByLeagueId_ExcludesInactiveMembers));
        var seedDb = factory.CreateDbContext();
        seedDb.LeagueInfo.Add(new LeagueInfo { Id = 1, LeagueName = "L", OwnerUserId = "active-user" });
        seedDb.Users.AddRange(
            new ApplicationUser { Id = "active-user", UserName = "active-user" },
            new ApplicationUser { Id = "removed-user", UserName = "removed-user" });
        seedDb.LeagueUserMapping.Add(new LeagueUserMapping { LeagueId = 1, UserId = "active-user" });
        seedDb.LeagueUserMapping.Add(new LeagueUserMapping { LeagueId = 1, UserId = "removed-user", IsActive = false, RemovedAt = DateTimeOffset.UtcNow });
        await seedDb.SaveChangesAsync();

        var repo = new LeagueRepository(factory);
        var mappings = await repo.GetLeagueUserMappingsAsync(1);

        Assert.Single(mappings);
        Assert.Equal("active-user", mappings[0].UserId);
    }

    [Fact]
    public async Task GetLeagueUserMappingsAsync_ByUser_ExcludesInactiveMemberships()
    {
        var factory = new DbContextFactoryStub(nameof(GetLeagueUserMappingsAsync_ByUser_ExcludesInactiveMemberships));
        var seedDb = factory.CreateDbContext();
        seedDb.LeagueInfo.AddRange(
            new LeagueInfo { Id = 1, LeagueName = "L1", OwnerUserId = "user-1" },
            new LeagueInfo { Id = 2, LeagueName = "L2", OwnerUserId = "user-1" });
        seedDb.Users.Add(new ApplicationUser { Id = "user-1", UserName = "user-1" });
        seedDb.LeagueUserMapping.Add(new LeagueUserMapping { LeagueId = 1, UserId = "user-1" });
        seedDb.LeagueUserMapping.Add(new LeagueUserMapping { LeagueId = 2, UserId = "user-1", IsActive = false, RemovedAt = DateTimeOffset.UtcNow });
        await seedDb.SaveChangesAsync();

        var repo = new LeagueRepository(factory);
        var mappings = await repo.GetLeagueUserMappingsAsync(new ApplicationUser { Id = "user-1" });

        Assert.Single(mappings);
        Assert.Equal(1, mappings[0].LeagueId);
    }

    [Fact]
    public async Task GetLeagueMemberCountAsync_ExcludesInactiveMembers()
    {
        var factory = new DbContextFactoryStub(nameof(GetLeagueMemberCountAsync_ExcludesInactiveMembers));
        var seedDb = factory.CreateDbContext();
        seedDb.LeagueUserMapping.Add(new LeagueUserMapping { LeagueId = 1, UserId = "active-user" });
        seedDb.LeagueUserMapping.Add(new LeagueUserMapping { LeagueId = 1, UserId = "removed-user", IsActive = false, RemovedAt = DateTimeOffset.UtcNow });
        await seedDb.SaveChangesAsync();

        var repo = new LeagueRepository(factory);
        var count = await repo.GetLeagueMemberCountAsync(1);

        Assert.Equal(1, count);
    }

    // ── Season-aware member count (admin cost dashboard, frizat-fug) ────────────
    // frizat: "cost owed for season Y" must count members whose membership WINDOW overlapped
    // season Y's date range — not just currently-active members — so a past season's cost isn't
    // silently zeroed out by members who have since left. Window = [DateCreated, RemovedAt ?? now).

    private static async Task SeedNflSeasonAsync(DbContextFactoryStub factory, int season, DateTime start, DateTime end)
    {
        var db = factory.CreateDbContext();
        db.NflSeasonWeekConfigs.Add(new NflSeasonWeekConfig {
            Season = season, WeekId = 1, WeekLabel = "Week 1",
            WeekStartDatetime = start, WeekEndDatetime = end,
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task GetLeagueMemberCountAsync_SeasonAware_CountsMemberActiveDuringThatSeason_ExcludingLaterRemoval()
    {
        var factory = new DbContextFactoryStub(nameof(GetLeagueMemberCountAsync_SeasonAware_CountsMemberActiveDuringThatSeason_ExcludingLaterRemoval));
        await SeedNflSeasonAsync(factory, 2024, new DateTime(2024, 9, 1), new DateTime(2025, 2, 1));
        var seedDb = factory.CreateDbContext();
        // Joined before the 2024 season and removed after it ended — was present the whole season.
        seedDb.LeagueUserMapping.Add(new LeagueUserMapping {
            LeagueId = 1, UserId = "still-counted",
            DateCreated = new DateTimeOffset(2024, 8, 1, 0, 0, 0, TimeSpan.Zero),
            RemovedAt = new DateTimeOffset(2025, 3, 1, 0, 0, 0, TimeSpan.Zero), IsActive = false,
        });
        await seedDb.SaveChangesAsync();

        var repo = new LeagueRepository(factory);
        var count = await repo.GetLeagueMemberCountAsync(1, 2024, LeagueType.Nfl);

        Assert.Equal(1, count);
    }

    [Fact]
    public async Task GetLeagueMemberCountAsync_SeasonAware_ExcludesMemberWhoLeftEntirelyBeforeTheSeasonStarted()
    {
        var factory = new DbContextFactoryStub(nameof(GetLeagueMemberCountAsync_SeasonAware_ExcludesMemberWhoLeftEntirelyBeforeTheSeasonStarted));
        await SeedNflSeasonAsync(factory, 2024, new DateTime(2024, 9, 1), new DateTime(2025, 2, 1));
        var seedDb = factory.CreateDbContext();
        // Joined and left in 2023, well before the 2024 season window — must not count toward 2024.
        seedDb.LeagueUserMapping.Add(new LeagueUserMapping {
            LeagueId = 1, UserId = "left-before-season",
            DateCreated = new DateTimeOffset(2023, 8, 1, 0, 0, 0, TimeSpan.Zero),
            RemovedAt = new DateTimeOffset(2023, 12, 1, 0, 0, 0, TimeSpan.Zero), IsActive = false,
        });
        await seedDb.SaveChangesAsync();

        var repo = new LeagueRepository(factory);
        var count = await repo.GetLeagueMemberCountAsync(1, 2024, LeagueType.Nfl);

        Assert.Equal(0, count);
    }

    // /code-review: GetSeasonDateRangeAsync's CFB branch computed the season end as
    // WeekEndDate.ToDateTime(TimeOnly.MinValue) — midnight at the *start* of the last calendar
    // day, not end-of-day. A member joining later that same day (a real day, still within the
    // season by any reasonable reading of WeekEndDate) was incorrectly excluded, inconsistent
    // with CfbCurrentSlateService's TimeOnly.MaxValue convention for the same "end of this date"
    // concept elsewhere in the codebase.
    [Fact]
    public async Task GetLeagueMemberCountAsync_SeasonAware_Cfb_CountsMemberWhoJoinedLaterOnTheSeasonsLastCalendarDay()
    {
        var factory = new DbContextFactoryStub(nameof(GetLeagueMemberCountAsync_SeasonAware_Cfb_CountsMemberWhoJoinedLaterOnTheSeasonsLastCalendarDay));
        var seedDb = factory.CreateDbContext();
        seedDb.CfbSeasonWeekConfigs.Add(new CfbSeasonWeekConfig {
            Season = 2024, EspnWeekNumber = 1, IvLeagueWeekNumber = 1,
            WeekStartDate = new DateOnly(2024, 8, 20), WeekEndDate = new DateOnly(2025, 1, 15),
        });
        // Joined at 6pm UTC on the season's last calendar day — still that day, still in-season.
        seedDb.LeagueUserMapping.Add(new LeagueUserMapping {
            LeagueId = 1, UserId = "joined-late-on-last-day",
            DateCreated = new DateTimeOffset(2025, 1, 15, 18, 0, 0, TimeSpan.Zero),
        });
        await seedDb.SaveChangesAsync();

        var repo = new LeagueRepository(factory);
        var count = await repo.GetLeagueMemberCountAsync(1, 2024, LeagueType.Cfb);

        Assert.Equal(1, count);
    }

    [Fact]
    public async Task GetLeagueMemberCountAsync_SeasonAware_ExcludesMemberWhoJoinedAfterTheSeasonEnded()
    {
        var factory = new DbContextFactoryStub(nameof(GetLeagueMemberCountAsync_SeasonAware_ExcludesMemberWhoJoinedAfterTheSeasonEnded));
        await SeedNflSeasonAsync(factory, 2024, new DateTime(2024, 9, 1), new DateTime(2025, 2, 1));
        var seedDb = factory.CreateDbContext();
        // Joined in the 2025 season, after the 2024 window ended — must not count toward 2024.
        seedDb.LeagueUserMapping.Add(new LeagueUserMapping {
            LeagueId = 1, UserId = "joined-next-season",
            DateCreated = new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero),
        });
        await seedDb.SaveChangesAsync();

        var repo = new LeagueRepository(factory);
        var count = await repo.GetLeagueMemberCountAsync(1, 2024, LeagueType.Nfl);

        Assert.Equal(0, count);
    }

    [Fact]
    public async Task GetLeagueMemberCountAsync_SeasonAware_CountsCurrentlyActiveMemberWhoJoinedMidSeason()
    {
        var factory = new DbContextFactoryStub(nameof(GetLeagueMemberCountAsync_SeasonAware_CountsCurrentlyActiveMemberWhoJoinedMidSeason));
        await SeedNflSeasonAsync(factory, 2024, new DateTime(2024, 9, 1), new DateTime(2025, 2, 1));
        var seedDb = factory.CreateDbContext();
        // Joined mid-2024-season and never left — still active today.
        seedDb.LeagueUserMapping.Add(new LeagueUserMapping {
            LeagueId = 1, UserId = "joined-mid-season",
            DateCreated = new DateTimeOffset(2024, 10, 1, 0, 0, 0, TimeSpan.Zero),
        });
        await seedDb.SaveChangesAsync();

        var repo = new LeagueRepository(factory);
        var count = await repo.GetLeagueMemberCountAsync(1, 2024, LeagueType.Nfl);

        Assert.Equal(1, count);
    }

    [Fact]
    public async Task GetLeagueMemberCountsAsync_ReturnsCountsPerLeague_AcrossBothSports()
    {
        var factory = new DbContextFactoryStub(nameof(GetLeagueMemberCountsAsync_ReturnsCountsPerLeague_AcrossBothSports));
        await SeedNflSeasonAsync(factory, 2024, new DateTime(2024, 9, 1), new DateTime(2025, 2, 1));
        var seedDb = factory.CreateDbContext();
        seedDb.CfbSeasonWeekConfigs.Add(new CfbSeasonWeekConfig {
            Season = 2024, EspnWeekNumber = 1, IvLeagueWeekNumber = 1,
            WeekStartDate = new DateOnly(2024, 8, 20), WeekEndDate = new DateOnly(2025, 1, 15),
        });
        seedDb.LeagueInfo.AddRange(
            new LeagueInfo { Id = 1, LeagueName = "NFL League", OwnerUserId = "owner-1", LeagueType = LeagueType.Nfl },
            new LeagueInfo { Id = 2, LeagueName = "CFB League", OwnerUserId = "owner-2", LeagueType = LeagueType.Cfb });
        seedDb.LeagueUserMapping.Add(new LeagueUserMapping {
            LeagueId = 1, UserId = "nfl-member", DateCreated = new DateTimeOffset(2024, 9, 15, 0, 0, 0, TimeSpan.Zero),
        });
        seedDb.LeagueUserMapping.Add(new LeagueUserMapping {
            LeagueId = 2, UserId = "cfb-member-1", DateCreated = new DateTimeOffset(2024, 9, 1, 0, 0, 0, TimeSpan.Zero),
        });
        seedDb.LeagueUserMapping.Add(new LeagueUserMapping {
            LeagueId = 2, UserId = "cfb-member-2", DateCreated = new DateTimeOffset(2024, 9, 1, 0, 0, 0, TimeSpan.Zero),
        });
        await seedDb.SaveChangesAsync();

        var repo = new LeagueRepository(factory);
        var counts = await repo.GetLeagueMemberCountsAsync(2024);

        Assert.Equal(1, counts[1]);
        Assert.Equal(2, counts[2]);
    }

    [Fact]
    public async Task UserExistsInLeagueAsync_ReturnsFalse_ForInactiveMember()
    {
        var factory = new DbContextFactoryStub(nameof(UserExistsInLeagueAsync_ReturnsFalse_ForInactiveMember));
        var seedDb = factory.CreateDbContext();
        seedDb.LeagueUserMapping.Add(new LeagueUserMapping { LeagueId = 1, UserId = "removed-user", IsActive = false, RemovedAt = DateTimeOffset.UtcNow });
        await seedDb.SaveChangesAsync();

        var repo = new LeagueRepository(factory);
        var exists = await repo.UserExistsInLeagueAsync("removed-user", 1);

        Assert.False(exists);
    }
}
