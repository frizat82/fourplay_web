namespace FourPlayWebApp.Server.Services;

// DI key for the TimeProvider injected into NflCurrentWeekService/CfbCurrentSlateService only —
// kept separate from the app's general-purpose TimeProvider singleton (used by SpreadLockGuard,
// LeaderboardService, LeagueJuiceScheduleSource, etc.) so DEMO_MODE's frozen clock
// (DemoDataSeeder.DemoFrozenNow, wired in Program.cs) can never silently reach a consumer that
// hasn't been vetted against it.
public static class CurrentWeekClock {
    public const string Key = "currentWeek";
}
