using Quartz;

namespace FourPlayWebApp.Server.Jobs;

// Shared Execute logic for NFL/CFB spread-trigger scheduling — the only thing that differs
// between sports is which ISpreadScheduleSource feeds in and which TSpreadJob gets triggered.
// NflSpreadSchedulerJob/CfbSpreadSchedulerJob are one-line subclasses of this, which is what
// actually guarantees the two stay in sync rather than hoping two hand-maintained job classes
// don't drift (frizat CLAUDE.md: siblings, not separate products).
[DisallowConcurrentExecution]
public abstract class SpreadSchedulerJobBase<TSpreadJob>(ISpreadScheduleSource source, ISchedulerFactory schedulerFactory) : IJob
    where TSpreadJob : IJob {
    public async Task Execute(IJobExecutionContext context) {
        var candidates = await source.GetCandidatesAsync();
        var scheduler = await schedulerFactory.GetScheduler(context.CancellationToken);
        await SpreadTriggerScheduler.ScheduleAsync<TSpreadJob>(scheduler, candidates, context.CancellationToken);
    }
}
