namespace FourPlayWebApp.Shared.Models.Data.Dtos;

// What the browser's PushSubscription (returned by pushManager.subscribe()) serializes to —
// endpoint plus the p256dh/auth keys needed to encrypt a payload to this specific install.
public class PushSubscriptionRequestDto
{
    public string Endpoint { get; set; } = string.Empty;
    public string P256dh { get; set; } = string.Empty;
    public string Auth { get; set; } = string.Empty;
    public string? UserAgent { get; set; }
}
