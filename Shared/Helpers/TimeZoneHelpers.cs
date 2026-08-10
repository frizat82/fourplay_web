using NodaTime;

namespace FourPlayWebApp.Shared.Helpers;

public static class TimeZoneHelpers {
    public static DateTime ConvertTimeToCst(DateTime utcDateTime) =>
        ConvertTime(DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc), "America/Chicago");

    public static DateTime ConvertTimeToEt(DateTimeOffset utcDateTime) =>
        ConvertTime(utcDateTime.UtcDateTime, "America/New_York");

    private static DateTime ConvertTime(DateTime utcDateTime, string tzdbZoneId) {
        var zone = DateTimeZoneProviders.Tzdb[tzdbZoneId];
        var instant = Instant.FromDateTimeUtc(utcDateTime);
        return instant.InZone(zone).ToDateTimeUnspecified();
    }
}
