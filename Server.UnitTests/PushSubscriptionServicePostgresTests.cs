using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Models.Identity;
using FourPlayWebApp.Server.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FourPlayWebApp.Server.UnitTests;

/// <summary>
/// PushSubscriptionService's Unsubscribe/DeleteByEndpoint are bulk ExecuteDeleteAsync calls, which
/// CLAUDE.md requires proving on real Postgres rather than SQLite (SQLite passing proves nothing
/// about how Npgsql translates the query — see LeagueInviteLinkServicePostgresTests for the same
/// reasoning). This file also proves SubscribeAsync's unique-Endpoint-index race fallback (two
/// concurrent subscribes for the same brand-new endpoint) under a real concurrent connection pool,
/// mirroring PickConcurrencyTests' Task.WhenAll approach — a SQLite shared-cache connection
/// serializes writes in a way that wouldn't exercise the same race window.
/// </summary>
[Collection(PostgresCollection.Name)]
public class PushSubscriptionServicePostgresTests(PostgresFixture postgres)
{
    private async Task SeedUserAsync(string userId)
    {
        await using var db = postgres.OpenDb();
        db.Users.Add(new ApplicationUser { Id = userId, UserName = userId, Email = $"{userId}@demo.local" });
        await db.SaveChangesAsync();
    }

    private async Task DeleteSeededAsync(string userId)
    {
        await using var db = postgres.OpenDb();
        db.PushSubscriptions.RemoveRange(await db.PushSubscriptions.Where(s => s.UserId == userId).ToListAsync());
        db.Users.RemoveRange(await db.Users.Where(u => u.Id == userId).ToListAsync());
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task UnsubscribeAsync_DeletesOnlyTheCallersMatchingEndpoint_OnPostgres()
    {
        var userId = $"push-unsub-{Guid.NewGuid():N}";
        await SeedUserAsync(userId);
        try
        {
            await using (var seed = postgres.OpenDb())
            {
                seed.PushSubscriptions.Add(new PushSubscription { UserId = userId, Endpoint = "https://push.example/a", P256dh = "p", Auth = "a" });
                seed.PushSubscriptions.Add(new PushSubscription { UserId = userId, Endpoint = "https://push.example/b", P256dh = "p", Auth = "a" });
                await seed.SaveChangesAsync();
            }

            await new PushSubscriptionService(postgres.Factory()).UnsubscribeAsync(userId, "https://push.example/a");

            await using var verify = postgres.OpenDb();
            var remaining = await verify.PushSubscriptions.Where(s => s.UserId == userId).ToListAsync();
            Assert.Equal("https://push.example/b", Assert.Single(remaining).Endpoint);
        }
        finally
        {
            await DeleteSeededAsync(userId);
        }
    }

    [Fact]
    public async Task DeleteByEndpointAsync_DeletesRegardlessOfOwningUser_OnPostgres()
    {
        var userId = $"push-delbyendpoint-{Guid.NewGuid():N}";
        await SeedUserAsync(userId);
        try
        {
            await using (var seed = postgres.OpenDb())
            {
                seed.PushSubscriptions.Add(new PushSubscription { UserId = userId, Endpoint = "https://push.example/stale", P256dh = "p", Auth = "a" });
                await seed.SaveChangesAsync();
            }

            await new PushSubscriptionService(postgres.Factory()).DeleteByEndpointAsync("https://push.example/stale");

            await using var verify = postgres.OpenDb();
            Assert.Equal(0, await verify.PushSubscriptions.CountAsync(s => s.UserId == userId));
        }
        finally
        {
            await DeleteSeededAsync(userId);
        }
    }

    /// <summary>
    /// The regression this test exists to prevent: two concurrent SubscribeAsync calls for the
    /// SAME brand-new endpoint (e.g. sw.js's pushsubscriptionchange handler racing a manual
    /// re-subscribe) both pass the "no existing row" check before either writes, so the second
    /// SaveChangesAsync hits the unique Endpoint index and must fall back to an update instead of
    /// throwing an unhandled 500.
    /// </summary>
    [Fact]
    public async Task SubscribeAsync_ConcurrentCallsForTheSameNewEndpoint_NeverThrows_AndEndsWithOneRow()
    {
        var userId = $"push-race-{Guid.NewGuid():N}";
        await SeedUserAsync(userId);
        const string endpoint = "https://push.example/concurrent-new-endpoint";
        try
        {
            var factory = postgres.Factory();
            var service = new PushSubscriptionService(factory);

            var tasks = Enumerable.Range(0, 8)
                .Select(i => service.SubscribeAsync(userId, endpoint, $"p256dh-{i}", $"auth-{i}", $"agent-{i}"));
            await Task.WhenAll(tasks);

            await using var verify = postgres.OpenDb();
            var rows = await verify.PushSubscriptions.Where(s => s.Endpoint == endpoint).ToListAsync();
            Assert.Single(rows);
            Assert.Equal(userId, rows[0].UserId);
        }
        finally
        {
            await DeleteSeededAsync(userId);
        }
    }
}
