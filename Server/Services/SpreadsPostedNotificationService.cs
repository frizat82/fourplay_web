using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Server.Services.Repositories.Interfaces;
using FourPlayWebApp.Shared.Helpers.Extensions;
using FourPlayWebApp.Shared.Models.Enum;
using Serilog;

namespace FourPlayWebApp.Server.Services;

public class SpreadsPostedNotificationService(
    ILeagueRepository leagueRepository,
    INotificationPreferencesService preferencesService,
    INotificationDispatcher dispatcher,
    // The clock the current week (and so its spread lock) is resolved by.
    [FromKeyedServices(CurrentWeekClock.Key)] TimeProvider clock) : ISpreadsPostedNotificationService
{
    // Spreads first saved this long after the scheduled release are old news (e.g. an admin
    // re-running a week whose spreads went up days earlier): recorded as sent, never pushed.
    public static readonly TimeSpan PushWindowAfterLock = TimeSpan.FromHours(24);

    public async Task<int> NotifyAsync(LeagueType sport, int season, int week, string weekLabel, DateTime spreadLockUtc)
    {
        try
        {
            // Recorded before sending: a crash mid-send costs some users this push, but a re-run
            // can never send it twice.
            if (!await leagueRepository.TryRecordSpreadsPostedNotificationAsync(sport, season, week)) return 0;
            if (clock.GetUtcNow().UtcDateTime > spreadLockUtc + PushWindowAfterLock) return 0;

            var payload = new PushPayload("IV League", $"{sport.Label()} {weekLabel} spreads are up — make your picks! 🏈", Url: "/picks", Sport: sport);
            var sent = 0;
            foreach (var userId in await preferencesService.GetSpreadsPostedUserIdsAsync(sport))
            {
                try
                {
                    await dispatcher.DispatchAsync(userId, p => p.NotifySpreadsPosted, payload);
                    sent++;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "SpreadsPostedNotificationService: push failed for user {UserId}, {Sport} {Week}", userId, sport.Label(), weekLabel);
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
