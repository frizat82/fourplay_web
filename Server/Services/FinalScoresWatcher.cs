using FourPlayWebApp.Server.Jobs;
using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Shared.Helpers;
using FourPlayWebApp.Shared.Models;
using FourPlayWebApp.Shared.Models.Enum;
using Quartz;

namespace FourPlayWebApp.Server.Services;

/// <summary>
/// Saves final scores the moment a game ends instead of at the next fixed scores-job time. Those
/// times left gaps of hours on a Sunday — a 3:25pm kickoff's final waited for the 7:40pm run, a
/// Sunday-night game's for 1am — and results, the leaderboard and the money ride on saved finals.
///
/// When a sport's live scoreboard shows a game final that this process hasn't seen final before,
/// it queues one run of that sport's scores job (ScoresJobs), scoped to the current week/slate,
/// starting BatchDelay later so games ending a minute apart share it. While that run hasn't
/// started, further finals need nothing: it reads ESPN fresh when it starts. The fixed-time
/// triggers stay as the full-season backstop. If ESPN fails that run, the job retries it
/// (ScoresJobs.RetryFailedOnFinalRunAsync).
/// </summary>
public class FinalScoresWatcher(
    IEspnCacheService espnCache,
    ICfbCacheService cfbCache,
    ISchedulerFactory schedulerFactory,
    IConfiguration configuration,
    ILogger<FinalScoresWatcher> logger)
    // Kill switch: turn off without a deploy; the fixed-time scores triggers still run.
    : ScoresChangedWatcher(espnCache, cfbCache, configuration, "FINAL_SCORES_WATCHER_ENABLED", logger)
{
    public static readonly TimeSpan BatchDelay = TimeSpan.FromMinutes(1);

    /// <summary>Games this process has already seen final (and queued a save for), per sport.</summary>
    private readonly Dictionary<LeagueType, HashSet<string>> _seenFinal = new()
    {
        [LeagueType.Nfl] = [],
        [LeagueType.Cfb] = [],
    };

    protected override async Task OnScoresChangedAsync(LeagueType sport, ILiveScoresSource source, CancellationToken stoppingToken)
    {
        var seen = _seenFinal[sport];
        var finals = FinalGameIds(await source.GetScoresAsync());
        if (finals.IsSubsetOf(seen)) return;

        var job = sport == LeagueType.Cfb ? ScoresJobs.Cfb : ScoresJobs.Nfl;
        var scheduler = await schedulerFactory.GetScheduler(stoppingToken);
        // Demo/replay stacks never register the scores jobs — their data is seeded.
        if (await scheduler.CheckExists(job, stoppingToken) && !await HasQueuedRunAsync(scheduler, job, stoppingToken))
        {
            logger.LogInformation("FinalScoresWatcher: {Sport} game went final, saving scores in {Delay}", sport, BatchDelay);
            await scheduler.ScheduleJob(ScoresJobs.OnFinalRun(job, BatchDelay), stoppingToken);
        }
        // Only after the run is queued: a failed check leaves the final "new" for next time.
        seen.UnionWith(finals);
    }

    // A queued run that hasn't started yet. One that already fired has no next fire time, and may
    // have read ESPN before this final — so it doesn't count.
    private static async Task<bool> HasQueuedRunAsync(IScheduler scheduler, JobKey job, CancellationToken cancellationToken) =>
        (await scheduler.GetTriggersOfJob(job, cancellationToken))
            .Any(t => t.Key.Group == ScoresJobs.OnFinalGroup && t.GetNextFireTimeUtc() is not null);

    private static HashSet<string> FinalGameIds(EspnScores? scores) =>
        (scores?.Events ?? [])
            .SelectMany(e => e.Competitions ?? [])
            .Where(GameHelpers.IsGameOver)
            .Select(c => c.Id)
            .ToHashSet();
}
