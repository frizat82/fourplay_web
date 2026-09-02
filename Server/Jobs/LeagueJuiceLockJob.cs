using FourPlayWebApp.Server.Services;
using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Server.Services.Repositories.Interfaces;
using Quartz;

namespace FourPlayWebApp.Server.Jobs;

// frizat-ugs: fires once per (league, season), scheduled by LeagueJuiceSchedulerJob at
// lockTime (2pm America/Chicago on the season's first game date). Auto-fills Juice — carrying
// forward the prior season's values, or falling back to entity defaults if none exists — if
// still unconfigured at fire time.
[DisallowConcurrentExecution]
public class LeagueJuiceLockJob(ILeagueRepository repo, IJobObserverService observer) : IJob {
    public async Task Execute(IJobExecutionContext context) {
        // RecordJobStartAsync is now centralized in JobFailureAlertListener.JobToBeExecuted
        // (fires for every job type, not just this one) — calling it again here would
        // double-count RunCount for the exact same run.
        //
        // /code-review: must be the actual Quartz JobKey ("Juice Lock {leagueId}-{season}", per
        // TimedTriggerScheduler/LeagueJuiceScheduleSource's candidate.Identity), not the class
        // name — JobManagerController correlates observer info by jobDetail.Key.Name, so
        // recording under the class name meant every league/season's message clobbered every
        // other's under one shared key, invisible under the row an admin actually looks at.
        var jobName = context.JobDetail.Key.Name;
        try {
            var (leagueId, season) = LeagueJuiceJobData.Parse(context);

            // Re-check freshness at fire time — the owner may have configured Juice in the days
            // since this trigger was registered.
            if (await repo.GetLeagueJuiceMappingAsync(leagueId, season) is not null) {
                await observer.RecordJobSuccessAsync(jobName, $"League {leagueId} season {season} already configured — nothing to lock");
                return;
            }

            var priorMapping = LeagueJuiceRollForward.FindPriorSeasonMapping(season, await repo.GetLeagueJuiceMappingAsync(leagueId));
            await repo.AddLeagueJuiceMappingAsync(LeagueJuiceRollForward.BuildMapping(leagueId, season, priorMapping));

            await observer.RecordJobSuccessAsync(jobName, priorMapping is not null
                ? $"League {leagueId} season {season} Juice carried forward from season {priorMapping.Season}"
                : $"League {leagueId} season {season} Juice set to defaults (no prior season)");
        } catch (Exception ex) {
            await observer.RecordAndRethrowAsync(jobName, ex);
        }
    }
}
