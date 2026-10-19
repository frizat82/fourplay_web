using Quartz;

namespace FourPlayWebApp.Server.Jobs;

/// <summary>
/// One Quartz job per sport's scores job, with every fixed time as a trigger on it — so
/// [DisallowConcurrentExecution] keeps every run, scheduled, admin "run now" or queued by
/// FinalScoresWatcher, one at a time. Two runs saving the same finals at once would race on the
/// scores tables' unique index.
/// </summary>
public static class ScoresJobs
{
    public static readonly JobKey Nfl = new("NFL Scores");
    public static readonly JobKey Cfb = new("CFB Scores");

    /// <summary>Set on FinalScoresWatcher's runs: refetch only the current week/slate.</summary>
    public const string CurrentWeekOnlyKey = "currentWeekOnly";

    /// <summary>Trigger group of the runs FinalScoresWatcher queues.</summary>
    public const string OnFinalGroup = "On Final";

    public static bool IsCurrentWeekOnly(IJobExecutionContext context) =>
        context.MergedJobDataMap?.TryGetValue(CurrentWeekOnlyKey, out var value) == true && value is true;
}
