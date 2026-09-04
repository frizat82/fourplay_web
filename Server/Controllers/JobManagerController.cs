using FourPlayWebApp.Shared.Models;
using FourPlayWebApp.Shared.Models.Data.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quartz;
using Quartz.Impl.Matchers;
using Serilog;
using FourPlayWebApp.Server.Jobs;
using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Server.Services.Repositories.Interfaces;

namespace FourPlayWebApp.Server.Controllers {
    [ApiController]
    [Route("api/[controller]")]
    public class JobManagerController(ISchedulerFactory schedulerFactory, IJobObserverService observer, ILeagueRepository leagueRepo) : ControllerBase {
        [Authorize(Roles = "Administrator")]
        [HttpPost("run-spreads")]
        public Task<IActionResult> RunSpreads([FromQuery] bool force = false) =>
            // Each in-scope week now registers its own one-time "NFL Spreads {season} Wk{n}"
            // job (frizat-pxy) — TriggerSoonestJobAsync picks the SOONEST one, not an
            // arbitrary match; a plain Contains("Spreads") FirstOrDefault would grab whichever
            // week sorts first alphabetically ("Wk1" before "Wk10"), not the one coming up next.
            TriggerSoonestJobAsync("NFL Spreads ", "Spread", force);
        [Authorize(Roles = "Administrator")]
        [HttpPost("run-scores")]
        public Task<IActionResult> RunScores() =>
            // frizat-11t: NflScoresJob is registered under 7 separately-keyed cron triggers ("NFL
            // Scores Thu 10am", "NFL Scores Fri 1am", ...), all containing "Scores" — same for CFB's
            // 5 triggers ("CFB Scores Sat Noon", ...). The old unscoped Contains("Scores")
            // FirstOrDefault could pick either sport's job depending on enumeration order. Scoped to
            // "NFL Scores " and soonest-first, same pattern as the spread jobs — though unlike the
            // per-week spread jobs, "soonest" carries no real meaning here: every trigger for a given
            // sport runs the exact same Execute() regardless of which cron fired it, so any matching
            // job would do. Reusing the soonest-first helper is just convenient shared plumbing, not
            // picking a semantically "correct" job the way it does for spreads.
            TriggerSoonestJobAsync("NFL Scores ", "Scores", force: false);
        [Authorize(Roles = "Administrator")]
        [HttpPost("run-users")]
        public async Task<IActionResult> RunUserJob() {
            try {
                var scheduler = await schedulerFactory.GetScheduler();
                await scheduler.TriggerJob(new JobKey("User Manager"));
                Log.Information("Started User Manager Job");
                return Ok(new MessageResponseDto("Started User Manager Job"));
            }
            catch (Exception e) {
                return BadRequest(e.Message);
            }
        }
        [Authorize(Roles = "Administrator")]
        [HttpPost("run-cfb-slate-seeder")]
        public async Task<IActionResult> RunCfbSlateSeeder() {
            try {
                var scheduler = await schedulerFactory.GetScheduler();
                await scheduler.TriggerJob(new JobKey("CFB Slate Seeder"));
                Log.Information("Started CFB Slate Seeder Job");
                return Ok(new MessageResponseDto("Started CFB Slate Seeder Job"));
            }
            catch (Exception e) {
                return BadRequest(e.Message);
            }
        }
        [Authorize(Roles = "Administrator")]
        [HttpPost("run-cfb-spreads")]
        public Task<IActionResult> RunCfbSpreads([FromQuery] bool force = false) =>
            // The fixed "CFB Spread Job" JobKey this used to trigger no longer exists — CFB
            // spreads now run via per-week "CFB Spreads {season} Wk{n}" triggers (frizat-9m0),
            // same reasoning as RunSpreads above: pick the soonest one, not an arbitrary match.
            TriggerSoonestJobAsync("CFB Spreads ", "CFB Spread", force);
        [Authorize(Roles = "Administrator")]
        [HttpPost("run-cfb-scores")]
        public Task<IActionResult> RunCfbScores() =>
            // frizat-11t: the fixed "CFB Scores Job" JobKey this used to trigger has never
            // existed — CfbScoresJob runs via 5 separately-keyed cron triggers ("CFB Scores Sat
            // Noon"/"Sat 4pm"/"Sat 8pm"/"Sat Midnight"/"Sun 6am"), same shape as the per-week
            // spread jobs. Pick the soonest one, not a hardcoded key that always 400s.
            TriggerSoonestJobAsync("CFB Scores ", "CFB Scores", force: false);

