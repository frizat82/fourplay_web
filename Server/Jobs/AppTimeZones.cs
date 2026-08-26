namespace FourPlayWebApp.Server.Jobs;

// Canonical timezone lookups shared across scheduling code — currently two unrelated consumers:
// QuartzExtensions.ScheduleCstCronJob (cron triggers) and LeagueJuiceScheduleSource (computing
// "2pm Central" from a plain date). Neither owns this constant; it lives here instead so a third
// consumer has an obvious place to reuse it rather than adding its own FindSystemTimeZoneById call.
internal static class AppTimeZones {
    internal static readonly TimeZoneInfo Central = TimeZoneInfo.FindSystemTimeZoneById("America/Chicago");
}
