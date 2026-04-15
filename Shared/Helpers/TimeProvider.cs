using NodaTime;

namespace FourPlayWebApp.Shared.Helpers
{
    public static class PageTimeProvider
    {
        private static Instant? _currentInstant;
        private static readonly DateTimeZone _cstZone = DateTimeZoneProviders.Tzdb["America/Chicago"];
        // Works cross-platform (no Windows/Linux ID mismatch)

        /// <summary>
        /// Current UTC instant (falls back to system clock if no test time is set).
        /// </summary>
        public static Instant UtcNowInstant => _currentInstant ?? SystemClock.Instance.GetCurrentInstant();

        /// <summary>
        /// Current time in UTC (for compatibility).
        /// </summary>
        public static DateTime UtcNow => UtcNowInstant.ToDateTimeUtc();

        /// <summary>
        /// Current time in CST/CDT (America/Chicago).
        /// </summary>
        public static ZonedDateTime CstNow => UtcNowInstant.InZone(_cstZone);

        /// <summary>
        /// Set a fixed test time (in CST unless explicitly passed as UTC).
        /// </summary>
        public static void SetTestTime(LocalDateTime localCstTime)
        {
            // Assume given time is CST/CDT
            _currentInstant = localCstTime.InZoneLeniently(_cstZone).ToInstant();
        }

        /// <summary>
        /// Set a fixed test time using a DateTime.
        /// </summary>
        public static void SetTestTime(DateTime time)
        {
            switch (time.Kind)
            {
                case DateTimeKind.Utc:
                    _currentInstant = Instant.FromDateTimeUtc(time);
                    break;

                case DateTimeKind.Local:
                    // Treat as local system time, convert to Instant
                    var localInstant = Instant.FromDateTimeUtc(time.ToUniversalTime());
                    _currentInstant = localInstant;
                    break;

                case DateTimeKind.Unspecified:
                    // Assume CST if unspecified
                    var localCst = LocalDateTime.FromDateTime(time);
                    _currentInstant = localCst.InZoneLeniently(_cstZone).ToInstant();
                    break;
            }
        }
        public static void SetTestTimeUtc(DateTime utcTime)
        {
            _currentInstant = Instant.FromDateTimeUtc(utcTime);
        }

        /// <summary>
        /// Reset back to real system clock.
        /// </summary>
        public static void ResetTestTime()
        {
            _currentInstant = null;
        }
    }
}
