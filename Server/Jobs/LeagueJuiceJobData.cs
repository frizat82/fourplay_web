using Quartz;

namespace FourPlayWebApp.Server.Jobs;

// Shared JobDataMap parsing for LeagueJuiceReminderJob/LeagueJuiceLockJob — both fire per
// (league, season), scheduled by LeagueJuiceSchedulerJob with LeagueId/Season set as JobData.
internal static class LeagueJuiceJobData {
    public static (int LeagueId, int Season) Parse(IJobExecutionContext context) => (
        int.Parse(context.MergedJobDataMap.GetString("LeagueId")!),
        int.Parse(context.MergedJobDataMap.GetString("Season")!));
}
