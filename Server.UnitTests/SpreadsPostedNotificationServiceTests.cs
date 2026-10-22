using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Services;
using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Shared.Models.Data.Dtos;
using FourPlayWebApp.Shared.Models.Enum;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace FourPlayWebApp.Server.UnitTests;

public class SpreadsPostedNotificationServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 22, 14, 0, 0, TimeSpan.Zero);
    private static readonly DateTime LockedAnHourAgo = Now.UtcDateTime.AddHours(-1);

    private sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private readonly INotificationDispatcher _dispatcher = Substitute.For<INotificationDispatcher>();

    private SpreadsPostedNotificationService Build(IDbContextFactory<FourPlayWebApp.Server.Data.ApplicationDbContext> factory) =>
        new(factory, _dispatcher, new FakeTimeProvider(Now));

    // Seeds users plus their per-sport "spreads are up" setting.
    private static async Task Seed(string db, params (string UserId, LeagueType Sport, bool On)[] prefs)
    {
        await using var seed = SqliteTestDb.Open(db);
        foreach (var userId in prefs.Select(p => p.UserId).Distinct())
            await SqliteTestDb.SeedUser(seed, userId);
        seed.NotificationPreferences.AddRange(prefs.Select(p =>
            new NotificationPreferences { UserId = p.UserId, Sport = p.Sport, NotifySpreadsPosted = p.On }));
        await seed.SaveChangesAsync();
    }

    [Fact]
    public async Task NotifyAsync_PushesOnlyUsersWhoOptedInForThatSport_NamingTheWeekAndSport()
    {
        var db = nameof(NotifyAsync_PushesOnlyUsersWhoOptedInForThatSport_NamingTheWeekAndSport);
        await SqliteTestDb.WithDb(db, async factory =>
        {
            await Seed(db, ("alice", LeagueType.Nfl, true), ("bob", LeagueType.Nfl, false), ("carlos", LeagueType.Cfb, true));

            var sent = await Build(factory).NotifyAsync(LeagueType.Nfl, 2026, 8, "Week 8", LockedAnHourAgo);

            Assert.Equal(1, sent);
            await _dispatcher.Received(1).DispatchAsync("alice", Arg.Any<Func<NotificationPreferencesDto, bool>>(),
                Arg.Is<PushPayload>(p => p.Body.Contains("NFL Week 8 spreads are up") && p.Sport == LeagueType.Nfl && p.Url == "/picks"),
                Arg.Any<CancellationToken>());
            await _dispatcher.DidNotReceive().DispatchAsync("bob", Arg.Any<Func<NotificationPreferencesDto, bool>>(), Arg.Any<PushPayload>(), Arg.Any<CancellationToken>());
            await _dispatcher.DidNotReceive().DispatchAsync("carlos", Arg.Any<Func<NotificationPreferencesDto, bool>>(), Arg.Any<PushPayload>(), Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task NotifyAsync_ChecksTheSpreadsPostedSetting()
    {
        var db = nameof(NotifyAsync_ChecksTheSpreadsPostedSetting);
        await SqliteTestDb.WithDb(db, async factory =>
        {
            await Seed(db, ("alice", LeagueType.Cfb, true));
            Func<NotificationPreferencesDto, bool>? isEnabled = null;
            await _dispatcher.DispatchAsync("alice", Arg.Do<Func<NotificationPreferencesDto, bool>>(f => isEnabled = f), Arg.Any<PushPayload>(), Arg.Any<CancellationToken>());

            await Build(factory).NotifyAsync(LeagueType.Cfb, 2026, 9, "Week 9", LockedAnHourAgo);

            Assert.NotNull(isEnabled);
            Assert.True(isEnabled(new NotificationPreferencesDto { NotifySpreadsPosted = true }));
            Assert.False(isEnabled(new NotificationPreferencesDto { NotifyWeekResult = true }));
            await _dispatcher.Received(1).DispatchAsync("alice", Arg.Any<Func<NotificationPreferencesDto, bool>>(),
                Arg.Is<PushPayload>(p => p.Body.Contains("CFB Week 9 spreads are up") && p.Sport == LeagueType.Cfb), Arg.Any<CancellationToken>());
        });
    }

    // The spread job can run again for the same week (a retry, an admin re-run).
    [Fact]
    public async Task NotifyAsync_SameWeekTwice_PushesOnlyOnce()
    {
        var db = nameof(NotifyAsync_SameWeekTwice_PushesOnlyOnce);
        await SqliteTestDb.WithDb(db, async factory =>
        {
            await Seed(db, ("alice", LeagueType.Nfl, true));
            var service = Build(factory);

            await service.NotifyAsync(LeagueType.Nfl, 2026, 8, "Week 8", LockedAnHourAgo);
            var second = await service.NotifyAsync(LeagueType.Nfl, 2026, 8, "Week 8", LockedAnHourAgo);

            Assert.Equal(0, second);
            await _dispatcher.Received(1).DispatchAsync("alice", Arg.Any<Func<NotificationPreferencesDto, bool>>(), Arg.Any<PushPayload>(), Arg.Any<CancellationToken>());
        });
    }

    // Each sport's week is its own release.
    [Fact]
    public async Task NotifyAsync_SameWeekNumberInTheOtherSport_StillPushes()
    {
        var db = nameof(NotifyAsync_SameWeekNumberInTheOtherSport_StillPushes);
        await SqliteTestDb.WithDb(db, async factory =>
        {
            await Seed(db, ("alice", LeagueType.Nfl, true), ("alice", LeagueType.Cfb, true));
            var service = Build(factory);

            await service.NotifyAsync(LeagueType.Nfl, 2026, 8, "Week 8", LockedAnHourAgo);
            await service.NotifyAsync(LeagueType.Cfb, 2026, 8, "Week 8", LockedAnHourAgo);

            await _dispatcher.Received(2).DispatchAsync("alice", Arg.Any<Func<NotificationPreferencesDto, bool>>(), Arg.Any<PushPayload>(), Arg.Any<CancellationToken>());
        });
    }

    // A run long after the release (e.g. an admin re-running a week whose spreads went up days
    // ago, before this feature shipped) is old news — record it, don't push.
    [Fact]
    public async Task NotifyAsync_LongAfterTheRelease_RecordsWithoutPushing()
    {
        var db = nameof(NotifyAsync_LongAfterTheRelease_RecordsWithoutPushing);
        await SqliteTestDb.WithDb(db, async factory =>
        {
            await Seed(db, ("alice", LeagueType.Nfl, true));
            var service = Build(factory);
            var lockedLongAgo = Now.UtcDateTime - SpreadsPostedNotificationService.PushWindowAfterLock - TimeSpan.FromMinutes(1);

            var sent = await service.NotifyAsync(LeagueType.Nfl, 2026, 8, "Week 8", lockedLongAgo);

            Assert.Equal(0, sent);
            await _dispatcher.DidNotReceiveWithAnyArgs().DispatchAsync(default!, default!, default!, default);
            await using var check = SqliteTestDb.Open(db);
            Assert.True(await check.SpreadsPostedNotificationSent.AnyAsync(r => r.Sport == LeagueType.Nfl && r.Season == 2026 && r.Week == 8));
        });
    }

    // An admin can post a week's spreads before the scheduled release — that's still news.
    [Fact]
    public async Task NotifyAsync_PostedBeforeTheScheduledLock_Pushes()
    {
        var db = nameof(NotifyAsync_PostedBeforeTheScheduledLock_Pushes);
        await SqliteTestDb.WithDb(db, async factory =>
        {
            await Seed(db, ("alice", LeagueType.Nfl, true));

            var sent = await Build(factory).NotifyAsync(LeagueType.Nfl, 2026, 8, "Week 8", Now.UtcDateTime.AddDays(1));

            Assert.Equal(1, sent);
        });
    }

    [Fact]
    public async Task NotifyAsync_OneUsersPushFails_OthersStillGetIt()
    {
        var db = nameof(NotifyAsync_OneUsersPushFails_OthersStillGetIt);
        await SqliteTestDb.WithDb(db, async factory =>
        {
            await Seed(db, ("alice", LeagueType.Nfl, true), ("bob", LeagueType.Nfl, true));
            _dispatcher.DispatchAsync("alice", Arg.Any<Func<NotificationPreferencesDto, bool>>(), Arg.Any<PushPayload>(), Arg.Any<CancellationToken>())
                .ThrowsAsync(new InvalidOperationException("push service down"));

            var sent = await Build(factory).NotifyAsync(LeagueType.Nfl, 2026, 8, "Week 8", LockedAnHourAgo);

            Assert.Equal(1, sent);
            await _dispatcher.Received(1).DispatchAsync("bob", Arg.Any<Func<NotificationPreferencesDto, bool>>(), Arg.Any<PushPayload>(), Arg.Any<CancellationToken>());
        });
    }

    // The spreads are already saved — a database failure here must not fail the spread job.
    [Fact]
    public async Task NotifyAsync_WhenTheDatabaseFails_DoesNotThrow()
    {
        var factory = Substitute.For<IDbContextFactory<FourPlayWebApp.Server.Data.ApplicationDbContext>>();
        factory.CreateDbContextAsync().ThrowsAsync(new InvalidOperationException("db down"));

        var sent = await Build(factory).NotifyAsync(LeagueType.Nfl, 2026, 8, "Week 8", LockedAnHourAgo);

        Assert.Equal(0, sent);
    }
}
