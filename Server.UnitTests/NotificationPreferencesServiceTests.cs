using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Shared.Models.Enum;
using FourPlayWebApp.Server.Services;
using FourPlayWebApp.Shared.Models.Data.Dtos;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FourPlayWebApp.Server.UnitTests;

public class NotificationPreferencesServiceTests
{
    [Fact]
    public async Task GetAsync_WhenNoRowExists_ReturnsAllOffDefaults()
    {
        await SqliteTestDb.WithDb(nameof(GetAsync_WhenNoRowExists_ReturnsAllOffDefaults), async factory =>
        {
            await using (var seed = SqliteTestDb.Open(nameof(GetAsync_WhenNoRowExists_ReturnsAllOffDefaults)))
                await SqliteTestDb.SeedUser(seed, "alice");

            var result = await new NotificationPreferencesService(factory).GetAsync("alice", LeagueType.Nfl);

            Assert.False(result.NotifyMineBloodyDuringGame);
            Assert.False(result.NotifyMineBloodyAtFinal);
            Assert.False(result.NotifyMineCoveringDuringGame);
            Assert.False(result.NotifyMineCoveringAtFinal);
            Assert.False(result.NotifyOthersBloodyDuringGame);
            Assert.False(result.NotifyOthersBloodyAtFinal);
            Assert.False(result.NotifyOthersCoveringDuringGame);
            Assert.False(result.NotifyOthersCoveringAtFinal);
            Assert.False(result.NotifyWeekResult);
        });
    }

    [Fact]
    public async Task UpsertAsync_WhenNoRowExists_CreatesIt_AndReturnsWhatWasSaved()
    {
        var db = nameof(UpsertAsync_WhenNoRowExists_CreatesIt_AndReturnsWhatWasSaved);
        await SqliteTestDb.WithDb(db, async factory =>
        {
            await using (var seed = SqliteTestDb.Open(db))
                await SqliteTestDb.SeedUser(seed, "alice");

            var saved = await new NotificationPreferencesService(factory).UpsertAsync("alice", LeagueType.Nfl, new NotificationPreferencesDto
            {
                NotifyMineCoveringAtFinal = true,
                NotifyWeekResult = true,
            });

            Assert.True(saved.NotifyMineCoveringAtFinal);
            Assert.True(saved.NotifyWeekResult);
            Assert.False(saved.NotifyMineBloodyDuringGame);

            var reread = await new NotificationPreferencesService(factory).GetAsync("alice", LeagueType.Nfl);
            Assert.True(reread.NotifyMineCoveringAtFinal);
            Assert.True(reread.NotifyWeekResult);
        });
    }

    [Fact]
    public async Task UpsertAsync_WhenRowAlreadyExists_UpdatesInPlace_NotInsertingASecondRow()
    {
        var db = nameof(UpsertAsync_WhenRowAlreadyExists_UpdatesInPlace_NotInsertingASecondRow);
        await SqliteTestDb.WithDb(db, async factory =>
        {
            await using (var seed = SqliteTestDb.Open(db))
                await SqliteTestDb.SeedUser(seed, "alice");

            var service = new NotificationPreferencesService(factory);
            await service.UpsertAsync("alice", LeagueType.Nfl, new NotificationPreferencesDto { NotifyMineBloodyAtFinal = true });
            await service.UpsertAsync("alice", LeagueType.Nfl, new NotificationPreferencesDto { NotifyMineBloodyAtFinal = false, NotifyWeekResult = true });

            var result = await service.GetAsync("alice", LeagueType.Nfl);
            Assert.False(result.NotifyMineBloodyAtFinal);
            Assert.True(result.NotifyWeekResult);

            await using var verify = SqliteTestDb.Open(db);
            Assert.Equal(1, await verify.NotificationPreferences.CountAsync());
        });
    }

    [Fact]
    public async Task UpsertAsync_DoesNotAffectAnotherUsersPreferences()
    {
        var db = nameof(UpsertAsync_DoesNotAffectAnotherUsersPreferences);
        await SqliteTestDb.WithDb(db, async factory =>
        {
            await using (var seed = SqliteTestDb.Open(db))
            {
                await SqliteTestDb.SeedUser(seed, "alice");
                await SqliteTestDb.SeedUser(seed, "bob");
            }

            var service = new NotificationPreferencesService(factory);
            await service.UpsertAsync("alice", LeagueType.Nfl, new NotificationPreferencesDto { NotifyWeekResult = true });

            var bobsPrefs = await service.GetAsync("bob", LeagueType.Nfl);
            Assert.False(bobsPrefs.NotifyWeekResult);
        });
    }

    // LivePickTransitionService's cost-control short-circuit: must stay false until at least one
    // of the 8 LIVE toggles is on — NotifyWeekResult alone (Phase 2's toggle) must not count.
    [Fact]
    public async Task AnyLiveNotificationPreferenceEnabledAsync_IsFalse_WhenOnlyWeekResultIsEnabled()
    {
        var db = nameof(AnyLiveNotificationPreferenceEnabledAsync_IsFalse_WhenOnlyWeekResultIsEnabled);
        await SqliteTestDb.WithDb(db, async factory =>
        {
            await using (var seed = SqliteTestDb.Open(db))
                await SqliteTestDb.SeedUser(seed, "alice");

            var service = new NotificationPreferencesService(factory);
            await service.UpsertAsync("alice", LeagueType.Nfl, new NotificationPreferencesDto { NotifyWeekResult = true });

            Assert.False(await service.AnyLiveNotificationPreferenceEnabledAsync());
        });
    }

