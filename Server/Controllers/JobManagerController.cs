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
            // job (frizat-pxy) — TriggerSoonestSpreadJobAsync picks the SOONEST one, not an
            // arbitrary match; a plain Contains("Spreads") FirstOrDefault would grab whichever
            // week sorts first alphabetically ("Wk1" before "Wk10"), not the one coming up next.
            TriggerSoonestSpreadJobAsync("NFL Spreads ", "Spread", force);
        [Authorize(Roles = "Administrator")]
        [HttpPost("run-scores")]
        public async Task<IActionResult> RunScores() {
            try {
                var scheduler = await schedulerFactory.GetScheduler();
                var allJobs = await GetAllJobsStatusAsync();
                var jobName = allJobs.FirstOrDefault(job =>
                    job.JobName.Contains("Scores", StringComparison.OrdinalIgnoreCase));
                if (jobName is null)
                    return NotFound();
                await scheduler.TriggerJob(new JobKey(jobName.JobName));
                Log.Information("Started Scores Job");
                return Ok(new MessageResponseDto("Started Scores Job"));
            }
            catch (Exception e) {
                return BadRequest(e.Message);
            }
        }
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
            TriggerSoonestSpreadJobAsync("CFB Spreads ", "CFB Spread", force);
        [Authorize(Roles = "Administrator")]
        [HttpPost("run-cfb-scores")]
        public async Task<IActionResult> RunCfbScores() {
            try {
                var scheduler = await schedulerFactory.GetScheduler();
                await scheduler.TriggerJob(new JobKey("CFB Scores Job"));
                Log.Information("Started CFB Scores Job");
                return Ok(new MessageResponseDto("Started CFB Scores Job"));
            }
            catch (Exception e) {
                return BadRequest(e.Message);
            }
        }

        [Authorize(Roles = "Administrator")]
        [HttpGet("get-jobs")]
        public async Task<IEnumerable<JobStatusResponse>> GetAllJobsStatusAsync() {
            var scheduler = await schedulerFactory.GetScheduler();
            var jobStatuses = new List<JobStatusResponse>();

            // fetch observer info once and index by job name for quick lookup
            var observerInfos = (await observer.GetAllJobInfosAsync())
                .ToDictionary(i => i.JobName, StringComparer.OrdinalIgnoreCase);

            // Juice Reminder/Lock job descriptions embed a raw LeagueId ("Remind league 6 owner
            // ...") — resolve it to the league's actual name once, not per-row, same pattern as
            // observerInfos above.
            var leagueNames = (await leagueRepo.GetAllLeaguesAsync())
                .ToDictionary(l => l.Id, l => l.LeagueName);

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
                        Description = DescribeWithLeagueName(jobDetail, leagueNames),
                        Status = await GetJobStatusAsync(scheduler, jobKey),
                        NextRun = trigger?.GetNextFireTimeUtc(),
                        Category = category,
                        IsDynamic = isDynamic,
                    };

                    if (observerInfos.TryGetValue(status.JobName, out var info)) {
                        status.LastSucceededUtc = info.LastSucceededUtc;
                        status.LastFailedUtc = info.LastFailedUtc;
                        status.LastMessage = info.LastMessage;
                    }

                    jobStatuses.Add(status);
                }
            }

            return jobStatuses.OrderBy(j => j.Category).ThenBy(j => j.JobName);
        }

        // Only Juice Reminder/Lock jobs carry a LeagueId in their JobDataMap (see
        // LeagueJuiceScheduleSource) — everything else's description passes through unchanged.
        private static string DescribeWithLeagueName(IJobDetail jobDetail, Dictionary<int, string> leagueNames) {
            var description = jobDetail.Description ?? "";
            if (jobDetail.JobDataMap is null || !jobDetail.JobDataMap.ContainsKey("LeagueId")) return description;
            var leagueIdRaw = jobDetail.JobDataMap.GetString("LeagueId");
            if (leagueIdRaw is null || !int.TryParse(leagueIdRaw, out var leagueId)) return description;
            if (!leagueNames.TryGetValue(leagueId, out var leagueName)) return description;
            return description.Replace($"league {leagueId}", $"\"{leagueName}\"");
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


        // Shared by RunSpreads/RunCfbSpreads: picks the soonest not-yet-fired per-week job whose
        // name starts with jobNamePrefix and triggers it, optionally bypassing the sport's
        // lock-time write guard (SpreadLockGuard) via force=true. force is deliberately not the
        // default — logged distinctly so an early write via this path is always auditable.
        private async Task<IActionResult> TriggerSoonestSpreadJobAsync(string jobNamePrefix, string sportLabel, bool force) {
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