        [Authorize(Roles = "Administrator")]
        [HttpGet("get-jobs")]
        public async Task<IEnumerable<JobStatusResponse>> GetAllJobsStatusAsync() {
            var scheduler = await schedulerFactory.GetScheduler();
            var jobStatuses = new List<JobStatusResponse>();

            // fetch observer info once and index by job name for quick lookup
            var observerInfos = (await observer.GetAllJobInfosAsync())
                .ToDictionary(i => i.JobName, StringComparer.OrdinalIgnoreCase);

            var jobGroups = await scheduler.GetJobGroupNames();
            foreach (var group in jobGroups) {
                var groupMatcher = GroupMatcher<JobKey>.GroupEquals(group);
                var jobKeys = await scheduler.GetJobKeys(groupMatcher);

                foreach (var jobKey in jobKeys) {
                    var jobDetail = await scheduler.GetJobDetail(jobKey);
                    if (jobDetail == null) continue;

                    var triggers = await scheduler.GetTriggersOfJob(jobKey);
                    var trigger = triggers.FirstOrDefault();
                    var (category, isDynamic) = JobCategoryClassifier.Classify(jobDetail.JobType);

                    var status = new JobStatusResponse {
                        JobName = jobDetail.Key.Name,
                        Description = jobDetail.Description ?? "",
                        Status = await GetJobStatusAsync(scheduler, jobKey),
                        NextRun = trigger?.GetNextFireTimeUtc(),
                        Category = category,
                        IsDynamic = isDynamic,
                        LeagueId = TryGetLeagueId(jobDetail),
                    };

                    if (observerInfos.TryGetValue(status.JobName, out var info)) {
                        status.LastSucceededUtc = info.LastSucceededUtc;
                        status.LastFailedUtc = info.LastFailedUtc;
                        status.LastMessage = info.LastMessage;
                    }

                    jobStatuses.Add(status);
                }
            }

            // Only Juice Reminder/Lock jobs carry a LeagueId — this endpoint is also on the hot
            // path for every logged-in user via GetNextSpreadJobAsync below (not admin-gated), so
            // the league join only runs when a job in THIS batch actually references one, not on
            // every call.
            var referencedLeagueIds = jobStatuses.Where(j => j.LeagueId.HasValue).Select(j => j.LeagueId!.Value).ToHashSet();
            if (referencedLeagueIds.Count > 0) {
                var leagueNames = (await leagueRepo.GetAllLeaguesAsync())
                    .Where(l => referencedLeagueIds.Contains(l.Id))
                    .ToDictionary(l => l.Id, l => l.LeagueName);
                foreach (var status in jobStatuses.Where(j => j.LeagueId.HasValue)) {
                    if (leagueNames.TryGetValue(status.LeagueId!.Value, out var leagueName)) status.LeagueName = leagueName;
                }
            }

            return jobStatuses.OrderBy(j => j.Category).ThenBy(j => j.JobName);
        }

