namespace FourPlayWebApp.Server.Services;

// Shared, pure NFL/CFB season-resolution logic. Both sports have a control table shaped as
// "Season + a start/end window per week (NFL) or slate (CFB), each with its own spread-lock
// datetime" — this used to be two independent, hand-written implementations
// (NflCurrentWeekService's fallback chain, CfbCurrentSlateService's hardcoded-year hack) that
// had already drifted into a real bug. No DB/ESPN dependency — callers map their own rows into
// Window/WeekWindow and normalize any DateOnly fields to DateTime first.
public static class SeasonWindowResolver {
    public readonly record struct Window(int Season, DateTime Start, DateTime End);

    // A window whose own SpreadLockDatetime is known — ResolveCurrentWeek needs this to tell
    // whether a window's own data (odds/results) actually exists yet, which its calendar
    // Start/End range alone can't answer.
    public readonly record struct WeekWindow(int Season, DateTime Start, DateTime End, DateTime SpreadLockDatetime);

    // frizat-9xg: don't switch "current" to a window until we're this close to ITS OWN spread
    // grab — otherwise the moment a new week's (or season's) calendar window begins, the UI
    // jumps to a week with no odds/results yet while the previous week still has real data.
    private static readonly TimeSpan EarlyActivationWindow = TimeSpan.FromDays(2);

    // WEEK-LEVEL: "which specific week/slate should the UI treat as current?" — fine-grained,
    // used for UI defaults, the spread-lock schedule, and the spread-scheduling jobs.
    // "Current" = the most recent window whose own SpreadLockDatetime has passed (real data
    // exists), UNLESS we're within EarlyActivationWindow of the NEXT window's own
    // SpreadLockDatetime, in which case that next window takes over early. Applies identically
    // whether "next" is a later week in the same season or week 1 of a new season — no
    // season-boundary special case, since windows are compared purely by SpreadLockDatetime
    // across all seasons at once.
    public static WeekWindow? ResolveCurrentWeek(IEnumerable<WeekWindow> windows, DateTime now) {
        var sorted = windows.OrderBy(w => w.SpreadLockDatetime).ToList();
        if (sorted.Count == 0) return null;

        WeekWindow? lastStarted = null;
        WeekWindow? next = null;
        foreach (var w in sorted) {
            if (w.SpreadLockDatetime <= now) { lastStarted = w; continue; }
            next = w;
            break;
        }

        if (next is not null && now >= next.Value.SpreadLockDatetime - EarlyActivationWindow)
            return next;

        // Bootstrap: nothing has ever started (app's very first season, pre-launch) — fall
        // back to the soonest upcoming window regardless of the early-activation proximity.
        return lastStarted ?? next;
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
