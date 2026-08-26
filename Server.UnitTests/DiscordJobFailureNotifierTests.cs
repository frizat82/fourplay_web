using System.Net;
using FourPlayWebApp.Server.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FourPlayWebApp.Server.UnitTests;

// frizat-703.2: one Discord message per job identity per 6h window. A job stuck
// retrying/misfiring shouldn't spam the channel, but a different job failing at the same
// time is independently actionable and still gets its own message.
public class DiscordJobFailureNotifierTests
{
    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        public int CallCount { get; private set; }
        public string? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        }
    }

    // Unlike the other FakeTimeProvider copies in this test project (CfbRankingCaptureJobTests,
    // NflSpreadJobTests, LeagueJuiceScheduleSourceTests, CfbSpreadJobTests), this one needs to
    // advance mid-test to exercise the dedupe window expiring.
    private sealed class FakeTimeProvider(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan by) => _now += by;
    }

    private static (DiscordJobFailureNotifier sut, FakeHttpMessageHandler handler, FakeTimeProvider time) Build(string? webhookUrl = "https://discord.example/webhook")
    {
        var handler = new FakeHttpMessageHandler();
        var httpClient = new HttpClient(handler);
        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient(DiscordJobFailureNotifier.HttpClientName).Returns(httpClient);
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(webhookUrl is null
                ? new Dictionary<string, string?>()
                : new Dictionary<string, string?> { ["DISCORD_ALERT_WEBHOOK_URL"] = webhookUrl })
            .Build();

        var sut = new DiscordJobFailureNotifier(httpClientFactory, time, config, NullLogger<DiscordJobFailureNotifier>.Instance);
        return (sut, handler, time);
    }

    [Fact]
    public async Task NotifyAsync_PostsToWebhook_WhenConfigured()
    {
        var (sut, handler, _) = Build();

        await sut.NotifyAsync("NflSpreadJob", "NFL Spreads 2026 Wk1", "ESPN odds API timed out");

        Assert.Equal(1, handler.CallCount);
        Assert.Contains("NflSpreadJob", handler.LastBody);
        Assert.Contains("ESPN odds API timed out", handler.LastBody);
    }

    [Fact]
    public async Task NotifyAsync_DoesNotPost_WhenWebhookUrlMissing()
    {
        var (sut, handler, _) = Build(webhookUrl: null);

        await sut.NotifyAsync("NflSpreadJob", "NFL Spreads 2026 Wk1", "boom");

        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task NotifyAsync_DedupesSecondFailure_OfSameJobIdentity_WithinWindow()
    {
        var (sut, handler, _) = Build();

        await sut.NotifyAsync("NflSpreadJob", "trigger", "first failure");
        await sut.NotifyAsync("NflSpreadJob", "trigger", "second failure, same job, moments later");

        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task NotifyAsync_DoesNotDedupe_DifferentJobIdentity()
    {
        var (sut, handler, _) = Build();

        await sut.NotifyAsync("NflSpreadJob", "trigger", "failure A");
        await sut.NotifyAsync("CfbSpreadJob", "trigger", "failure B");

        Assert.Equal(2, handler.CallCount);
    }

    [Fact]
    public async Task NotifyAsync_SendsAgain_AfterDedupeWindowExpires()
    {
        var (sut, handler, time) = Build();

        await sut.NotifyAsync("NflSpreadJob", "trigger", "first failure");
        time.Advance(TimeSpan.FromHours(6) + TimeSpan.FromMinutes(1));
        await sut.NotifyAsync("NflSpreadJob", "trigger", "still failing 6h later");

        Assert.Equal(2, handler.CallCount);
    }
}
