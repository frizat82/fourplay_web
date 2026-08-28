using FourPlayWebApp.Server.Controllers;
using FourPlayWebApp.Server.Jobs;
using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Server.Services.Repositories.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Quartz;
using Quartz.Impl.Matchers;
using System.Reflection;
using System.Security.Claims;

namespace FourPlayWebApp.Server.UnitTests;

/// <summary>
/// Tests for JobManagerController.
///
/// Security tests use reflection (same pattern as AuthorizationTests.cs) to verify
/// that every job-trigger endpoint requires the Administrator role.
///
/// Functional tests mock ISchedulerFactory to drive the happy-path and error branches.
/// </summary>
public class JobManagerControllerTests
{
    // ── Endpoints that must require [Authorize(Roles="Administrator")] ─────────

    public static TheoryData<string> AdminOnlyEndpoints =>
    [
        nameof(JobManagerController.RunSpreads),
        nameof(JobManagerController.RunScores),
        nameof(JobManagerController.RunUserJob),
        nameof(JobManagerController.RunCfbSlateSeeder),
        nameof(JobManagerController.RunCfbSpreads),
        nameof(JobManagerController.RunCfbScores),
        nameof(JobManagerController.DeleteJob),
        // The full job registry (every job's status/next-run/last error message) is internal
        // operational detail — only GetNextSpreadJobAsync (a single timestamp, backs the public
        // Rules page) is meant for any logged-in user.
        nameof(JobManagerController.GetAllJobsStatusAsync),
        nameof(JobManagerController.GetJobStatusAsync),
    ];

    [Theory]
    [MemberData(nameof(AdminOnlyEndpoints))]
    public void AdminOnlyEndpoint_HasAuthorizeAttribute_WithAdministratorRole(string methodName)
    {
        var method = typeof(JobManagerController).GetMethod(methodName);
        Assert.NotNull(method);

        var attr = method.GetCustomAttributes<AuthorizeAttribute>()
                         .FirstOrDefault(a => a.Roles is not null);

        Assert.NotNull(attr);
        Assert.Equal("Administrator", attr.Roles);
    }

    // ── Endpoints that only require authentication (not admin role) ───────────

    public static TheoryData<string> AuthenticatedOnlyEndpoints =>
    [
        nameof(JobManagerController.GetNextSpreadJobAsync),
    ];

    [Theory]
    [MemberData(nameof(AuthenticatedOnlyEndpoints))]
    public void AuthenticatedEndpoint_HasAuthorizeAttribute_WithNoRole(string methodName)
    {
        var method = typeof(JobManagerController).GetMethod(methodName, new[] { typeof(string) })
                  ?? typeof(JobManagerController).GetMethod(methodName);
        Assert.NotNull(method);

        // Must have [Authorize] but without an Administrator role restriction
        var attr = method.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(attr);
        Assert.Null(attr.Roles); // authenticated-only, not admin-only
    }

    // ── Functional: RunUserJob happy path ─────────────────────────────────────

    [Fact]
    public async Task RunUserJob_ReturnsOk_WhenSchedulerSucceeds()
    {
        var (factory, scheduler, observer, controller) = BuildSut(isAdmin: true);
        scheduler.TriggerJob(new JobKey("User Manager")).Returns(Task.CompletedTask);

        var result = await controller.RunUserJob();

        Assert.IsType<OkObjectResult>(result);
        await scheduler.Received(1).TriggerJob(Arg.Is<JobKey>(k => k.Name == "User Manager"));
    }

    [Fact]
    public async Task RunUserJob_ReturnsBadRequest_WhenSchedulerThrows()
    {
        var (factory, scheduler, observer, controller) = BuildSut(isAdmin: true);
        scheduler.TriggerJob(Arg.Any<JobKey>())
            .ThrowsAsync(new InvalidOperationException("scheduler offline"));

        var result = await controller.RunUserJob();

        Assert.IsType<BadRequestObjectResult>(result);
    }

    // ── Functional: RunCfbSlateSeeder happy path ───────────────────────────────

