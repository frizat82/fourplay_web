using FourPlayWebApp.Server.Services;
using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Server.Services.Repositories.Interfaces;
using FourPlayWebApp.Shared.Models.Data.Dtos;
using FourPlayWebApp.Shared.Models.Enum;
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

    private readonly ILeagueRepository _repo = Substitute.For<ILeagueRepository>();
    private readonly INotificationPreferencesService _preferences = Substitute.For<INotificationPreferencesService>();
    private readonly INotificationDispatcher _dispatcher = Substitute.For<INotificationDispatcher>();

    public SpreadsPostedNotificationServiceTests()
    {
        _repo.TryRecordSpreadsPostedNotificationAsync(default, default, default).ReturnsForAnyArgs(true);
        _preferences.GetSpreadsPostedUserIdsAsync(LeagueType.Nfl).Returns(["alice", "bob"]);
        _preferences.GetSpreadsPostedUserIdsAsync(LeagueType.Cfb).Returns(["carlos"]);
    }

    private SpreadsPostedNotificationService Build() => new(_repo, _preferences, _dispatcher, new FakeTimeProvider(Now));

    [Theory]
    [InlineData(LeagueType.Nfl, "alice", "NFL Week 8 spreads are up")]
    [InlineData(LeagueType.Cfb, "carlos", "CFB Week 8 spreads are up")]
    public async Task NotifyAsync_PushesThatSportsOptedInUsers_NamingTheSportAndWeek(LeagueType sport, string userId, string body)
    {
        await Build().NotifyAsync(sport, 2026, 8, "Week 8", LockedAnHourAgo);

        await _dispatcher.Received(1).DispatchAsync(userId, Arg.Any<Func<NotificationPreferencesDto, bool>>(),
            Arg.Is<PushPayload>(p => p.Body.Contains(body) && p.Sport == sport && p.Url == "/picks"), Arg.Any<CancellationToken>());
        await _preferences.DidNotReceive().GetSpreadsPostedUserIdsAsync(sport == LeagueType.Nfl ? LeagueType.Cfb : LeagueType.Nfl);
    }

    [Fact]
    public async Task NotifyAsync_ChecksTheSpreadsPostedSetting()
    {
        Func<NotificationPreferencesDto, bool>? isEnabled = null;
        await _dispatcher.DispatchAsync("carlos", Arg.Do<Func<NotificationPreferencesDto, bool>>(f => isEnabled = f), Arg.Any<PushPayload>(), Arg.Any<CancellationToken>());

        await Build().NotifyAsync(LeagueType.Cfb, 2026, 9, "Week 9", LockedAnHourAgo);

        Assert.NotNull(isEnabled);
        Assert.True(isEnabled(new NotificationPreferencesDto { NotifySpreadsPosted = true }));
        Assert.False(isEnabled(new NotificationPreferencesDto { NotifyWeekResult = true }));
    }

    // The spread job can run again for the same week (a retry, an admin re-run).
    [Fact]
    public async Task NotifyAsync_WeekAlreadyNotified_PushesNothing()
    {
        _repo.TryRecordSpreadsPostedNotificationAsync(LeagueType.Nfl, 2026, 8).Returns(false);

        await Build().NotifyAsync(LeagueType.Nfl, 2026, 8, "Week 8", LockedAnHourAgo);

        await _dispatcher.DidNotReceiveWithAnyArgs().DispatchAsync(default!, default!, default!, default);
    }

    // A run long after the release (e.g. an admin re-running a week whose spreads went up days
    // ago, before this feature shipped) is old news — record it, don't push.
    [Fact]
    public async Task NotifyAsync_LongAfterTheRelease_RecordsWithoutPushing()
    {
        var lockedLongAgo = Now.UtcDateTime - SpreadsPostedNotificationService.PushWindowAfterLock - TimeSpan.FromMinutes(1);

        await Build().NotifyAsync(LeagueType.Nfl, 2026, 8, "Week 8", lockedLongAgo);

        await _repo.Received(1).TryRecordSpreadsPostedNotificationAsync(LeagueType.Nfl, 2026, 8);
        await _dispatcher.DidNotReceiveWithAnyArgs().DispatchAsync(default!, default!, default!, default);
    }

    // An admin can post a week's spreads before the scheduled release — that's still news.
    [Fact]
    public async Task NotifyAsync_PostedBeforeTheScheduledLock_Pushes()
    {
        await Build().NotifyAsync(LeagueType.Nfl, 2026, 8, "Week 8", Now.UtcDateTime.AddDays(1));

        await _dispatcher.ReceivedWithAnyArgs(2).DispatchAsync(default!, default!, default!, default);
    }

    [Fact]
    public async Task NotifyAsync_OneUsersPushFails_OthersStillGetIt()
    {
        _dispatcher.DispatchAsync("alice", Arg.Any<Func<NotificationPreferencesDto, bool>>(), Arg.Any<PushPayload>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("push service down"));

        await Build().NotifyAsync(LeagueType.Nfl, 2026, 8, "Week 8", LockedAnHourAgo);

        await _dispatcher.Received(1).DispatchAsync("bob", Arg.Any<Func<NotificationPreferencesDto, bool>>(), Arg.Any<PushPayload>(), Arg.Any<CancellationToken>());
    }

    // The spreads are already saved — a database failure here must not fail the spread job.
    [Fact]
    public async Task NotifyAsync_WhenTheDatabaseFails_DoesNotThrow()
    {
        _repo.TryRecordSpreadsPostedNotificationAsync(default, default, default).ThrowsAsyncForAnyArgs(new InvalidOperationException("db down"));

        await Build().NotifyAsync(LeagueType.Nfl, 2026, 8, "Week 8", LockedAnHourAgo);

        await _dispatcher.DidNotReceiveWithAnyArgs().DispatchAsync(default!, default!, default!, default);
    }

    // A failed recipient read must leave the week unrecorded, so the next run still pushes it.
    [Fact]
    public async Task NotifyAsync_WhenReadingRecipientsFails_DoesNotRecordTheWeek()
    {
        _preferences.GetSpreadsPostedUserIdsAsync(LeagueType.Nfl).ThrowsAsync(new InvalidOperationException("db blip"));

        await Build().NotifyAsync(LeagueType.Nfl, 2026, 8, "Week 8", LockedAnHourAgo);

        await _repo.DidNotReceiveWithAnyArgs().TryRecordSpreadsPostedNotificationAsync(default, default, default);
    }
}
