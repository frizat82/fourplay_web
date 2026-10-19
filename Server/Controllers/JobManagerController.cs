using FourPlayWebApp.Shared.Models;
using FourPlayWebApp.Shared.Models.Data.Dtos;
using FourPlayWebApp.Shared.Models.Enum;
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
        public Task<IActionResult> RunScores() => RunScoresJobAsync(ScoresJobs.Nfl);
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
        [HttpPost("run-cfb-spreads")]
        public Task<IActionResult> RunCfbSpreads([FromQuery] bool force = false) =>
            // The fixed "CFB Spread Job" JobKey this used to trigger no longer exists — CFB
            // spreads now run via per-week "CFB Spreads {season} Wk{n}" triggers (frizat-9m0),
            // same reasoning as RunSpreads above: pick the soonest one, not an arbitrary match.
            TriggerSoonestJobAsync("CFB Spreads ", "CFB Spread", force);
        [Authorize(Roles = "Administrator")]
        [HttpPost("run-cfb-scores")]
        public Task<IActionResult> RunCfbScores() => RunScoresJobAsync(ScoresJobs.Cfb);

        [Authorize(Roles = "Administrator")]
        [HttpGet("get-jobs")]
        public async Task<IEnumerable<JobStatusResponse>> GetAllJobsStatusAsync() {
            var jobs = await ListJobsAsync();

            // The sport each job is for. Sport-wide jobs know theirs from the job type; a per-league
            // job (Juice Reminder/Lock) takes its league's, so leagues are only loaded when one is
            // listed — and never on the hot path (GetNextSpreadJobAsync, every user's picks page).
            if (jobs.Any(j => j.Status.LeagueId.HasValue)) {
                var leagueSports = (await leagueRepo.GetAllLeaguesAsync()).ToDictionary(l => l.Id, l => l.LeagueType);
                jobs = jobs.Select(j => j.Status.LeagueId is { } id && leagueSports.TryGetValue(id, out var sport)
                    ? (j.Status, sport) : j).ToList();
            }
            foreach (var (status, sport) in jobs) status.Sport = sport is { } s ? SportLabel(s) : null;

            // Soonest-to-run first within a category, never-scheduled last. Not by name: per-week
            // jobs are named "... Wk{n}", and as text "Wk10".."Wk19" sort before "Wk7" — the
            // Job Manager's first NFL spread row read as week 10 while week 7 was up next.
            return jobs.Select(j => j.Status)
                .OrderBy(j => j.Category)
                .ThenBy(j => j.NextRun ?? DateTimeOffset.MaxValue)
                .ThenBy(j => j.JobName);
        }

        /// <summary>Every scheduled job's status (Sport not yet set, no ordering), and its sport if sport-wide.</summary>
        private async Task<List<(JobStatusResponse Status, LeagueType? Sport)>> ListJobsAsync() {
            var scheduler = await schedulerFactory.GetScheduler();
            var jobStatuses = new List<(JobStatusResponse Status, LeagueType? Sport)>();

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

                    // A job can have several triggers (each sport's scores job has one per fixed
                    // time — see ScoresJobs): show the one that fires next.
                    var trigger = (await scheduler.GetTriggersOfJob(jobKey))
                        .MinBy(t => t.GetNextFireTimeUtc() ?? DateTimeOffset.MaxValue);
                    var (category, isDynamic, sport) = JobCategoryClassifier.Classify(jobDetail.JobType);

                    var status = new JobStatusResponse {
                        JobName = jobDetail.Key.Name,
                        Description = jobDetail.Description ?? "",
                        Status = await GetJobStatusAsync(scheduler, jobKey, trigger),
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

                    jobStatuses.Add((status, sport));
                }
            }

            return jobStatuses;
        }

        private static string SportLabel(LeagueType sport) => sport == LeagueType.Cfb ? "CFB" : "NFL";

        private static int? TryGetLeagueId(IJobDetail jobDetail) {
            if (jobDetail.JobDataMap is null || !jobDetail.JobDataMap.ContainsKey(LeagueJuiceJobData.LeagueIdKey)) return null;
            return int.TryParse(jobDetail.JobDataMap.GetString(LeagueJuiceJobData.LeagueIdKey), out var leagueId) ? leagueId : null;
        }
        [Authorize]
        [HttpGet("get-next-spread-job")]
        public async Task<DateTimeOffset?> GetNextSpreadJobAsync([FromQuery] string? sport = null) {
            var allJobs = (await ListJobsAsync()).Select(j => j.Status);

            // "Spreads " (plural, trailing space) matches only the per-week triggers registered by
            // NflSpreadSchedulerJob/CfbSpreadSchedulerJob ("NFL Spreads {season} Wk{n}" / "CFB Spreads
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


        // Runs a sport's scores job now. One job key per sport carries every fixed time (ScoresJobs),
        // so its [DisallowConcurrentExecution] also keeps this from overlapping a scheduled run.
        // Demo stacks never register it.
        private async Task<IActionResult> RunScoresJobAsync(JobKey job) {
            try {
                var scheduler = await schedulerFactory.GetScheduler();
                if (!await scheduler.CheckExists(job))
                    return NotFound();
                await scheduler.TriggerJob(job);
                Log.Information("Started {JobName} Job", job.Name);
                return Ok(new MessageResponseDto($"Started {job.Name} Job"));
            }
            catch (Exception e) {
                return BadRequest(e.Message);
            }
        }

        // Shared by RunSpreads/RunCfbSpreads: picks the soonest not-yet-fired per-week job whose
        // name starts with jobNamePrefix and triggers it, optionally bypassing the spread jobs'
        // lock-time write guard (SpreadLockGuard) via force=true. force is deliberately not the
        // default — logged distinctly so an early write via this path is always auditable.
        private async Task<IActionResult> TriggerSoonestJobAsync(string jobNamePrefix, string sportLabel, bool force) {
            try {
                var scheduler = await schedulerFactory.GetScheduler();
                var allJobs = (await ListJobsAsync()).Select(j => j.Status);
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

        private static async Task<string> GetJobStatusAsync(IScheduler scheduler, JobKey jobKey, ITrigger? trigger) {
            var triggerState = trigger is null ? TriggerState.None : await scheduler.GetTriggerState(trigger.Key);
            var currentlyExecuting = await scheduler.GetCurrentlyExecutingJobs();
            var isExecuting = currentlyExecuting.Any(context => context.JobDetail.Key.Equals(jobKey));

            return isExecuting ? "EXECUTING" : triggerState.ToString();
        }
    }
}
