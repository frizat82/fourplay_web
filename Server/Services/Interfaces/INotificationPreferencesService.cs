using FourPlayWebApp.Shared.Models.Data.Dtos;

namespace FourPlayWebApp.Server.Services.Interfaces;

public interface INotificationPreferencesService
{
    /// <summary>Returns the user's saved preferences, or all-off defaults if no row exists yet.</summary>
    Task<NotificationPreferencesDto> GetAsync(string userId);

    Task<NotificationPreferencesDto> UpsertAsync(string userId, NotificationPreferencesDto preferences);
}
