namespace FourPlayWebApp.Server.Models.Data;

// One row per browser/device install that has granted Notification permission and subscribed via
// the Push API. A single user can have many (phone + laptop + ...). Endpoint is unique: the
// browser mints a fresh Endpoint if a subscription is ever re-created, so re-subscribing the same
// device is an upsert keyed on Endpoint, never a duplicate row.
public class PushSubscription {
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public string P256dh { get; set; } = string.Empty;
    public string Auth { get; set; } = string.Empty;
    public string? UserAgent { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastSeenAt { get; set; }
}
