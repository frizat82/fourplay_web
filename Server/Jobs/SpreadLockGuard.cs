using Quartz;

namespace FourPlayWebApp.Server.Jobs;

// Shared by NflSpreadJob and CfbSpreadJob (frizat CLAUDE.md: NFL/CFB are siblings). The
// scheduler (SpreadTriggerScheduler) already only fires these jobs at or after each week's
// SpreadLockDatetime, but that's not the only way Execute can run — an admin manual trigger
// (JobManagerController) or a future scheduling bug could invoke it early. This is the second,
// independent line of defense: the job itself refuses to write before lock time, same principle
// as Phase 1's DB-level unique index backstopping the app-level upsert check.
//
// SpreadLockDatetime is a required column (NflSeasonWeekConfigs/CfbSeasonWeekConfigs) — every
// real config row always has one, so there's no "not configured yet" case to fail closed on here.
// The only way past a future lock time is an explicit "force" flag in the triggering JobDataMap,
// set by JobManagerController's admin endpoints for a genuine outage where spreads need to go out
// immediately regardless of the configured lock time.
internal static class SpreadLockGuard {
    // `now` is passed in explicitly (from an injected TimeProvider in the caller) rather than
    // read via DateTime.UtcNow directly, so this boundary condition can be tested with an exact,
    // controlled instant instead of being tied to whatever the wall clock says when tests happen
    // to run.
    public static bool ShouldSkip(DateTime lockDatetime, DateTime now, IJobExecutionContext context) {
        if (IsForced(context)) return false;
        return now < lockDatetime;
    }

    private static bool IsForced(IJobExecutionContext context) =>
        context.MergedJobDataMap.ContainsKey("force") && context.MergedJobDataMap.GetBoolean("force");
}
