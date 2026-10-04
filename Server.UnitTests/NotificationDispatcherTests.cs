using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Services;
using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Shared.Models.Data.Dtos;
using FourPlayWebApp.Shared.Models.Enum;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace FourPlayWebApp.Server.UnitTests;

/// <summary>
/// The single shared call site every real trigger (week-result service, future live-pick watcher)
/// routes through. Mirrors GoogleEmailSender's exact non-production suppression gate — see that
/// class's own doc comment for the full incident history behind the RAILWAY_ENVIRONMENT_NAME +
/// IsDevelopment() combination. Deliberately NOT exercised by NotificationsController.SendTestPush,
/// which calls IPushSender directly (see NotificationsControllerTests).
/// </summary>
[Collection(RailwayEnvironmentNameCollection.Name)]
public class NotificationDispatcherTests : IDisposable
{
    private readonly INotificationPreferencesService _preferencesService = Substitute.For<INotificationPreferencesService>();
    private readonly IPushSubscriptionService _subscriptionService = Substitute.For<IPushSubscriptionService>();
    private readonly IPushSender _pushSender = Substitute.For<IPushSender>();

    public void Dispose() => Environment.SetEnvironmentVariable("RAILWAY_ENVIRONMENT_NAME", null);

    private NotificationDispatcher BuildDispatcher(bool isDevelopment, string? railwayEnvironmentName)
    {
        Environment.SetEnvironmentVariable("RAILWAY_ENVIRONMENT_NAME", railwayEnvironmentName);
        var environment = Substitute.For<IWebHostEnvironment>();
        environment.EnvironmentName.Returns(isDevelopment ? "Development" : "Production");
        return new NotificationDispatcher(_preferencesService, _subscriptionService, _pushSender,
            NullLogger<NotificationDispatcher>.Instance, environment);
    }

    private static List<PushSubscription> OneSubscription() => [new() { Id = 1, UserId = "user-1", Endpoint = "https://push.example/a", P256dh = "p", Auth = "a" }];

    [Fact]
    public async Task DispatchAsync_WhenToggleDisabled_NeverLoadsSubscriptions_NeverSends()
    {
        _preferencesService.GetAsync("user-1").Returns(new NotificationPreferencesDto { NotifyWeekResult = false });

        await BuildDispatcher(isDevelopment: false, railwayEnvironmentName: "production")
            .DispatchAsync("user-1", p => p.NotifyWeekResult, new PushPayload("T", "B"));

        await _subscriptionService.DidNotReceive().GetForUserAsync(Arg.Any<string>());
        await _pushSender.DidNotReceive().SendAsync(Arg.Any<PushSubscription>(), Arg.Any<PushPayload>());
    }

    [Fact]
    public async Task DispatchAsync_WhenToggleEnabled_ButNoSubscribedDevices_DoesNotSend()
    {
        _preferencesService.GetAsync("user-1").Returns(new NotificationPreferencesDto { NotifyWeekResult = true });
        _subscriptionService.GetForUserAsync("user-1").Returns([]);

        await BuildDispatcher(isDevelopment: false, railwayEnvironmentName: "production")
            .DispatchAsync("user-1", p => p.NotifyWeekResult, new PushPayload("T", "B"));

        await _pushSender.DidNotReceive().SendAsync(Arg.Any<PushSubscription>(), Arg.Any<PushPayload>());
    }

    [Fact]
    public async Task DispatchAsync_InProduction_SendsToEveryDevice()
    {
        _preferencesService.GetAsync("user-1").Returns(new NotificationPreferencesDto { NotifyWeekResult = true });
        var subscriptions = OneSubscription();
        _subscriptionService.GetForUserAsync("user-1").Returns(subscriptions);

        await BuildDispatcher(isDevelopment: false, railwayEnvironmentName: "production")
            .DispatchAsync("user-1", p => p.NotifyWeekResult, new PushPayload("T", "B"));

        await _pushSender.Received(1).SendAsync(subscriptions[0], Arg.Any<PushPayload>());
    }

    [Theory]
    [InlineData(false, null)]          // deployed dev environment: IsDevelopment()=false, no Railway var
    [InlineData(false, "development")] // deployed dev environment: explicit Railway var
    [InlineData(true, null)]           // local dev run: IsDevelopment()=true, no Railway var at all
    public async Task DispatchAsync_OutsideProduction_SuppressesSend_ButStillConsumesTheToggleCheck(bool isDevelopment, string? railwayEnvironmentName)
    {
        _preferencesService.GetAsync("user-1").Returns(new NotificationPreferencesDto { NotifyWeekResult = true });
        _subscriptionService.GetForUserAsync("user-1").Returns(OneSubscription());

        await BuildDispatcher(isDevelopment, railwayEnvironmentName)
            .DispatchAsync("user-1", p => p.NotifyWeekResult, new PushPayload("T", "B"));

        await _pushSender.DidNotReceive().SendAsync(Arg.Any<PushSubscription>(), Arg.Any<PushPayload>());
    }

    // Each installed app (NFL at ivleague.xyz, CFB at cfb.ivleague.xyz) registers its own
    // subscription — an NFL alert must only reach the NFL app. A subscription saved before sport
    // was tracked (null) keeps getting every sport, so nobody who enabled push loses anything.
    private static List<PushSubscription> NflCfbAndUnlabeled() => [
        new() { Id = 1, UserId = "user-1", Endpoint = "https://push.example/nfl", P256dh = "p", Auth = "a", Sport = LeagueType.Nfl },
        new() { Id = 2, UserId = "user-1", Endpoint = "https://push.example/cfb", P256dh = "p", Auth = "a", Sport = LeagueType.Cfb },
        new() { Id = 3, UserId = "user-1", Endpoint = "https://push.example/old", P256dh = "p", Auth = "a", Sport = null },
    ];

    [Theory]
    [InlineData(LeagueType.Nfl, "https://push.example/nfl", "https://push.example/cfb")]
    [InlineData(LeagueType.Cfb, "https://push.example/cfb", "https://push.example/nfl")]
    public async Task DispatchAsync_SendsOnlyToThatSportsApp_PlusUnlabeledSubscriptions(LeagueType sport, string expected, string excluded)
    {
        _preferencesService.GetAsync("user-1").Returns(new NotificationPreferencesDto { NotifyWeekResult = true });
        _subscriptionService.GetForUserAsync("user-1").Returns(NflCfbAndUnlabeled());

        await BuildDispatcher(isDevelopment: false, railwayEnvironmentName: "production")
            .DispatchAsync("user-1", p => p.NotifyWeekResult, new PushPayload("T", "B", Sport: sport));

        await _pushSender.Received(1).SendAsync(Arg.Is<PushSubscription>(s => s.Endpoint == expected), Arg.Any<PushPayload>(), Arg.Any<CancellationToken>());
        await _pushSender.Received(1).SendAsync(Arg.Is<PushSubscription>(s => s.Endpoint == "https://push.example/old"), Arg.Any<PushPayload>(), Arg.Any<CancellationToken>());
        await _pushSender.DidNotReceive().SendAsync(Arg.Is<PushSubscription>(s => s.Endpoint == excluded), Arg.Any<PushPayload>(), Arg.Any<CancellationToken>());
    }
}