    [Fact]
    public async Task RunCfbSlateSeeder_ReturnsOk_WhenSchedulerSucceeds()
    {
        var (factory, scheduler, observer, controller) = BuildSut(isAdmin: true);
        scheduler.TriggerJob(new JobKey("CFB Slate Seeder")).Returns(Task.CompletedTask);

        var result = await controller.RunCfbSlateSeeder();

        Assert.IsType<OkObjectResult>(result);
        await scheduler.Received(1).TriggerJob(Arg.Is<JobKey>(k => k.Name == "CFB Slate Seeder"));
    }

    // ── Functional: RunCfbSpreads happy path ──────────────────────────────────
    // frizat-9m0: the old fixed "CFB Spread Job" JobKey no longer exists — CFB spreads now run
    // via per-week "CFB Spreads {season} Wk{n}" triggers, so this must find one via the same
    // soonest-job lookup as RunSpreads, not a hardcoded key.

    [Fact]
    public async Task RunCfbSpreads_ReturnsOk_WhenSchedulerSucceeds()
    {
        var (factory, scheduler, observer, controller) = BuildSut(isAdmin: true);
        SetupSchedulerWithJobs(scheduler, ("CFB Spreads 2026 Wk6", DateTimeOffset.UtcNow.AddDays(3)));
        scheduler.TriggerJob(new JobKey("CFB Spreads 2026 Wk6")).Returns(Task.CompletedTask);

        var result = await controller.RunCfbSpreads();

        Assert.IsType<OkObjectResult>(result);
        await scheduler.Received(1).TriggerJob(new JobKey("CFB Spreads 2026 Wk6"));
    }

    [Fact]
    public async Task RunCfbSpreads_Forced_PassesForceFlagInJobDataMap()
    {
        var (factory, scheduler, observer, controller) = BuildSut(isAdmin: true);
        SetupSchedulerWithJobs(scheduler, ("CFB Spreads 2026 Wk6", DateTimeOffset.UtcNow.AddDays(3)));
        scheduler.TriggerJob(new JobKey("CFB Spreads 2026 Wk6"), Arg.Any<JobDataMap>()).Returns(Task.CompletedTask);

        var result = await controller.RunCfbSpreads(force: true);

        Assert.IsType<OkObjectResult>(result);
        await scheduler.Received(1).TriggerJob(
            new JobKey("CFB Spreads 2026 Wk6"),
            Arg.Is<JobDataMap>(d => d.GetBoolean("force")));
    }