    [Fact]
    public async Task AnyLiveNotificationPreferenceEnabledAsync_IsTrue_WhenAnyOneLiveToggleIsOn()
    {
        var db = nameof(AnyLiveNotificationPreferenceEnabledAsync_IsTrue_WhenAnyOneLiveToggleIsOn);
        await SqliteTestDb.WithDb(db, async factory =>
        {
            await using (var seed = SqliteTestDb.Open(db))
                await SqliteTestDb.SeedUser(seed, "alice");

            var service = new NotificationPreferencesService(factory);
            await service.UpsertAsync("alice", LeagueType.Nfl, new NotificationPreferencesDto { NotifyOthersCoveringAtFinal = true });

            Assert.True(await service.AnyLiveNotificationPreferenceEnabledAsync());
        });
    }

    // Each app (NFL host / CFB host) has its own notification settings — "I may want NFL and not
    // college" — saving one sport must never change the other.
    [Fact]
    public async Task UpsertAsync_PerSport_SavingNflNeverChangesCollege()
    {
        var db = nameof(UpsertAsync_PerSport_SavingNflNeverChangesCollege);
        await SqliteTestDb.WithDb(db, async factory =>
        {
            await using (var seed = SqliteTestDb.Open(db))
                await SqliteTestDb.SeedUser(seed, "alice");
            var service = new NotificationPreferencesService(factory);

            await service.UpsertAsync("alice", LeagueType.Cfb, new NotificationPreferencesDto { NotifyWeekResult = true });
            await service.UpsertAsync("alice", LeagueType.Nfl, new NotificationPreferencesDto { NotifyWeekResult = false, NotifyMineBloodyDuringGame = true });

            Assert.True((await service.GetAsync("alice", LeagueType.Cfb)).NotifyWeekResult);
            Assert.False((await service.GetAsync("alice", LeagueType.Cfb)).NotifyMineBloodyDuringGame);
            Assert.False((await service.GetAsync("alice", LeagueType.Nfl)).NotifyWeekResult);
            Assert.True((await service.GetAsync("alice", LeagueType.Nfl)).NotifyMineBloodyDuringGame);
        });
    }

    [Fact]
    public async Task UpsertAsync_SavesTheSpreadsPostedSetting()
    {
        var db = nameof(UpsertAsync_SavesTheSpreadsPostedSetting);
        await SqliteTestDb.WithDb(db, async factory =>
        {
            await using (var seed = SqliteTestDb.Open(db))
                await SqliteTestDb.SeedUser(seed, "alice");

            var service = new NotificationPreferencesService(factory);
            await service.UpsertAsync("alice", LeagueType.Cfb, new NotificationPreferencesDto { NotifySpreadsPosted = true });

            Assert.True((await service.GetAsync("alice", LeagueType.Cfb)).NotifySpreadsPosted);
            Assert.False((await service.GetAsync("alice", LeagueType.Nfl)).NotifySpreadsPosted);
        });
    }

    // Only people actually playing that sport get "spreads are up": opted in for the sport AND an
    // active member of at least one league of that sport.
    [Fact]
    public async Task GetSpreadsPostedUserIdsAsync_ReturnsOptedInActiveMembersOfThatSportsLeagues()
    {
        var db = nameof(GetSpreadsPostedUserIdsAsync_ReturnsOptedInActiveMembersOfThatSportsLeagues);
        await SqliteTestDb.WithDb(db, async factory =>
        {
            string[] users = ["alice", "bob", "carlos", "dana", "eve", "frank"];
            await using (var seed = SqliteTestDb.Open(db))
            {
                foreach (var user in users)
                    await SqliteTestDb.SeedUser(seed, user);
                var nfl = new LeagueInfo { LeagueName = "NFL league", OwnerUserId = "alice", LeagueType = LeagueType.Nfl };
                var cfb = new LeagueInfo { LeagueName = "CFB league", OwnerUserId = "carlos", LeagueType = LeagueType.Cfb };
                seed.LeagueInfo.AddRange(nfl, cfb);
                await seed.SaveChangesAsync();
                seed.LeagueUserMapping.AddRange(
                    new LeagueUserMapping { LeagueId = nfl.Id, UserId = "alice" },
                    new LeagueUserMapping { LeagueId = nfl.Id, UserId = "bob" },
                    new LeagueUserMapping { LeagueId = cfb.Id, UserId = "carlos" },
                    new LeagueUserMapping { LeagueId = nfl.Id, UserId = "eve", IsActive = false },
                    new LeagueUserMapping { LeagueId = nfl.Id, UserId = "frank" });
                await seed.SaveChangesAsync();
            }

            var service = new NotificationPreferencesService(factory);
            var spreadsOn = new NotificationPreferencesDto { NotifySpreadsPosted = true };
            await service.UpsertAsync("alice", LeagueType.Nfl, spreadsOn);                                       // NFL member: yes
            await service.UpsertAsync("bob", LeagueType.Nfl, new NotificationPreferencesDto { NotifyWeekResult = true }); // not opted in
            await service.UpsertAsync("carlos", LeagueType.Cfb, spreadsOn);                                      // CFB member: yes
            await service.UpsertAsync("dana", LeagueType.Nfl, spreadsOn);                                        // in no league
            await service.UpsertAsync("eve", LeagueType.Nfl, spreadsOn);                                         // removed from her league
            await service.UpsertAsync("frank", LeagueType.Cfb, spreadsOn);                                       // CFB on, but only in an NFL league

            Assert.Equal(["alice"], await service.GetSpreadsPostedUserIdsAsync(LeagueType.Nfl));
            Assert.Equal(["carlos"], await service.GetSpreadsPostedUserIdsAsync(LeagueType.Cfb));
        });
    }
}