        private static int? TryGetLeagueId(IJobDetail jobDetail) {
            if (jobDetail.JobDataMap is null || !jobDetail.JobDataMap.ContainsKey(LeagueJuiceJobData.LeagueIdKey)) return null;
            return int.TryParse(jobDetail.JobDataMap.GetString(LeagueJuiceJobData.LeagueIdKey), out var leagueId) ? leagueId : null;
        }
        [Authorize]
        [HttpGet("get-next-spread-job")]
        public async Task<DateTimeOffset?> GetNextSpreadJobAsync([FromQuery] string? sport = null) {
            var allJobs = await GetAllJobsStatusAsync();

            // "Spreads " (plural, trailing space) matches only the per-week triggers registered by
            // NflSpreadSchedulerJob/CfbSlateSeederJob ("NFL Spreads {season} Wk{n}" / "CFB Spreads
            // {season} Wk{n}") — NOT the scheduler jobs' own triggers ("NFL Spread Scheduler ..."),
            // whose next-run time is when the scheduler next checks in, not when spreads get fetched.
            var candidates = allJobs.Where(job =>
                job.JobName.Contains("Spreads ", StringComparison.OrdinalIgnoreCase) && job.NextRun is not null);

            var prefix = sport?.Trim().ToLowerInvariant() switch {
                "nfl" => "NFL Spreads ",
                "cfb" => "CFB Spreads ",
                _ => null,
            };
            if (prefix is not null)
                candidates = candidates.Where(job => job.JobName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

            return candidates.MinBy(job => job.NextRun)?.NextRun;
        }
        [Authorize(Roles = "Administrator")]
        [HttpGet("get-job/{jobName}")]
        public async Task<JobStatusResponse?> GetJobStatusAsync(string jobName) {
            var allJobs = await GetAllJobsStatusAsync();
            return allJobs.FirstOrDefault(job =>
                string.Equals(job.JobName, jobName, StringComparison.OrdinalIgnoreCase));
        }
        [Authorize(Roles = "Administrator")]
        [HttpDelete("delete-job/{jobName}")]
        public async Task<ActionResult<bool>> DeleteJob(string jobName) {
            var scheduler = await schedulerFactory.GetScheduler();
            var result = await scheduler.DeleteJob(new JobKey(jobName));
            return Ok(result);
        }


        // Shared by RunSpreads/RunCfbSpreads/RunScores/RunCfbScores: picks the soonest not-yet-fired
        // job whose name starts with jobNamePrefix and triggers it, optionally bypassing the spread
        // jobs' lock-time write guard (SpreadLockGuard) via force=true. force is deliberately not
        // the default — logged distinctly so an early write via this path is always auditable.
        // Scores jobs don't have a lock-time guard, so their callers always pass force: false.
        private async Task<IActionResult> TriggerSoonestJobAsync(string jobNamePrefix, string sportLabel, bool force) {
            try {
                var scheduler = await schedulerFactory.GetScheduler();
                var allJobs = await GetAllJobsStatusAsync();
                var jobName = allJobs
                    .Where(job => job.JobName.StartsWith(jobNamePrefix, StringComparison.OrdinalIgnoreCase) && job.NextRun is not null)
                    .MinBy(job => job.NextRun);
                if (jobName is null)
                    return NotFound();
                if (force) {
                    var data = new JobDataMap { { "force", true } };
                    await scheduler.TriggerJob(new JobKey(jobName.JobName), data);
                    Log.Warning("Admin FORCED {SportLabel} Job {JobName} — bypassing lock-time guard", sportLabel, jobName.JobName);
                } else {
                    await scheduler.TriggerJob(new JobKey(jobName.JobName));
                    Log.Information("Started {SportLabel} Job {JobName}", sportLabel, jobName.JobName);
                }
                return Ok(new MessageResponseDto($"Started {sportLabel} Job"));
            }
            catch (Exception e) {
                return BadRequest(e.Message);
            }
        }

        private static async Task<string> GetJobStatusAsync(IScheduler scheduler, JobKey jobKey) {
            var triggerState = await scheduler.GetTriggerState(new TriggerKey($"{jobKey.Name}-trigger"));
            var currentlyExecuting = await scheduler.GetCurrentlyExecutingJobs();
            var isExecuting = currentlyExecuting.Any(context => context.JobDetail.Key.Equals(jobKey));

            return isExecuting ? "EXECUTING" : triggerState.ToString();
        }
    }
}
