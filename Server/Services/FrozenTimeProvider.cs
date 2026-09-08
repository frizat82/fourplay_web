namespace FourPlayWebApp.Server.Services;

// Used in DEMO_MODE so NflCurrentWeekService/CfbCurrentSlateService's "what's current"
// resolution always agrees with whatever season DemoDataSeeder actually populated, regardless of
// the real wall clock (see DemoDataSeeder.DemoFrozenNow and Program.cs's TimeProvider
// registration).
public sealed class FrozenTimeProvider(DateTimeOffset now) : TimeProvider {
    public override DateTimeOffset GetUtcNow() => now;
}
