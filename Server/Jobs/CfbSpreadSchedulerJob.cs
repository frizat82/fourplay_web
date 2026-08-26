using Quartz;

namespace FourPlayWebApp.Server.Jobs;

// Resolves frizat-pxy for CFB: reads every in-scope week's SpreadLockDatetime and dynamically
// registers a one-time trigger for CfbSpreadJob at that exact instant, with data-driven catch-up
// for past-due weeks (see TimedTriggerScheduler). Runs at startup plus a daily catch-up cron —
// same cadence as NflSpreadSchedulerJob, replacing the old weekly-only catch-up that used to be
// fused into CfbSlateSeederJob. Structurally identical to NflSpreadSchedulerJob — only
// CfbSpreadScheduleSource differs.
public class CfbSpreadSchedulerJob(CfbSpreadScheduleSource source, ISchedulerFactory schedulerFactory)
    : SpreadSchedulerJobBase<CfbSpreadJob>(source, schedulerFactory);
