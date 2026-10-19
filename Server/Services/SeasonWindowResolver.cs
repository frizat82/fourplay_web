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
    // One day, not two: Thanksgiving week locks on Wednesday, and two days before that was Monday
    // morning — the app moved on before the previous week's Monday-night game was played, so the
    // live scoreboard stopped watching it. A day before any 2026 NFL/CFB lock is always after the
    // previous week's last game.
    private static readonly TimeSpan EarlyActivationWindow = TimeSpan.FromDays(1);

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

    // The first week/slate of a season that isn't over yet — the one being played, or the next one
    // once it's over. Unlike ResolveCurrentWeek there's no early-activation lead: a week lasts until
    // its own End (Monday night), and the day after, "current" can still be that week while it has
    // nothing left to pick. What a league started right now should begin with.
    public static WeekWindow? FirstUnfinishedWeek(IEnumerable<WeekWindow> windows, int season, DateTime now) =>
        windows.Where(w => w.Season == season && w.End > now)
            .OrderBy(w => w.Start)
            .Select(w => (WeekWindow?)w)
            .FirstOrDefault();

    // Every instant at which ResolveCurrentWeek or IsSeasonActive can give a different answer:
    // each window's Start/End, its SpreadLockDatetime, and the early-activation point before it.
    // The score pollers sleep between games and wake at these (ScorePollSchedule), so a week
    // rolling over is picked up at once rather than whenever they next happen to wake.
    public static IEnumerable<DateTime> ChangePoints(IEnumerable<WeekWindow> windows) =>
        windows.SelectMany(w => new[] { w.Start, w.End, w.SpreadLockDatetime, w.SpreadLockDatetime - EarlyActivationWindow });

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