    [Fact]
    public async Task RunCfbSpreads_ReturnsNotFound_WhenNoCfbSpreadsJobExists()
    {
        var (factory, scheduler, observer, controller) = BuildSut(isAdmin: true);
        scheduler.GetJobGroupNames().Returns(new List<string>());

        var result = await controller.RunCfbSpreads();

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task RunCfbSpreads_IgnoresNflJobs()
    {
        var (factory, scheduler, observer, controller) = BuildSut(isAdmin: true);
        SetupSchedulerWithJobs(scheduler, ("NFL Spreads 2026 Wk6", DateTimeOffset.UtcNow.AddDays(1)));

        var result = await controller.RunCfbSpreads();

        Assert.IsType<NotFoundResult>(result);
    }

    // ── Functional: RunCfbScores happy path ───────────────────────────────────

    [Fact]
    public async Task RunCfbScores_ReturnsOk_WhenSchedulerSucceeds()
    {
        var (factory, scheduler, observer, controller) = BuildSut(isAdmin: true);
        scheduler.TriggerJob(new JobKey("CFB Scores Job")).Returns(Task.CompletedTask);

        var result = await controller.RunCfbScores();

        Assert.IsType<OkObjectResult>(result);
    }

    // ── Functional: DeleteJob happy path ──────────────────────────────────────

    [Fact]
    public async Task DeleteJob_ReturnsOk_WithTrue_WhenJobExists()
    {
        var (factory, scheduler, observer, controller) = BuildSut(isAdmin: true);
        scheduler.DeleteJob(Arg.Any<JobKey>()).Returns(true);

        var result = await controller.DeleteJob("some-job");

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(true, ok.Value);
    }

    [Fact]
    public async Task DeleteJob_ReturnsOk_WithFalse_WhenJobDoesNotExist()
    {
        var (factory, scheduler, observer, controller) = BuildSut(isAdmin: true);
        scheduler.DeleteJob(Arg.Any<JobKey>()).Returns(false);

        var result = await controller.DeleteJob("nonexistent-job");

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(false, ok.Value);
    }

    // ── Functional: RunSpreads — no spread job ────────────────────────────────

    [Fact]
    public async Task RunSpreads_ReturnsNotFound_WhenNoSpreadsJobExists()
    {
        var (factory, scheduler, observer, controller) = BuildSut(isAdmin: true);
        scheduler.GetJobGroupNames().Returns(new List<string>());

        var result = await controller.RunSpreads();

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task RunSpreads_ReturnsOk_WhenSchedulerSucceeds()
    {
        var (factory, scheduler, observer, controller) = BuildSut(isAdmin: true);
        SetupSchedulerWithJobs(scheduler, ("NFL Spreads 2026 Wk6", DateTimeOffset.UtcNow.AddDays(3)));
        scheduler.TriggerJob(new JobKey("NFL Spreads 2026 Wk6")).Returns(Task.CompletedTask);

        var result = await controller.RunSpreads();

        Assert.IsType<OkObjectResult>(result);
        await scheduler.Received(1).TriggerJob(new JobKey("NFL Spreads 2026 Wk6"));
    }

    [Fact]
    public async Task RunSpreads_DefaultNoForce_TriggersWithoutJobDataMap()
    {
        // The default (unforced) call must not pass a "force" flag through — NflSpreadJob's
        // lock-time guard treats an absent key as "not forced."
        var (factory, scheduler, observer, controller) = BuildSut(isAdmin: true);
        SetupSchedulerWithJobs(scheduler, ("NFL Spreads 2026 Wk6", DateTimeOffset.UtcNow.AddDays(3)));
        scheduler.TriggerJob(new JobKey("NFL Spreads 2026 Wk6")).Returns(Task.CompletedTask);

        var result = await controller.RunSpreads();

        Assert.IsType<OkObjectResult>(result);
        await scheduler.Received(1).TriggerJob(new JobKey("NFL Spreads 2026 Wk6"));
        await scheduler.DidNotReceive().TriggerJob(Arg.Any<JobKey>(), Arg.Any<JobDataMap>());
    }

    [Fact]
    public async Task RunSpreads_Forced_PassesForceFlagInJobDataMap()
    {
        var (factory, scheduler, observer, controller) = BuildSut(isAdmin: true);
        SetupSchedulerWithJobs(scheduler, ("NFL Spreads 2026 Wk6", DateTimeOffset.UtcNow.AddDays(3)));
        scheduler.TriggerJob(new JobKey("NFL Spreads 2026 Wk6"), Arg.Any<JobDataMap>()).Returns(Task.CompletedTask);

        var result = await controller.RunSpreads(force: true);

        Assert.IsType<OkObjectResult>(result);
        await scheduler.Received(1).TriggerJob(
            new JobKey("NFL Spreads 2026 Wk6"),
            Arg.Is<JobDataMap>(d => d.GetBoolean("force")));
    }

    [Fact]
    public async Task RunSpreads_PicksSoonestJob_NotArbitraryMatch()
    {
        // Regression: FirstOrDefault() over multiple simultaneously-registered per-week jobs
        // (frizat-pxy) would pick whichever week's name sorts first alphabetically — "Wk10" sorts
        // before "Wk6" — not the one actually coming up next.
        var (factory, scheduler, observer, controller) = BuildSut(isAdmin: true);
        SetupSchedulerWithJobs(scheduler,
            ("NFL Spreads 2026 Wk10", DateTimeOffset.UtcNow.AddDays(10)),
            ("NFL Spreads 2026 Wk6", DateTimeOffset.UtcNow.AddDays(1)));
        scheduler.TriggerJob(new JobKey("NFL Spreads 2026 Wk6")).Returns(Task.CompletedTask);

        var result = await controller.RunSpreads();

        Assert.IsType<OkObjectResult>(result);
        await scheduler.Received(1).TriggerJob(new JobKey("NFL Spreads 2026 Wk6"));
        await scheduler.DidNotReceive().TriggerJob(new JobKey("NFL Spreads 2026 Wk10"));
    }

    [Fact]
    public async Task RunSpreads_IgnoresCfbJobs()
    {
        var (factory, scheduler, observer, controller) = BuildSut(isAdmin: true);
        SetupSchedulerWithJobs(scheduler, ("CFB Spreads 2026 Wk6", DateTimeOffset.UtcNow.AddDays(1)));

        var result = await controller.RunSpreads();

        Assert.IsType<NotFoundResult>(result);
    }

    // ── Functional: RunScores — no scores job ────────────────────────────────

    [Fact]
    public async Task RunScores_ReturnsNotFound_WhenNoScoresJobExists()
    {
        var (factory, scheduler, observer, controller) = BuildSut(isAdmin: true);
        scheduler.GetJobGroupNames().Returns(new List<string>());

        var result = await controller.RunScores();

        Assert.IsType<NotFoundResult>(result);
    }

    // ── Functional: GetAllJobsStatusAsync — category/description enrichment ───
    // frizat: "Juice-Reminder-6" was reported unreadable — 6 is a raw league DB id, not a league
    // count. Description now substitutes the actual league name; Category/IsDynamic (from
    // JobCategoryClassifier) let the admin UI group jobs and hide the noisy per-league/per-week set.

    [Fact]
    public async Task GetAllJobsStatusAsync_JuiceReminderJob_DescriptionUsesLeagueName()
    {
        var (_, scheduler, _, leagueRepo, controller) = BuildSutWithLeagueRepo(isAdmin: true);
        leagueRepo.GetAllLeaguesAsync().Returns(new List<LeagueInfo> {
            new() { Id = 6, LeagueName = "Sunday Funday", OwnerUserId = "u1" },
        });
        SetupJobOfType<LeagueJuiceReminderJob>(scheduler, "Juice Reminder 6-2026",
            "Remind league 6 owner to configure Juice for season 2026",
            jobData: new Dictionary<string, string> { ["LeagueId"] = "6" });

        var result = await controller.GetAllJobsStatusAsync();

        var job = Assert.Single(result);
        Assert.Equal("Remind \"Sunday Funday\" owner to configure Juice for season 2026", job.Description);
        Assert.Equal("Juice", job.Category);
        Assert.True(job.IsDynamic);
    }

    [Fact]
    public async Task GetAllJobsStatusAsync_JuiceReminderJob_UnknownLeagueId_DescriptionUnchanged()
    {
        // League was deleted (frizat-ugs pruning) or its data hasn't been fetched yet — must not
        // throw or produce a mangled description, just fall back to the raw text.
        var (_, scheduler, _, leagueRepo, controller) = BuildSutWithLeagueRepo(isAdmin: true);
        leagueRepo.GetAllLeaguesAsync().Returns(new List<LeagueInfo>());
        SetupJobOfType<LeagueJuiceReminderJob>(scheduler, "Juice Reminder 6-2026",
            "Remind league 6 owner to configure Juice for season 2026",
            jobData: new Dictionary<string, string> { ["LeagueId"] = "6" });

        var result = await controller.GetAllJobsStatusAsync();

        var job = Assert.Single(result);
        Assert.Equal("Remind league 6 owner to configure Juice for season 2026", job.Description);
    }

    [Fact]
    public async Task GetAllJobsStatusAsync_SchedulerJob_IsNotDynamic()
    {
        var (_, scheduler, _, _, controller) = BuildSutWithLeagueRepo(isAdmin: true);
        SetupJobOfType<UserManagerJob>(scheduler, "User Manager", "Manages initial user admin");

        var result = await controller.GetAllJobsStatusAsync();

        var job = Assert.Single(result);
        Assert.Equal("System", job.Category);
        Assert.False(job.IsDynamic);
    }

    private static void SetupJobOfType<TJob>(IScheduler scheduler, string name, string? description,
        Dictionary<string, string>? jobData = null) where TJob : IJob {
        var groupName = "DEFAULT";
        var jobKey = new JobKey(name);
        scheduler.GetJobGroupNames().Returns(new List<string> { groupName });
        scheduler.GetJobKeys(Arg.Any<GroupMatcher<JobKey>>()).Returns(new HashSet<JobKey> { jobKey });
        scheduler.GetCurrentlyExecutingJobs().Returns(new List<IJobExecutionContext>());
        scheduler.GetTriggerState(Arg.Any<TriggerKey>()).Returns(TriggerState.Normal);

        var builder = JobBuilder.Create<TJob>().WithIdentity(jobKey).WithDescription(description);
        if (jobData is not null) {
            var map = new JobDataMap();
            foreach (var (key, value) in jobData) map.Put(key, value);
            builder.UsingJobData(map);
        }
        var jobDetail = builder.Build();
        scheduler.GetJobDetail(jobKey).Returns(jobDetail);
        scheduler.GetTriggersOfJob(jobKey).Returns(new List<ITrigger>());
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static (ISchedulerFactory factory, IScheduler scheduler, IJobObserverService observer, JobManagerController controller)
        BuildSut(bool isAdmin = false) {
        var (factory, scheduler, observer, _, controller) = BuildSutWithLeagueRepo(isAdmin);
        return (factory, scheduler, observer, controller);
    }

    private static (ISchedulerFactory factory, IScheduler scheduler, IJobObserverService observer, ILeagueRepository leagueRepo, JobManagerController controller)
        BuildSutWithLeagueRepo(bool isAdmin = false)
    {
        var factory   = Substitute.For<ISchedulerFactory>();
        var scheduler = Substitute.For<IScheduler>();
        var observer  = Substitute.For<IJobObserverService>();
        var leagueRepo = Substitute.For<ILeagueRepository>();

        factory.GetScheduler().Returns(scheduler);
        observer.GetAllJobInfosAsync().Returns(new List<JobRunInfo>());
        leagueRepo.GetAllLeaguesAsync().Returns(new List<LeagueInfo>());

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "user-1"),
        };
        if (isAdmin)
            claims.Add(new Claim(ClaimTypes.Role, "Administrator"));

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));

