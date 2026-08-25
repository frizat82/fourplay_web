namespace FourPlayWebApp.Server.Services;

// Shared, pure NFL/CFB season-resolution logic. Both sports have a control table shaped as
// "Season + a start/end window per week (NFL) or slate (CFB)" — this used to be two
// independent, hand-written implementations (NflCurrentWeekService's fallback chain,
// CfbCurrentSlateService's hardcoded-year hack) that had already drifted into a real bug.
// No DB/ESPN dependency — callers map their own rows into Window and normalize any
// DateOnly fields to DateTime first.
public static class SeasonWindowResolver {
    public readonly record struct Window(int Season, DateTime Start, DateTime End);

    // WEEK-LEVEL: "which specific week/slate are we in (or nearest to)?" — fine-grained,
    // used for UI defaults, the spread-lock schedule, and the spread-scheduling jobs. An
    // active window wins; otherwise the most-recently-completed window; otherwise the
    // soonest upcoming one; null only if the list is empty.
    public static Window? ResolveCurrentWeek(IEnumerable<Window> windows, DateTime now) {
        var list = windows as IReadOnlyCollection<Window> ?? windows.ToList();
        if (list.Count == 0) return null;

        var active = list.Where(w => w.Start <= now && now <= w.End)
            .Cast<Window?>()
            .FirstOrDefault();
        if (active is not null) return active;

        var mostRecentlyCompleted = list.Where(w => w.End < now)
            .OrderByDescending(w => w.End)
            .Cast<Window?>()
            .FirstOrDefault();
        if (mostRecentlyCompleted is not null) return mostRecentlyCompleted;

        return list.Where(w => w.Start > now)
            .OrderBy(w => w.Start)
            .Cast<Window?>()
            .FirstOrDefault();
    }

    // SEASON-LEVEL: "is a season actually happening right now, at all?" — coarse, used
    // exclusively by the two ESPN cache pollers and the two score jobs to decide whether
    // to do any ESPN-facing work this tick/run. A season's overall span is [earliest
    // window's Start, latest window's End] *for that season* — deliberately coarser than
    // "is one specific week window active," so a bye week or the gap between one week's
    // Monday night game and the next week's Thursday kickoff still reads as in-season.
    public static bool IsSeasonActive(IEnumerable<Window> windows, DateTime now) =>
        windows
            .GroupBy(w => w.Season)
            .Select(g => (Start: g.Min(w => w.Start), End: g.Max(w => w.End)))
            .Any(span => span.Start <= now && now <= span.End);
}
