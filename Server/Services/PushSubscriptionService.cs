using FourPlayWebApp.Server.Data;
using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FourPlayWebApp.Server.Services;

public class PushSubscriptionService(IDbContextFactory<ApplicationDbContext> dbContextFactory)
    : IPushSubscriptionService
{
    public async Task<List<PushSubscription>> GetForUserAsync(string userId)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        return await db.PushSubscriptions.AsNoTracking()
            .Where(s => s.UserId == userId)
            .ToListAsync();
    }

    public async Task SubscribeAsync(string userId, string endpoint, string p256dh, string auth, string? userAgent)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        var existing = await db.PushSubscriptions.FirstOrDefaultAsync(s => s.Endpoint == endpoint);

        if (existing is null)
        {
            var newSubscription = new PushSubscription
            {
                UserId = userId,
                Endpoint = endpoint,
                P256dh = p256dh,
                Auth = auth,
                UserAgent = userAgent,
                LastSeenAt = DateTimeOffset.UtcNow,
            };
            db.PushSubscriptions.Add(newSubscription);

            try
            {
                await db.SaveChangesAsync();
                return;
            }
            catch (DbUpdateException)
            {
                // Another SubscribeAsync call for this same brand-new endpoint (e.g. sw.js's
                // pushsubscriptionchange handler firing alongside a manual re-subscribe, or two
                // tabs both enabling push) won the unique-Endpoint-index race between our read and
                // write. Drop our failed insert from the tracker and fall through to the update
                // branch below instead of surfacing a 500.
                db.Entry(newSubscription).State = EntityState.Detached;
                existing = await db.PushSubscriptions.FirstOrDefaultAsync(s => s.Endpoint == endpoint);
                if (existing is null) throw; // genuinely unexpected — rethrow rather than silently drop the subscribe
            }
        }

        // Re-subscribing an existing endpoint always re-homes it to the calling user — covers
        // a shared/public device where a different account logs in and re-subscribes the same
        // browser installation.
        existing.UserId = userId;
        existing.P256dh = p256dh;
        existing.Auth = auth;
        existing.UserAgent = userAgent;
        existing.LastSeenAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
    }

    public async Task UnsubscribeAsync(string userId, string endpoint)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        await db.PushSubscriptions
            .Where(s => s.Endpoint == endpoint && s.UserId == userId)
            .ExecuteDeleteAsync();
    }

    public async Task DeleteByEndpointAsync(string endpoint)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        await db.PushSubscriptions.Where(s => s.Endpoint == endpoint).ExecuteDeleteAsync();
    }
}
