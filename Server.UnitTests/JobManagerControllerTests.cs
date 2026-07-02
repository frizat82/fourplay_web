using FourPlayWebApp.Server.Controllers;
using FourPlayWebApp.Server.Services.Interfaces;
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
        nameof(JobManagerController.RunMissingPicks),
        nameof(JobManagerController.RunSpreads),
        nameof(JobManagerController.RunScores),
        nameof(JobManagerController.RunUserJob),
        nameof(JobManagerController.RunCfbSlateSeeder),
        nameof(JobManagerController.RunCfbSpreads),
        nameof(JobManagerController.RunCfbScores),
        nameof(JobManagerController.DeleteJob),
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
        nameof(JobManagerController.GetAllJobsStatusAsync),
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

    [Fact]
    public async Task RunCfbSpreads_ReturnsOk_WhenSchedulerSucceeds()
    {
        var (factory, scheduler, observer, controller) = BuildSut(isAdmin: true);
        scheduler.TriggerJob(new JobKey("CFB Spread Job")).Returns(Task.CompletedTask);

        var result = await controller.RunCfbSpreads();

        Assert.IsType<OkObjectResult>(result);
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

    // ── Functional: RunMissingPicks — job found ────────────────────────────────

    [Fact]
    public async Task RunMissingPicks_ReturnsOk_WhenJobFoundAndTriggered()
    {
        var (factory, scheduler, observer, controller) = BuildSut(isAdmin: true);

        // Arrange: GetAllJobsStatusAsync -> GetJobGroupNames -> GetJobKeys -> GetJobDetail, etc.
        var jobKey = new JobKey("Missing Picks Job");
        SetupSchedulerWithJob(scheduler, jobKey);

        var result = await controller.RunMissingPicks();

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task RunMissingPicks_ReturnsNotFound_WhenNoMissingPicksJobExists()
    {
        var (factory, scheduler, observer, controller) = BuildSut(isAdmin: true);

        // No jobs at all → GetAllJobsStatusAsync returns empty list → FirstOrDefault = null
        scheduler.GetJobGroupNames().Returns(new List<string>());

        var result = await controller.RunMissingPicks();

        Assert.IsType<NotFoundResult>(result);
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

    // ── Functional: RunScores — no scores job ────────────────────────────────

    [Fact]
    public async Task RunScores_ReturnsNotFound_WhenNoScoresJobExists()
    {
        var (factory, scheduler, observer, controller) = BuildSut(isAdmin: true);
        scheduler.GetJobGroupNames().Returns(new List<string>());

        var result = await controller.RunScores();

        Assert.IsType<NotFoundResult>(result);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static (ISchedulerFactory factory, IScheduler scheduler, IJobObserverService observer, JobManagerController controller)
        BuildSut(bool isAdmin = false)
    {
        var factory   = Substitute.For<ISchedulerFactory>();
        var scheduler = Substitute.For<IScheduler>();
        var observer  = Substitute.For<IJobObserverService>();

        factory.GetScheduler().Returns(scheduler);
        observer.GetAllJobInfosAsync().Returns(new List<JobRunInfo>());

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "user-1"),
        };
        if (isAdmin)
            claims.Add(new Claim(ClaimTypes.Role, "Administrator"));

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));

        var controller = new JobManagerController(factory, observer);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };

        return (factory, scheduler, observer, controller);
    }

    /// <summary>
    /// Sets up the scheduler mock so that GetAllJobsStatusAsync returns one job
    /// whose name contains the given JobKey name.
    /// </summary>
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
