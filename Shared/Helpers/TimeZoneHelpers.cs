using NodaTime;

namespace FourPlayWebApp.Shared.Helpers;


public static class TimeZoneHelpers {
    public static DateTime ConvertTimeToCst(DateTime utcDateTime) {
        // Create an instance of the BclDateTimeZone representing the Central Standard Time (CST) zone.
        var cstZone = DateTimeZoneProviders.Tzdb["America/Chicago"];

        // Change the Kind to Local.
        var utc = DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc);
        // Create an Instant from the UTC DateTime.
        var instant = Instant.FromDateTimeUtc(utc);

        // Convert the instant to the Central Standard Time.
        var zonedDateTime = instant.InZone(cstZone);

        // Return the DateTime in CST.
        return zonedDateTime.ToDateTimeUnspecified();
    }
}
