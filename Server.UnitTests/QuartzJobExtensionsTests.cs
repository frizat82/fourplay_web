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

    [Fact]
    public async Task ScheduleCstCronJob_RegistersTriggerWithChicagoTimezone()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddQuartz(q =>
        {
            q.ScheduleCstCronJob<StubJob>(
                "Stub Thu 10am",
                "Test description",
                "0 0 10 ? * THU");
        });

        var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<ISchedulerFactory>();
        var scheduler = await factory.GetScheduler();

        var trigger = (ICronTrigger)await scheduler.GetTrigger(new TriggerKey("Stub Thu 10am"));

        Assert.NotNull(trigger);
        Assert.Equal("0 0 10 ? * THU", trigger.CronExpressionString);
        Assert.Equal(TimeZoneInfo.FindSystemTimeZoneById("America/Chicago"), trigger.TimeZone);
    }

    [Fact]
    public async Task ScheduleCstCronJob_TriggerDescriptionIsSet()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddQuartz(q =>
        {
            q.ScheduleCstCronJob<StubJob>(
                "Stub Fri 1am",
                "Runs early Friday",
                "0 0 1 ? * FRI");
        });

        var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<ISchedulerFactory>();
        var scheduler = await factory.GetScheduler();

        var trigger = await scheduler.GetTrigger(new TriggerKey("Stub Fri 1am"));

        Assert.NotNull(trigger);
        Assert.Equal("Runs early Friday", trigger.Description);
    }
}
