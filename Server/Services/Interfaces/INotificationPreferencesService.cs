using FourPlayWebApp.Shared.Models.Data.Dtos;

namespace FourPlayWebApp.Server.Services.Interfaces;

public interface INotificationPreferencesService
{
    /// <summary>Returns the user's saved preferences, or all-off defaults if no row exists yet.</summary>
    Task<NotificationPreferencesDto> GetAsync(string userId);

    Task<NotificationPreferencesDto> UpsertAsync(string userId, NotificationPreferencesDto preferences);

    /// <summary>
    /// Cheap short-circuit for LivePickTransitionService: true if ANY user has ANY of the 8 live
    /// (non-week-result) toggles on. Default preferences are all-off, so most of the time this
    /// should let the watcher skip all other work for a recompute tick.
    /// </summary>
    Task<bool> AnyLiveNotificationPreferenceEnabledAsync();
}
