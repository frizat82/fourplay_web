using FourPlayWebApp.Server.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Quartz;

namespace FourPlayWebApp.Server.UnitTests;

/// <summary>
/// mon.11: ScheduleCstCronJob helper reduces 17 copy-paste job registration blocks.
/// </summary>
public class QuartzJobExtensionsTests
{
    [DisallowConcurrentExecution]
    private class StubJob : IJob
    {
        public Task Execute(IJobExecutionContext context) => Task.CompletedTask;
    }

    private static async Task<IScheduler> BuildSchedulerWith(string identity, string description, string cron)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddQuartz(q => q.ScheduleCstCronJob<StubJob>(identity, description, cron));
        var provider = services.BuildServiceProvider();
        return await provider.GetRequiredService<ISchedulerFactory>().GetScheduler();
    }

    [Fact]
    public async Task ScheduleCstCronJob_RegistersTriggerWithChicagoTimezone()
    {
        var scheduler = await BuildSchedulerWith("Stub Thu 10am", "Test description", "0 0 10 ? * THU");
        var trigger = (ICronTrigger)await scheduler.GetTrigger(new TriggerKey("Stub Thu 10am"));

        Assert.NotNull(trigger);
        Assert.Equal("0 0 10 ? * THU", trigger.CronExpressionString);
        Assert.Equal(TimeZoneInfo.FindSystemTimeZoneById("America/Chicago"), trigger.TimeZone);
    }

    [Fact]
    public async Task ScheduleCstCronJob_TriggerDescriptionIsSet()
    {
        var scheduler = await BuildSchedulerWith("Stub Fri 1am", "Runs early Friday", "0 0 1 ? * FRI");
        var trigger = await scheduler.GetTrigger(new TriggerKey("Stub Fri 1am"));

        Assert.NotNull(trigger);
        Assert.Equal("Runs early Friday", trigger.Description);
    }
}
