using FourPlayWebApp.Shared.Models.Enum;
using FourPlayWebApp.Server.Data;
using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Shared.Models.Data.Dtos;
using Microsoft.EntityFrameworkCore;

namespace FourPlayWebApp.Server.Services;

public class NotificationPreferencesService(IDbContextFactory<ApplicationDbContext> dbContextFactory)
    : INotificationPreferencesService
{
    public async Task<NotificationPreferencesDto> GetAsync(string userId, LeagueType sport)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        var prefs = await db.NotificationPreferences.AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId && p.Sport == sport);

        // No row yet == every toggle defaults to off (pure opt-in) — not an error, the common
        // case for every user who has never visited the notifications settings page.
        return prefs is null ? new NotificationPreferencesDto() : ToDto(prefs);
    }

    public async Task<NotificationPreferencesDto> UpsertAsync(string userId, LeagueType sport, NotificationPreferencesDto preferences)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        var existing = await db.NotificationPreferences.FirstOrDefaultAsync(p => p.UserId == userId && p.Sport == sport);

        if (existing is null)
        {
            existing = new NotificationPreferences { UserId = userId, Sport = sport };
            db.NotificationPreferences.Add(existing);
        }

        existing.NotifyMineBloodyDuringGame = preferences.NotifyMineBloodyDuringGame;
        existing.NotifyMineBloodyAtFinal = preferences.NotifyMineBloodyAtFinal;
        existing.NotifyMineCoveringDuringGame = preferences.NotifyMineCoveringDuringGame;
        existing.NotifyMineCoveringAtFinal = preferences.NotifyMineCoveringAtFinal;
        existing.NotifyOthersBloodyDuringGame = preferences.NotifyOthersBloodyDuringGame;
        existing.NotifyOthersBloodyAtFinal = preferences.NotifyOthersBloodyAtFinal;
        existing.NotifyOthersCoveringDuringGame = preferences.NotifyOthersCoveringDuringGame;
        existing.NotifyOthersCoveringAtFinal = preferences.NotifyOthersCoveringAtFinal;
        existing.NotifyWeekResult = preferences.NotifyWeekResult;
        existing.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync();
        return ToDto(existing);
    }

    public async Task<bool> AnyLiveNotificationPreferenceEnabledAsync()
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        return await db.NotificationPreferences.AsNoTracking().AnyAsync(p =>
            p.NotifyMineBloodyDuringGame || p.NotifyMineBloodyAtFinal ||
            p.NotifyMineCoveringDuringGame || p.NotifyMineCoveringAtFinal ||
            p.NotifyOthersBloodyDuringGame || p.NotifyOthersBloodyAtFinal ||
            p.NotifyOthersCoveringDuringGame || p.NotifyOthersCoveringAtFinal);
    }

    private static NotificationPreferencesDto ToDto(NotificationPreferences p) => new()
    {
        NotifyMineBloodyDuringGame = p.NotifyMineBloodyDuringGame,
        NotifyMineBloodyAtFinal = p.NotifyMineBloodyAtFinal,
        NotifyMineCoveringDuringGame = p.NotifyMineCoveringDuringGame,
        NotifyMineCoveringAtFinal = p.NotifyMineCoveringAtFinal,
        NotifyOthersBloodyDuringGame = p.NotifyOthersBloodyDuringGame,
        NotifyOthersBloodyAtFinal = p.NotifyOthersBloodyAtFinal,
        NotifyOthersCoveringDuringGame = p.NotifyOthersCoveringDuringGame,
        NotifyOthersCoveringAtFinal = p.NotifyOthersCoveringAtFinal,
        NotifyWeekResult = p.NotifyWeekResult,
    };
}
