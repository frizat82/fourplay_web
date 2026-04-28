namespace FourPlayWebApp.Shared.Helpers;

public static class JobHelpers {

    private static string FormatLastRunStatus(DateTimeOffset? lastRun, string status) {
        return !lastRun.HasValue ? "Never executed" : $"{lastRun.Value:yyyy-MM-dd HH:mm:ss UTC} ({status})";
    }
}
