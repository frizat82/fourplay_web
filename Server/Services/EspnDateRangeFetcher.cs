using FourPlayWebApp.Shared.Helpers;
using FourPlayWebApp.Shared.Models;
using Serilog;

namespace FourPlayWebApp.Server.Services;

// Shared by EspnApiService (NFL) and CfbApiService (CFB) — frizat-4gn. Both switched to a
// dates=START-END range query under frizat-11t (to make our own slate/week date window
// authoritative instead of trusting ESPN's own week=N bucketing), but ESPN has since started
// returning HTTP 400 {"code":400,"message":"Failed to get events endpoint."} for every range
// query — a single date (dates=YYYYMMDD) still works fine. This fetches one day at a time across
// the window and merges events client-side, preserving frizat-11t's correctness without
// depending on ESPN's now-broken range syntax.
internal static class EspnDateRangeFetcher {
    public static Task<EspnScores?> FetchRangeAsync(
        DateOnly startDate, DateOnly endDate, Func<DateOnly, Task<EspnScores?>> fetchSingleDayAsync) =>
        FetchDaysAsync(Enumerable.Range(0, Math.Max(0, endDate.DayNumber - startDate.DayNumber + 1)).Select(startDate.AddDays), fetchSingleDayAsync);

    // Same merge for an arbitrary set of days — a live poll's (LiveDayRefresh) day buckets.
    public static async Task<EspnScores?> FetchDaysAsync(
        IEnumerable<DateOnly> days, Func<DateOnly, Task<EspnScores?>> fetchSingleDayAsync) {
        EspnScores? merged = null;
        var seenEventIds = new HashSet<string>();

        foreach (var date in days) {
            EspnScores? dayResult;
            try {
                dayResult = await fetchSingleDayAsync(date);
            } catch (Exception ex) {
                // frizat: live incident 2026-09-19 — a single day's fetch timing out (ESPN slow,
                // network blip) used to propagate out of the whole range fetch, which propagated
                // out of the whole scores job, losing every OTHER already-fetched slate/week's
                // scores in that same run, not just this one day's. One bad day must not cost the
                // days around it — same tolerance a null/empty day already had below.
                Log.Warning(ex, "EspnDateRangeFetcher: failed to fetch {Date}, skipping this day", date);
                continue;
            }
            if (dayResult?.Events is null) continue;

            // A late-kickoff game can land in two adjacent single-day ESPN buckets depending on
            // timezone rounding — de-dupe by Event.Id rather than trusting each day's bucket to
            // be disjoint from its neighbors.
            var newEvents = dayResult.Events.Where(e => seenEventIds.Add(e.Id)).ToArray();
            if (newEvents.Length == 0) continue;

            merged = merged is null
                ? GameHelpers.WithEvents(dayResult, newEvents)
                : GameHelpers.WithEvents(merged, [.. merged.Events!, .. newEvents]);
        }

        return merged;
    }
}
