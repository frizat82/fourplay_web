using FourPlayWebApp.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quartz;
using Quartz.Impl.Matchers;
using Serilog;
using FourPlayWebApp.Server.Services.Interfaces;

namespace FourPlayWebApp.Server.Controllers {
    [ApiController]
    [Route("api/[controller]")]
    public class JobManagerController(ISchedulerFactory schedulerFactory, IJobObserverService observer) : ControllerBase {
        [Authorize(Roles = "Administrator")]
        [HttpPost("run-missing")]
        public async Task<IActionResult> RunMissingPicks() {
            try {
                var scheduler = await schedulerFactory.GetScheduler();
                var allJobs = await GetAllJobsStatusAsync();
                var jobName = allJobs.FirstOrDefault(job =>
                    job.JobName.Contains("Missing Picks", StringComparison.OrdinalIgnoreCase));
                if (jobName is null)
                    return NotFound();
                await scheduler.TriggerJob(new JobKey(jobName.JobName));
                Log.Information("Started MissingPicksJob Job");
                return Ok(new {message = "Started MissingPicksJob Job"});
            }
            catch (Exception e) {
                return BadRequest(e.Message);
            }
        }
        [Authorize(Roles = "Administrator")]
        [HttpPost("run-spreads")]
        public async Task<IActionResult> RunSpreads() {
            try {
                var scheduler = await schedulerFactory.GetScheduler();
                var allJobs = await GetAllJobsStatusAsync();
                var jobName = allJobs.FirstOrDefault(job =>
                    job.JobName.Contains("Spreads", StringComparison.OrdinalIgnoreCase));
                if (jobName is null)
                    return NotFound();
                await scheduler.TriggerJob(new JobKey(jobName.JobName));
                Log.Information("Started Spread Job");
                return Ok(new {message = "Started Spread Job"});
            }
            catch (Exception e) {
                return BadRequest(e.Message);
            }
        }
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
                return Ok(new {message = "Started Scores Job"});
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
                return Ok(new {message = "Started User Manager Job"});
            }
            catch (Exception e) {
                return BadRequest(e.Message);
            }
        }
        [Authorize]
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

                    var status = new JobStatusResponse {
                        JobName = jobDetail.Key.Name,
                        Description = jobDetail.Description ?? "",
                        Status = await GetJobStatusAsync(scheduler, jobKey),
                        NextRun = trigger?.GetNextFireTimeUtc(),
                        LastRun = trigger?.GetPreviousFireTimeUtc(),

                        // Default observer fields - will be overwritten below if we have data
                        LastStartedUtc = null,
                        LastSucceededUtc = null,
                        LastFailedUtc = null,
                        LastMessage = null,
                        RunCount = 0
                    };

                    if (observerInfos.TryGetValue(status.JobName, out var info)) {
                        status.LastStartedUtc = info.LastStartedUtc;
                        status.LastSucceededUtc = info.LastSucceededUtc;
                        status.LastFailedUtc = info.LastFailedUtc;
                        status.LastMessage = info.LastMessage;
                        status.RunCount = info.RunCount;
                    }

                    jobStatuses.Add(status);
                }
            }

            return jobStatuses.OrderBy(j => j.JobName);
        }
        [Authorize]
        [HttpGet("get-next-spread-job")]
        public async Task<DateTimeOffset?> GetNextSpreadJobAsync() {
            var allJobs = await GetAllJobsStatusAsync();
            var spreadJob = allJobs.Where(job =>
                job.JobName.Contains("Spread", StringComparison.OrdinalIgnoreCase) && job.NextRun is not null).MinBy(job => job.NextRun);
            return spreadJob?.NextRun;
        }
        [Authorize]
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


        private static async Task<string> GetJobStatusAsync(IScheduler scheduler, JobKey jobKey) {
            var triggerState = await scheduler.GetTriggerState(new TriggerKey($"{jobKey.Name}-trigger"));
            var currentlyExecuting = await scheduler.GetCurrentlyExecutingJobs();
            var isExecuting = currentlyExecuting.Any(context => context.JobDetail.Key.Equals(jobKey));

            return isExecuting ? "EXECUTING" : triggerState.ToString();
        }
    }
}
