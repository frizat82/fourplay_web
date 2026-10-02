using FourPlayWebApp.Server.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FourPlayWebApp.Server.UnitTests;

public class PushSubscriptionServiceTests
{
    [Fact]
    public async Task SubscribeAsync_NewEndpoint_CreatesRow()
    {
        var db = nameof(SubscribeAsync_NewEndpoint_CreatesRow);
        await SqliteTestDb.WithDb(db, async factory =>
        {
            await using (var seed = SqliteTestDb.Open(db))
                await SqliteTestDb.SeedUser(seed, "alice");

            await new PushSubscriptionService(factory).SubscribeAsync("alice", "https://push.example/ep1", "p256dh-1", "auth-1", "TestAgent/1.0");

            await using var verify = SqliteTestDb.Open(db);
            var row = await verify.PushSubscriptions.SingleAsync();
            Assert.Equal("alice", row.UserId);
            Assert.Equal("https://push.example/ep1", row.Endpoint);
            Assert.Equal("p256dh-1", row.P256dh);
            Assert.Equal("auth-1", row.Auth);
            Assert.Equal("TestAgent/1.0", row.UserAgent);
            Assert.NotNull(row.LastSeenAt);
        });
    }

    [Fact]
    public async Task SubscribeAsync_SameEndpointAgain_UpsertsRatherThanDuplicating()
    {
        var db = nameof(SubscribeAsync_SameEndpointAgain_UpsertsRatherThanDuplicating);
        await SqliteTestDb.WithDb(db, async factory =>
        {
            await using (var seed = SqliteTestDb.Open(db))
                await SqliteTestDb.SeedUser(seed, "alice");

            var service = new PushSubscriptionService(factory);
            await service.SubscribeAsync("alice", "https://push.example/ep1", "p256dh-old", "auth-old", null);
            await service.SubscribeAsync("alice", "https://push.example/ep1", "p256dh-new", "auth-new", "Agent/2.0");

            await using var verify = SqliteTestDb.Open(db);
            Assert.Equal(1, await verify.PushSubscriptions.CountAsync());
            var row = await verify.PushSubscriptions.SingleAsync();
            Assert.Equal("p256dh-new", row.P256dh);
            Assert.Equal("auth-new", row.Auth);
            Assert.Equal("Agent/2.0", row.UserAgent);
        });
    }

    [Fact]
    public async Task GetForUserAsync_ReturnsOnlyThatUsersSubscriptions()
    {
        var db = nameof(GetForUserAsync_ReturnsOnlyThatUsersSubscriptions);
        await SqliteTestDb.WithDb(db, async factory =>
        {
            await using (var seed = SqliteTestDb.Open(db))
            {
                await SqliteTestDb.SeedUser(seed, "alice");
                await SqliteTestDb.SeedUser(seed, "bob");
            }

            var service = new PushSubscriptionService(factory);
            await service.SubscribeAsync("alice", "https://push.example/alice-1", "p", "a", null);
            await service.SubscribeAsync("alice", "https://push.example/alice-2", "p", "a", null);
            await service.SubscribeAsync("bob", "https://push.example/bob-1", "p", "a", null);

            var alicesSubscriptions = await service.GetForUserAsync("alice");
            Assert.Equal(2, alicesSubscriptions.Count);
            Assert.All(alicesSubscriptions, s => Assert.Equal("alice", s.UserId));
        });
    }

    [Fact]
    public async Task UnsubscribeAsync_OwnEndpoint_DeletesIt()
    {
        var db = nameof(UnsubscribeAsync_OwnEndpoint_DeletesIt);
        await SqliteTestDb.WithDb(db, async factory =>
        {
            await using (var seed = SqliteTestDb.Open(db))
                await SqliteTestDb.SeedUser(seed, "alice");

            var service = new PushSubscriptionService(factory);
            await service.SubscribeAsync("alice", "https://push.example/ep1", "p", "a", null);
            await service.UnsubscribeAsync("alice", "https://push.example/ep1");

            await using var verify = SqliteTestDb.Open(db);
            Assert.Equal(0, await verify.PushSubscriptions.CountAsync());
        });
    }

    [Fact]
    public async Task UnsubscribeAsync_AnotherUsersEndpoint_DoesNotDeleteIt()
    {
        // The security-critical case: a caller passing someone else's endpoint (e.g. seen in a
        // shared/public machine's browser devtools) must not be able to remove it.
        var db = nameof(UnsubscribeAsync_AnotherUsersEndpoint_DoesNotDeleteIt);
        await SqliteTestDb.WithDb(db, async factory =>
        {
            await using (var seed = SqliteTestDb.Open(db))
            {
                await SqliteTestDb.SeedUser(seed, "alice");
                await SqliteTestDb.SeedUser(seed, "mallory");
            }

            var service = new PushSubscriptionService(factory);
            await service.SubscribeAsync("alice", "https://push.example/alice-1", "p", "a", null);
            await service.UnsubscribeAsync("mallory", "https://push.example/alice-1");

            await using var verify = SqliteTestDb.Open(db);
            Assert.Equal(1, await verify.PushSubscriptions.CountAsync());
        });
    }

    [Fact]
    public async Task UnsubscribeAsync_NonExistentEndpoint_DoesNotThrow()
    {
        await SqliteTestDb.WithDb(nameof(UnsubscribeAsync_NonExistentEndpoint_DoesNotThrow), async factory =>
        {
            await new PushSubscriptionService(factory).UnsubscribeAsync("alice", "https://push.example/does-not-exist");
        });
    }
}