        var controller = new JobManagerController(factory, observer, leagueRepo);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };

        return (factory, scheduler, observer, leagueRepo, controller);
    }

    /// <summary>
    /// Sets up the scheduler mock so that GetAllJobsStatusAsync returns several jobs, each with
    /// its own next-fire time, keyed by name.
    /// </summary>
    private static void SetupSchedulerWithJobs(IScheduler scheduler, params (string name, DateTimeOffset? nextRun)[] jobs)
    {
        var groupName = "DEFAULT";
        var jobKeys = jobs.Select(j => new JobKey(j.name)).ToHashSet();
        scheduler.GetJobGroupNames().Returns(new List<string> { groupName });
        scheduler.GetJobKeys(Arg.Any<GroupMatcher<JobKey>>()).Returns(jobKeys);
        scheduler.GetCurrentlyExecutingJobs().Returns(new List<IJobExecutionContext>());
        scheduler.GetTriggerState(Arg.Any<TriggerKey>()).Returns(TriggerState.Normal);

        foreach (var (name, nextRun) in jobs) {
            var jobKey = new JobKey(name);
            var jobDetail = Substitute.For<IJobDetail>();
            jobDetail.Key.Returns(jobKey);
            jobDetail.Description.Returns((string?)null);
            scheduler.GetJobDetail(jobKey).Returns(jobDetail);

            var trigger = Substitute.For<ITrigger>();
            trigger.GetNextFireTimeUtc().Returns(nextRun);
            scheduler.GetTriggersOfJob(jobKey).Returns(new List<ITrigger> { trigger });
        }
    }

    // ── GetNextSpreadJobAsync — sport scoping (frizat-9e7) ──────────────────

    [Fact]
    public async Task GetNextSpreadJobAsync_NoSport_ReturnsSoonestAcrossBothSports()
    {
        var (_, scheduler, _, controller) = BuildSut(isAdmin: true);
        var nflRun = DateTimeOffset.UtcNow.AddDays(3);
        var cfbRun = DateTimeOffset.UtcNow.AddDays(1);
        SetupSchedulerWithJobs(scheduler,
            ("NFL Spreads 2026 Wk6", nflRun),
            ("CFB Spreads 2026 Wk6", cfbRun));

        var result = await controller.GetNextSpreadJobAsync();

        Assert.Equal(cfbRun, result);
    }

    [Fact]
    public async Task GetNextSpreadJobAsync_SportNfl_IgnoresSoonerCfbJob()
    {
        var (_, scheduler, _, controller) = BuildSut(isAdmin: true);
        var nflRun = DateTimeOffset.UtcNow.AddDays(3);
        var cfbRun = DateTimeOffset.UtcNow.AddDays(1);
        SetupSchedulerWithJobs(scheduler,
            ("NFL Spreads 2026 Wk6", nflRun),
            ("CFB Spreads 2026 Wk6", cfbRun));

        var result = await controller.GetNextSpreadJobAsync("nfl");

        Assert.Equal(nflRun, result);
    }

    [Fact]
    public async Task GetNextSpreadJobAsync_SportCfb_IgnoresSoonerNflJob()
    {
        var (_, scheduler, _, controller) = BuildSut(isAdmin: true);
        var nflRun = DateTimeOffset.UtcNow.AddDays(1);
        var cfbRun = DateTimeOffset.UtcNow.AddDays(3);
        SetupSchedulerWithJobs(scheduler,
            ("NFL Spreads 2026 Wk6", nflRun),
            ("CFB Spreads 2026 Wk6", cfbRun));

        var result = await controller.GetNextSpreadJobAsync("cfb");

        Assert.Equal(cfbRun, result);
    }

    [Fact]
    public async Task GetNextSpreadJobAsync_IgnoresSchedulerJobs_EvenWhenSooner()
    {
        // NflSpreadSchedulerJob's own triggers ("NFL Spread Scheduler ...") must never be mistaken
        // for an actual per-week NflSpreadJob trigger ("NFL Spreads ...") just because both contain
        // "Spread" — the scheduler job's own next-run time is not a spread-fetch time.
        var (_, scheduler, _, controller) = BuildSut(isAdmin: true);
        var schedulerRun = DateTimeOffset.UtcNow.AddHours(1);
        var realSpreadRun = DateTimeOffset.UtcNow.AddDays(3);
        SetupSchedulerWithJobs(scheduler,
            ("NFL Spread Scheduler Daily", schedulerRun),
            ("NFL Spreads 2026 Wk6", realSpreadRun));

        var result = await controller.GetNextSpreadJobAsync("nfl");

        Assert.Equal(realSpreadRun, result);
    }

    [Fact]
    public async Task GetNextSpreadJobAsync_NoMatchingJobs_ReturnsNull()
    {
        var (_, scheduler, _, controller) = BuildSut(isAdmin: true);
        SetupSchedulerWithJobs(scheduler, ("CFB Slate Seeder", DateTimeOffset.UtcNow.AddDays(1)));

        var result = await controller.GetNextSpreadJobAsync("nfl");

        Assert.Null(result);
    }

    private static void SetupSchedulerWithJob(IScheduler scheduler, JobKey jobKey)
    {
        var groupName = "DEFAULT";
        scheduler.GetJobGroupNames().Returns(new List<string> { groupName });
        scheduler.GetJobKeys(Arg.Any<GroupMatcher<JobKey>>())
                 .Returns(new HashSet<JobKey> { jobKey });

        var jobDetail = Substitute.For<IJobDetail>();
        jobDetail.Key.Returns(jobKey);
        jobDetail.Description.Returns((string?)null);
        scheduler.GetJobDetail(jobKey).Returns(jobDetail);

        scheduler.GetTriggersOfJob(jobKey)
                 .Returns(new List<ITrigger>());

        scheduler.GetTriggerState(Arg.Any<TriggerKey>())
                 .Returns(TriggerState.Normal);

        scheduler.GetCurrentlyExecutingJobs()
                 .Returns(new List<IJobExecutionContext>());

        scheduler.TriggerJob(jobKey).Returns(Task.CompletedTask);
    }
}
