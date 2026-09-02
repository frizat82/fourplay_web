using FourPlayWebApp.Server.Jobs;
using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Models.Identity;
using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Server.Services.Repositories.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using NSubstitute;
using Quartz;

namespace FourPlayWebApp.Server.UnitTests;

// frizat-ugs: fires once per (league, season) at reminder time. Always re-checks freshness at
// fire time rather than trusting the scheduler's snapshot — the owner may have configured Juice
// in the days between scheduling and firing.
public class LeagueJuiceReminderJobTests
{
    private readonly ILeagueRepository _repo;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEmailSender _emailSender;
    private readonly IJobObserverService _observer;
    private readonly IJobExecutionContext _context;

    public LeagueJuiceReminderJobTests()
    {
        Environment.SetEnvironmentVariable("APP_URL", "https://ivleague.com");
        _repo = Substitute.For<ILeagueRepository>();
        var store = Substitute.For<IUserStore<ApplicationUser>>();
        _userManager = Substitute.For<UserManager<ApplicationUser>>(store, null, null, null, null, null, null, null, null);
        _emailSender = Substitute.For<IEmailSender>();
        _observer = Substitute.For<IJobObserverService>();
        _context = Substitute.For<IJobExecutionContext>();

        var jobData = new JobDataMap();
        jobData.Put("LeagueId", "1");
        jobData.Put("Season", "2026");
        _context.MergedJobDataMap.Returns(jobData);
        // /code-review: the job must record under the actual Quartz JobKey ("Juice Reminder
        // 1-2026", per TimedTriggerScheduler/LeagueJuiceScheduleSource's candidate.Identity) —
        // not the static class name — since JobManagerController correlates observer info by
        // jobDetail.Key.Name. Recording under nameof(LeagueJuiceReminderJob) meant every league's/
        // season's rich success message clobbered every other's under one shared key, and none
        // of them were ever visible under the Job Manager row an admin actually looks at.
        var jobDetail = Substitute.For<IJobDetail>();
        jobDetail.Key.Returns(new JobKey("Juice Reminder 1-2026"));
        _context.JobDetail.Returns(jobDetail);

        _repo.GetLeagueInfoAsync(1).Returns(new LeagueInfo { Id = 1, LeagueName = "Test League", OwnerUserId = "owner-1" });
        _repo.GetJuiceRemindersSentAsync().Returns(new HashSet<(int, int)>());
        _userManager.FindByIdAsync("owner-1").Returns(new ApplicationUser { Id = "owner-1", Email = "owner@example.com" });
    }

    private LeagueJuiceReminderJob BuildJob() => new(_repo, _userManager, _emailSender, _observer);

    [Fact]
    public async Task SendsReminderEmail_WhenJuiceStillUnconfigured()
    {
        _repo.GetLeagueJuiceMappingAsync(1, 2026).Returns((LeagueJuiceMapping?)null);

        await BuildJob().Execute(_context);

        await _emailSender.Received(1).SendEmailAsync("owner@example.com", Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task DoesNotSendEmail_WhenJuiceWasConfiguredSinceScheduling()
    {
        // Re-check at fire time — the candidate was scheduled when Juice was missing, but the
        // owner configured it in the meantime.
        _repo.GetLeagueJuiceMappingAsync(1, 2026).Returns(new LeagueJuiceMapping { LeagueId = 1, Season = 2026 });

        await BuildJob().Execute(_context);

        await _emailSender.DidNotReceive().SendEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    // /code-review: "Juice configured" isn't a valid "already reminded" signal on its own (sending
    // this email doesn't configure anything) — a persisted marker is the real "already sent"
    // signal, re-checked here in case of a rare double-fire race.
    [Fact]
    public async Task DoesNotSendEmail_WhenReminderWasAlreadySent()
    {
        _repo.GetLeagueJuiceMappingAsync(1, 2026).Returns((LeagueJuiceMapping?)null);
        _repo.GetJuiceRemindersSentAsync().Returns(new HashSet<(int, int)> { (1, 2026) });

        await BuildJob().Execute(_context);

        await _emailSender.DidNotReceive().SendEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    // /code-review: league names are free-text and fully user-controlled (unlike usernames, which
    // ASP.NET Identity already restricts to a safe character set) — must be HTML-encoded before
    // landing in the email body.
    [Fact]
    public async Task HtmlEncodesLeagueName_InEmailBody()
    {
        _repo.GetLeagueJuiceMappingAsync(1, 2026).Returns((LeagueJuiceMapping?)null);
        _repo.GetLeagueInfoAsync(1).Returns(new LeagueInfo { Id = 1, LeagueName = "<script>alert(1)</script>", OwnerUserId = "owner-1" });

        await BuildJob().Execute(_context);

        await _emailSender.Received(1).SendEmailAsync(
            "owner@example.com", Arg.Any<string>(),
            Arg.Is<string>(body => !body.Contains("<script>") && body.Contains("&lt;script&gt;")));
    }

    [Fact]
    public async Task RecordsReminderSent_AfterSendingEmail()
    {
        _repo.GetLeagueJuiceMappingAsync(1, 2026).Returns((LeagueJuiceMapping?)null);

        await BuildJob().Execute(_context);

        await _repo.Received(1).RecordJuiceReminderSentAsync(1, 2026);
    }

    // frizat-703.2: a missing owner email is a real misconfiguration an admin needs to act on —
    // it must throw (not swallow-and-return) so it reaches Quartz's jobException and the global
    // JobFailureAlertListener/Discord alert, same as any other unhandled failure in this job.
    [Fact]
    public async Task Throws_AndRecordsFailure_WhenOwnerHasNoEmail()
    {
        _repo.GetLeagueJuiceMappingAsync(1, 2026).Returns((LeagueJuiceMapping?)null);
        _userManager.FindByIdAsync("owner-1").Returns(new ApplicationUser { Id = "owner-1", Email = null });

        await Assert.ThrowsAsync<InvalidOperationException>(() => BuildJob().Execute(_context));

        await _emailSender.DidNotReceive().SendEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
        await _observer.Received(1).RecordJobFailureAsync("Juice Reminder 1-2026", Arg.Is<string>(m => m.Contains("no email on file")));
    }

    [Fact]
    public async Task RecordsJobSuccess_AfterSendingReminder()
    {
        _repo.GetLeagueJuiceMappingAsync(1, 2026).Returns((LeagueJuiceMapping?)null);

        await BuildJob().Execute(_context);

        await _observer.Received(1).RecordJobSuccessAsync("Juice Reminder 1-2026", Arg.Any<string>());
    }

    // frizat-703.2: an unhandled exception must propagate to Quartz (not be swallowed after
    // logging) so the global JobFailureAlertListener — which only fires when JobWasExecuted
    // receives a non-null jobException — actually sees this job's failures. Recording via
    // IJobObserverService is for the existing admin job-monitor UI; it's not a substitute alert
    // path, since RecordJobFailureAsync has no wiring to the Discord notifier.
    [Fact]
    public async Task RecordsFailure_ThenRethrows_OnUnexpectedException()
    {
        _repo.GetLeagueJuiceMappingAsync(1, 2026).Returns((LeagueJuiceMapping?)null);
        var boom = new InvalidOperationException("SMTP unavailable");
        _emailSender.SendEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns<Task>(_ => throw boom);

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => BuildJob().Execute(_context));

        Assert.Same(boom, thrown);
        await _observer.Received(1).RecordJobFailureAsync("Juice Reminder 1-2026", "SMTP unavailable");
    }
}
