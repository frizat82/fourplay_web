using Quartz;

namespace FourPlayWebApp.Server.Jobs;

internal static class QuartzExtensions
{
    internal static void ScheduleCstCronJob<TJob>(
        this IServiceCollectionQuartzConfigurator q,
        string identity,
        string description,
        string cronExpression)
        where TJob : IJob
    {
        q.ScheduleJob<TJob>(trigger => trigger
            .WithIdentity(identity)
            .WithDescription(description)
            .WithCstCronSchedule(cronExpression));
    }

    /// <summary>
    /// One durable job with several fixed CST times as its triggers — unlike one
    /// ScheduleCstCronJob per time, [DisallowConcurrentExecution] then covers every run of it.
    /// </summary>
    internal static void ScheduleCstCronJob<TJob>(
        this IServiceCollectionQuartzConfigurator q,
        JobKey job,
        string description,
        params (string Identity, string Description, string CronExpression)[] times)
        where TJob : IJob
    {
        q.AddJob<TJob>(job, j => j.WithDescription(description).StoreDurably());
        foreach (var time in times)
        {
            q.AddTrigger(trigger => trigger
                .ForJob(job)
                .WithIdentity(time.Identity)
                .WithDescription(time.Description)
                .WithCstCronSchedule(time.CronExpression));
        }
    }

    private static ITriggerConfigurator WithCstCronSchedule(this ITriggerConfigurator trigger, string cronExpression) =>
        trigger.WithCronSchedule(cronExpression,
            x => x.WithMisfireHandlingInstructionFireAndProceed()
                  .InTimeZone(AppTimeZones.Central));
}
