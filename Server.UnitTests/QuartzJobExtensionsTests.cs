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

    // One job, several fixed times: every time is a trigger on the same job key, so
    // [DisallowConcurrentExecution] covers all of them (see ScoresJobs).
    [Fact]
    public async Task ScheduleCstCronJob_SeveralTimes_AreAllTriggersOfOneDurableJob()
    {
        var job = new JobKey("Stub Scores");
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddQuartz(q => q.ScheduleCstCronJob<StubJob>(job, "Stub job",
            ("Stub Sun 4:30pm", "Sunday late", "0 30 16 ? * SUN"),
            ("Stub Mon 1am", "After SNF", "0 0 1 ? * MON")));
        var scheduler = await services.BuildServiceProvider().GetRequiredService<ISchedulerFactory>().GetScheduler();

        var detail = await scheduler.GetJobDetail(job);
        var triggers = (await scheduler.GetTriggersOfJob(job)).Cast<ICronTrigger>().ToList();

        Assert.NotNull(detail);
        Assert.True(detail.Durable);
        Assert.Equal(new[] { "Stub Mon 1am", "Stub Sun 4:30pm" }, triggers.Select(t => t.Key.Name).Order());
        Assert.All(triggers, t => Assert.Equal(TimeZoneInfo.FindSystemTimeZoneById("America/Chicago"), t.TimeZone));
        Assert.Contains(triggers, t => t.CronExpressionString == "0 30 16 ? * SUN" && t.Description == "Sunday late");
    }
}
