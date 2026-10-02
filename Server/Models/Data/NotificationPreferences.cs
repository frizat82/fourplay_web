namespace FourPlayWebApp.Server.Models.Data;

// One row per user, lazily created on first write (GetPreferencesAsync returns in-code all-off
// defaults when no row exists yet). Default is pure opt-in across every toggle — an explicit
// product decision, not an oversight (see docs/ideas.md #1 / frizat-tgk design notes).
public class NotificationPreferences {
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;

    public bool NotifyMineBloodyDuringGame { get; set; }
    public bool NotifyMineBloodyAtFinal { get; set; }
    public bool NotifyMineCoveringDuringGame { get; set; }
    public bool NotifyMineCoveringAtFinal { get; set; }

    public bool NotifyOthersBloodyDuringGame { get; set; }
    public bool NotifyOthersBloodyAtFinal { get; set; }
    public bool NotifyOthersCoveringDuringGame { get; set; }
    public bool NotifyOthersCoveringAtFinal { get; set; }

    public bool NotifyWeekResult { get; set; }

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
