using FourPlayWebApp.Server.Controllers;
using FourPlayWebApp.Server.Jobs;
using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Server.Services.Repositories.Interfaces;
using FourPlayWebApp.Shared.Models.Enum;
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

    // ── Functional: RunScores / RunCfbScores ──────────────────────────────────
    // Each sport's scores job is one job key with all its fixed times as triggers (so Quartz's
    // [DisallowConcurrentExecution] keeps every run, scheduled or on-final, one at a time). "Run
    // now" runs that key; demo stacks never register it, so it's a 404 there.

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RunScores_RunsThatSportsScoresJob(bool cfb)
    {
        var (_, scheduler, _, controller) = BuildSut(isAdmin: true);
        var key = cfb ? ScoresJobs.Cfb : ScoresJobs.Nfl;
        scheduler.CheckExists(key).Returns(true);

        var result = cfb ? await controller.RunCfbScores() : await controller.RunScores();

        Assert.IsType<OkObjectResult>(result);
        await scheduler.Received(1).TriggerJob(key);
        await scheduler.DidNotReceive().TriggerJob(cfb ? ScoresJobs.Nfl : ScoresJobs.Cfb);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RunScores_ReturnsNotFound_WhenTheScoresJobIsNotRegistered(bool cfb)
    {
        var (_, scheduler, _, controller) = BuildSut(isAdmin: true);
        scheduler.CheckExists(Arg.Any<JobKey>()).Returns(false);

        var result = cfb ? await controller.RunCfbScores() : await controller.RunScores();

        Assert.IsType<NotFoundResult>(result);
        await scheduler.DidNotReceive().TriggerJob(Arg.Any<JobKey>());
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

    // ── Functional: GetAllJobsStatusAsync — category/description enrichment ───
    // frizat: "Juice-Reminder-6" was reported unreadable — 6 is a raw league DB id, not a league
    // count. Description now substitutes the actual league name; Category/IsDynamic (from
    // JobCategoryClassifier) let the admin UI group jobs and hide the noisy per-league/per-week set.

    // The admin list's League column is the sport a job is for — NFL or CFB — never a user
    // league's name. A per-league juice job is for its league's sport.
    [Theory]
    [InlineData(LeagueType.Nfl, "NFL")]
    [InlineData(LeagueType.Cfb, "CFB")]
    public async Task GetAllJobsStatusAsync_JuiceReminderJob_ShowsItsLeaguesSport(LeagueType leagueType, string expected)
    {
        var (_, scheduler, _, leagueRepo, controller) = BuildSutWithLeagueRepo(isAdmin: true);
        leagueRepo.GetAllLeaguesAsync().Returns(new List<LeagueInfo> {
            new() { Id = 6, LeagueName = "Sunday Funday", OwnerUserId = "u1", LeagueType = leagueType },
        });
        SetupJobOfType<LeagueJuiceReminderJob>(scheduler, "Juice Reminder 6-2026",
            "Remind league 6 owner to configure Juice for season 2026",
            jobData: new Dictionary<string, string> { ["LeagueId"] = "6" });

        var result = await controller.GetAllJobsStatusAsync();

        var job = Assert.Single(result);
        Assert.Equal("Remind league 6 owner to configure Juice for season 2026", job.Description);
        Assert.Equal(6, job.LeagueId);
        Assert.Equal(expected, job.Sport);
        Assert.Equal("Juice", job.Category);
        Assert.True(job.IsDynamic);
    }

    [Fact]
    public async Task GetAllJobsStatusAsync_JuiceReminderJob_UnknownLeagueId_HasNoSport()
    {
        // League was deleted (frizat-ugs pruning) or its data hasn't been fetched yet — must not
        // throw, just leave the sport unresolved.
        var (_, scheduler, _, leagueRepo, controller) = BuildSutWithLeagueRepo(isAdmin: true);
        leagueRepo.GetAllLeaguesAsync().Returns(new List<LeagueInfo>());
        SetupJobOfType<LeagueJuiceReminderJob>(scheduler, "Juice Reminder 6-2026",
            "Remind league 6 owner to configure Juice for season 2026",
            jobData: new Dictionary<string, string> { ["LeagueId"] = "6" });

        var result = await controller.GetAllJobsStatusAsync();

        var job = Assert.Single(result);
        Assert.Equal(6, job.LeagueId);
        Assert.Null(job.Sport);
    }

    // Scores, spreads, the spread schedulers and rankings are each for one sport — known from the
    // job type alone, no league lookup.
    [Theory]
    [InlineData(typeof(NflScoresJob), "NFL")]
    [InlineData(typeof(NflSpreadJob), "NFL")]
    [InlineData(typeof(NflSpreadSchedulerJob), "NFL")]
    [InlineData(typeof(CfbScoresJob), "CFB")]
    [InlineData(typeof(CfbSpreadJob), "CFB")]
    [InlineData(typeof(CfbSpreadSchedulerJob), "CFB")]
    [InlineData(typeof(CfbRankingCaptureJob), "CFB")]
    public async Task GetAllJobsStatusAsync_SportWideJob_ShowsItsSport_WithoutLoadingLeagues(Type jobType, string expected)
    {
        var (_, scheduler, _, leagueRepo, controller) = BuildSutWithLeagueRepo(isAdmin: true);
        SetupJobOfType(jobType, scheduler, "Some Job", "desc");

        var job = Assert.Single(await controller.GetAllJobsStatusAsync());

        Assert.Equal(expected, job.Sport);
        await leagueRepo.DidNotReceive().GetAllLeaguesAsync();
    }

    [Fact]
    public async Task GetAllJobsStatusAsync_SystemJob_HasNoSport()
    {
        var (_, scheduler, _, _, controller) = BuildSutWithLeagueRepo(isAdmin: true);
        SetupJobOfType<UserManagerJob>(scheduler, "User Manager", "Manages initial user admin");

        var job = Assert.Single(await controller.GetAllJobsStatusAsync());

        Assert.Null(job.Sport);
    }

    // frizat: GetNextSpreadJobAsync (below) is [Authorize]-only, no admin role — it's on the hot
    // path for every logged-in user's picks page via SpreadRelease.tsx. It must never pay for the
    // league lookup the admin list does for juice jobs.
    [Fact]
    public async Task GetNextSpreadJobAsync_NeverLoadsLeagues()
    {
        var (_, scheduler, _, leagueRepo, controller) = BuildSutWithLeagueRepo(isAdmin: false);
        SetupJobOfType<LeagueJuiceReminderJob>(scheduler, "Juice Reminder 6-2026", "desc",
            jobData: new Dictionary<string, string> { ["LeagueId"] = "6" });

        await controller.GetNextSpreadJobAsync("nfl");

        await leagueRepo.DidNotReceive().GetAllLeaguesAsync();
    }

    [Fact]
    public async Task GetAllJobsStatusAsync_NoJobReferencesALeague_NeverCallsLeagueRepo()
    {
        var (_, scheduler, _, leagueRepo, controller) = BuildSutWithLeagueRepo(isAdmin: true);
        SetupJobOfType<UserManagerJob>(scheduler, "User Manager", "Manages initial user admin");

        await controller.GetAllJobsStatusAsync();

        await leagueRepo.DidNotReceive().GetAllLeaguesAsync();
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
        Dictionary<string, string>? jobData = null) where TJob : IJob =>
        SetupJobOfType(typeof(TJob), scheduler, name, description, jobData);

    private static void SetupJobOfType(Type jobType, IScheduler scheduler, string name, string? description,
        Dictionary<string, string>? jobData = null) {
        var groupName = "DEFAULT";
        var jobKey = new JobKey(name);
        scheduler.GetJobGroupNames().Returns(new List<string> { groupName });
        scheduler.GetJobKeys(Arg.Any<GroupMatcher<JobKey>>()).Returns(new HashSet<JobKey> { jobKey });
        scheduler.GetCurrentlyExecutingJobs().Returns(new List<IJobExecutionContext>());
        scheduler.GetTriggerState(Arg.Any<TriggerKey>()).Returns(TriggerState.Normal);

        var builder = JobBuilder.Create(jobType).WithIdentity(jobKey).WithDescription(description);
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

    // ── GetAllJobsStatusAsync — order within a category ───────────────────
    // Per-week spread jobs are named "NFL Spreads 2026 Wk{n}": sorted by name, "Wk10".."Wk19" all
    // land before "Wk7", so the Job Manager's first NFL spread row read as week 10 while week 7
    // was the one actually up next. Within a category, soonest-to-run first; never-scheduled last.
    [Fact]
    public async Task GetAllJobsStatusAsync_WithinACategory_ListsSoonestNextRunFirst_NotByName()
    {
        var (_, scheduler, _, controller) = BuildSut(isAdmin: true);
        var now = DateTimeOffset.UtcNow;
        SetupSchedulerWithJobs(scheduler,
            ("NFL Spreads 2026 Wk10", now.AddDays(25)),
            ("NFL Spreads 2026 Wk7", now.AddDays(4)),
            ("NFL Spreads 2026 Wk8", now.AddDays(11)),
            ("NFL Spreads Not Scheduled", null));

        var names = (await controller.GetAllJobsStatusAsync()).Select(j => j.JobName).ToList();

        Assert.Equal(new[] { "NFL Spreads 2026 Wk7", "NFL Spreads 2026 Wk8", "NFL Spreads 2026 Wk10", "NFL Spreads Not Scheduled" }, names);
    }

    // One job, several fixed times: its row shows the soonest of them, and that trigger's state.
    [Fact]
    public async Task GetAllJobsStatusAsync_JobWithSeveralTriggers_ShowsTheSoonestNextRun()
    {
        var (_, scheduler, _, controller) = BuildSut(isAdmin: true);
        var now = DateTimeOffset.UtcNow;
        SetupSchedulerWithJobs(scheduler, ("NFL Scores", now.AddDays(3)));
        var later = Substitute.For<ITrigger>();
        later.GetNextFireTimeUtc().Returns(now.AddDays(3));
        later.Key.Returns(new TriggerKey("NFL Scores Tue 1am"));
        var sooner = Substitute.For<ITrigger>();
        sooner.GetNextFireTimeUtc().Returns(now.AddHours(2));
        sooner.Key.Returns(new TriggerKey("NFL Scores Sun 4:30pm"));
        scheduler.GetTriggersOfJob(new JobKey("NFL Scores")).Returns(new List<ITrigger> { later, sooner });
        scheduler.GetTriggerState(new TriggerKey("NFL Scores Sun 4:30pm")).Returns(TriggerState.Paused);

        var job = Assert.Single(await controller.GetAllJobsStatusAsync());

        Assert.Equal(now.AddHours(2), job.NextRun);
        Assert.Equal(nameof(TriggerState.Paused), job.Status);
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
