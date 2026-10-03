using Microsoft.AspNetCore.Hosting;

namespace FourPlayWebApp.Server.Services;

// Single source of truth for "is this a real production deploy" — extracted so the incident
// history behind this exact check only needs to live in one place as more call sites adopt it
// (GoogleEmailSender first, NotificationDispatcher second, a live-pick watcher next).
//
// frizat: an unattended cron was emailing a real inbox from the demo league seeded on dev, and
// /full-test-local's real-mode local runs send real email too — neither is desired. /code-review
// caught a real gap in an earlier version of this check: it suppressed only when
// environment.IsDevelopment() was true OR RAILWAY_ENVIRONMENT_NAME=="development" — but this
// repo's own documented local-run command (`dotnet run --no-launch-profile ...`) deliberately
// skips launchSettings.json, so ASPNETCORE_ENVIRONMENT is never set locally and .NET defaults
// IsDevelopment() to false. That earlier version would have sent real email/push on every
// ordinary local run — the exact incident this exists to prevent. Fixed by inverting to a
// fail-safe allow-list instead of a fail-open deny-list: this app is ONLY ever deployed via
// Railway (dev + prod, both in the same project/service), so RAILWAY_ENVIRONMENT_NAME is always
// present when actually deployed and always absent when running on a developer's own machine.
// Real sends now require an explicit "production" value; every other case (missing entirely,
// "development", or anything unexpected) suppresses. environment.IsDevelopment() is kept as an
// extra guard in case RAILWAY_ENVIRONMENT_NAME is ever misconfigured while genuinely running
// locally.
public static class DeploymentEnvironment
{
    public static bool IsProduction(IWebHostEnvironment environment) =>
        string.Equals(Environment.GetEnvironmentVariable("RAILWAY_ENVIRONMENT_NAME"), "production", StringComparison.OrdinalIgnoreCase)
        && !environment.IsDevelopment();
}
