namespace FourPlayWebApp.Server.Services;

// Single source of truth for whether Web Push is configured and what its keys are. Both
// WebPushSender (to sign/send) and NotificationsController (to hand the public key to the
// browser) read this one instance — if they each read the env vars independently, the public-key
// endpoint could hand out a key while WebPushSender still considers VAPID unconfigured (missing
// private key/subject) and silently no-ops every send.
public class VapidOptions
{
    public string? PublicKey { get; init; }
    public string? PrivateKey { get; init; }
    public string? Subject { get; init; }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(PublicKey) && !string.IsNullOrWhiteSpace(PrivateKey) && !string.IsNullOrWhiteSpace(Subject);

    public static VapidOptions FromEnvironment() => new()
    {
        PublicKey = Environment.GetEnvironmentVariable("VAPID_PUBLIC_KEY"),
        PrivateKey = Environment.GetEnvironmentVariable("VAPID_PRIVATE_KEY"),
        Subject = Environment.GetEnvironmentVariable("VAPID_SUBJECT"),
    };
}
