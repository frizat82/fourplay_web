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
}
