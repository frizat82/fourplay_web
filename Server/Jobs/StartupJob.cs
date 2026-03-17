using Quartz;
namespace FourPlayWebApp.Server.Jobs;
public class StartupJob(ISchedulerFactory factory) : IJob {
    public async Task Execute(IJobExecutionContext context)
    {
        var scheduler = await factory.GetScheduler();
        await scheduler.TriggerJob(new JobKey("User Manager"));
        await Task.Delay(TimeSpan.FromMinutes(1));
        await scheduler.TriggerJob(new JobKey("NFL Scores"));
        /*await Task.Delay(TimeSpan.FromMinutes(1));
        await scheduler.TriggerJob(new JobKey("NFL Spreads"));
        */
    }
}