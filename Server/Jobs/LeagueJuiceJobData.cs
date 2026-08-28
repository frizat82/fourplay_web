using Quartz;

namespace FourPlayWebApp.Server.Jobs;

// Shared JobDataMap parsing for LeagueJuiceReminderJob/LeagueJuiceLockJob — both fire per
// (league, season), scheduled by LeagueJuiceSchedulerJob with LeagueId/Season set as JobData.
internal static class LeagueJuiceJobData {
    // Shared with LeagueJuiceScheduleSource (which sets this key) and JobManagerController (which
    // reads it to resolve a job's league name for the admin UI) — one name, three call sites.
    public const string LeagueIdKey = "LeagueId";
    public const string SeasonKey = "Season";

    public static (int LeagueId, int Season) Parse(IJobExecutionContext context) => (
        int.Parse(context.MergedJobDataMap.GetString(LeagueIdKey)!),
        int.Parse(context.MergedJobDataMap.GetString(SeasonKey)!));
}
