using FourPlayWebApp.Server.Data;
using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Shared.Models.Enum;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace FourPlayWebApp.Server.Services;

public class SpreadsPostedNotificationService(
    IDbContextFactory<ApplicationDbContext> dbContextFactory,
    INotificationDispatcher dispatcher,
    TimeProvider clock) : ISpreadsPostedNotificationService
{
    // Spreads first saved this long after the scheduled release are old news (e.g. an admin
    // re-running a week whose spreads went up days earlier): recorded as sent, never pushed.
    public static readonly TimeSpan PushWindowAfterLock = TimeSpan.FromHours(24);

    public async Task<int> NotifyAsync(LeagueType sport, int season, int week, string weekLabel, DateTime spreadLockUtc)
    {
        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync();
            // The spread jobs never run concurrently, so check-then-insert can't race.
            if (await db.SpreadsPostedNotificationSent.AnyAsync(r => r.Sport == sport && r.Season == season && r.Week == week))
                return 0;

            // Recorded before sending: a crash mid-send costs some users this push, but a re-run
            // can never send it twice.
            db.SpreadsPostedNotificationSent.Add(new SpreadsPostedNotificationSent { Sport = sport, Season = season, Week = week });
            await db.SaveChangesAsync();

            if (clock.GetUtcNow().UtcDateTime > spreadLockUtc + PushWindowAfterLock) return 0;

            var userIds = await db.NotificationPreferences.AsNoTracking()
                .Where(p => p.Sport == sport && p.NotifySpreadsPosted)
                .Select(p => p.UserId)
                .ToListAsync();

            var sportLabel = sport == LeagueType.Cfb ? "CFB" : "NFL";
            var payload = new PushPayload("IV League", $"{sportLabel} {weekLabel} spreads are up — make your picks! 🏈", Url: "/picks", Sport: sport);
            var sent = 0;
            foreach (var userId in userIds)
            {
                try
                {
                    await dispatcher.DispatchAsync(userId, p => p.NotifySpreadsPosted, payload);
                    sent++;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "SpreadsPostedNotificationService: push failed for user {UserId}, {Sport} {Week}", userId, sportLabel, weekLabel);
                }
            }
            return sent;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "SpreadsPostedNotificationService: {Sport} {Season} week {Week} notification failed", sport, season, week);
            return 0;
        }
    }
}
