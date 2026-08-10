using Quartz;

namespace FourPlayWebApp.Server.Jobs;

// Resolves frizat-pxy: reads the SpreadLockDatetime column (NflSeasonWeekConfigs) and dynamically
// registers a one-time trigger for NflSpreadJob at that exact instant, with data-driven catch-up
// for past-due weeks (see SpreadTriggerScheduler). Runs at startup plus a cheap daily catch-up
// cron. Structurally identical to CfbSpreadSchedulerJob — only NflSpreadScheduleSource differs.
public class NflSpreadSchedulerJob(NflSpreadScheduleSource source, ISchedulerFactory schedulerFactory)
    : SpreadSchedulerJobBase<NflSpreadJob>(source, schedulerFactory);
