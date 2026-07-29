using Quartz;

namespace FourPlayWebApp.Server.Jobs;

internal static class QuartzExtensions
{
    private static readonly TimeZoneInfo Cst = TimeZoneInfo.FindSystemTimeZoneById("America/Chicago");

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
            .WithCronSchedule(cronExpression,
                x => x.WithMisfireHandlingInstructionFireAndProceed()
                      .InTimeZone(Cst)));
    }
}
