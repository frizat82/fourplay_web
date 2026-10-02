using System.Net;
using System.Security.Cryptography;
using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Services;
using FourPlayWebApp.Server.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using VapidHelper = WebPush.VapidHelper;

namespace FourPlayWebApp.Server.UnitTests;

/// <summary>
/// Tests for WebPushSender's response handling: a real (test-generated) VAPID key pair and a
/// real EC subscriber key are required so the library's own payload encryption step succeeds
/// before our code ever sees the HTTP response — only the transport (HttpMessageHandler) is
/// faked, mirroring JerseyCacheServiceTests' FakeHttpMessageHandler pattern. VapidOptions is
/// passed directly into each sender rather than via process environment variables, so these
/// tests carry no global mutable state and need no shared xUnit collection to serialize them.
/// </summary>
public class WebPushSenderTests
{
    private sealed class FakeHttpMessageHandler(HttpStatusCode status) : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(new HttpResponseMessage(status));
        }
    }

    private static readonly WebPush.VapidDetails TestVapidKeys = VapidHelper.GenerateVapidKeys();

    private static readonly VapidOptions ConfiguredVapidOptions = new()
    {
        PublicKey = TestVapidKeys.PublicKey,
        PrivateKey = TestVapidKeys.PrivateKey,
        Subject = "mailto:test@example.com",
    };

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');

    /// <summary>A real, uncompressed P-256 point + a real 16-byte auth secret — what a browser's
    /// pushManager.subscribe() actually returns — so WebPush's own Encryptor doesn't reject it.</summary>
    private static PushSubscription Subscription(string endpoint = "https://push.example.com/abc123")
    {
        using var ecdh = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var q = ecdh.ExportParameters(false).Q;
        var point = new byte[65];
        point[0] = 0x04;
        Buffer.BlockCopy(q.X!, 0, point, 1, 32);
        Buffer.BlockCopy(q.Y!, 0, point, 33, 32);

        var auth = new byte[16];
        RandomNumberGenerator.Fill(auth);

        return new PushSubscription
        {
            UserId = "user-1",
            Endpoint = endpoint,
            P256dh = Base64UrlEncode(point),
            Auth = Base64UrlEncode(auth),
        };
    }

    private static async Task SeedSubscriptionAsync(string dbName, PushSubscription subscription)
    {
        await using var seed = SqliteTestDb.Open(dbName);
        seed.PushSubscriptions.Add(subscription);
        await seed.SaveChangesAsync();
    }

    private static WebPushSender BuildSender(HttpStatusCode status, string dbName, out FakeHttpMessageHandler handler, VapidOptions? vapidOptions = null)
    {
        handler = new FakeHttpMessageHandler(status);
        var httpClient = new HttpClient(handler);
        var subscriptionService = new PushSubscriptionService(SqliteTestDb.Factory(dbName));
        return new WebPushSender(httpClient, subscriptionService, vapidOptions ?? ConfiguredVapidOptions, NullLogger<WebPushSender>.Instance);
    }

    [Fact]
    public async Task SendAsync_OnSuccess_MakesOneRequest_AndDoesNotDeleteSubscription()
    {
        var dbName = nameof(SendAsync_OnSuccess_MakesOneRequest_AndDoesNotDeleteSubscription);
        await SqliteTestDb.WithDb(dbName, async factoryUnused =>
        {
            var subscription = Subscription();
            await SeedSubscriptionAsync(dbName, subscription);

            var sender = BuildSender(HttpStatusCode.Created, dbName, out var handler);
            var result = await sender.SendAsync(subscription, new PushPayload("Title", "Body"));

            Assert.True(result);
            Assert.Equal(1, handler.CallCount);
            await using var verify = SqliteTestDb.Open(dbName);
            Assert.Equal(1, await verify.PushSubscriptions.CountAsync());
        });
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Gone)]
    public async Task SendAsync_OnStaleSubscriptionStatus_DeletesSubscription_AndDoesNotThrow(HttpStatusCode status)
    {
        var dbName = $"{nameof(SendAsync_OnStaleSubscriptionStatus_DeletesSubscription_AndDoesNotThrow)}_{status}";
        await SqliteTestDb.WithDb(dbName, async factoryUnused =>
        {
            var subscription = Subscription();
            await SeedSubscriptionAsync(dbName, subscription);

            var sender = BuildSender(status, dbName, out _);
            var result = await sender.SendAsync(subscription, new PushPayload("Title", "Body"));

            Assert.False(result);
            await using var verify = SqliteTestDb.Open(dbName);
            Assert.Equal(0, await verify.PushSubscriptions.CountAsync());
        });
    }

    [Fact]
    public async Task SendAsync_OnOtherFailureStatus_LogsButDoesNotThrow_AndDoesNotDeleteSubscription()
    {
        var dbName = nameof(SendAsync_OnOtherFailureStatus_LogsButDoesNotThrow_AndDoesNotDeleteSubscription);
        await SqliteTestDb.WithDb(dbName, async factoryUnused =>
        {
            var subscription = Subscription();
            await SeedSubscriptionAsync(dbName, subscription);

            var sender = BuildSender(HttpStatusCode.InternalServerError, dbName, out _);
            var result = await sender.SendAsync(subscription, new PushPayload("Title", "Body"));

            Assert.False(result);
            await using var verify = SqliteTestDb.Open(dbName);
            Assert.Equal(1, await verify.PushSubscriptions.CountAsync());
        });
    }

    [Fact]
    public async Task SendAsync_WhenVapidNotConfigured_DoesNotThrow_AndMakesNoRequest()
    {
        var dbName = nameof(SendAsync_WhenVapidNotConfigured_DoesNotThrow_AndMakesNoRequest);
        await SqliteTestDb.WithDb(dbName, async factoryUnused =>
        {
            var sender = BuildSender(HttpStatusCode.Created, dbName, out var handler, vapidOptions: new VapidOptions());
            var result = await sender.SendAsync(Subscription(), new PushPayload("Title", "Body"));

            Assert.False(result);
            Assert.Equal(0, handler.CallCount);
        });
    }
}
