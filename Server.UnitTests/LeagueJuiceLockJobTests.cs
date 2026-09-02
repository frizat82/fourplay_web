using FourPlayWebApp.Server.Jobs;
using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Server.Services.Repositories.Interfaces;
using NSubstitute;
using Quartz;

namespace FourPlayWebApp.Server.UnitTests;

// frizat-ugs: fires once per (league, season) at lock time (2pm America/Chicago on the season's
// first game date). Auto-fills Juice — carries forward the prior season's values, or falls back
// to entity defaults if no prior season exists — if still unconfigured at fire time.
public class LeagueJuiceLockJobTests
{
    private readonly ILeagueRepository _repo;
    private readonly IJobObserverService _observer;
    private readonly IJobExecutionContext _context;

    public LeagueJuiceLockJobTests()
    {
        _repo = Substitute.For<ILeagueRepository>();
        _observer = Substitute.For<IJobObserverService>();
        _context = Substitute.For<IJobExecutionContext>();

        var jobData = new JobDataMap();
        jobData.Put("LeagueId", "1");
        jobData.Put("Season", "2026");
        _context.MergedJobDataMap.Returns(jobData);
        // /code-review: the job must record under the actual Quartz JobKey ("Juice Lock 1-2026",
        // per TimedTriggerScheduler/LeagueJuiceScheduleSource's candidate.Identity) — not the
        // static class name — since JobManagerController correlates observer info by
        // jobDetail.Key.Name. Recording under nameof(LeagueJuiceLockJob) meant every league's/
        // season's rich success message clobbered every other's under one shared key, and none
        // of them were ever visible under the Job Manager row an admin actually looks at.
        var jobDetail = Substitute.For<IJobDetail>();
        jobDetail.Key.Returns(new JobKey("Juice Lock 1-2026"));
        _context.JobDetail.Returns(jobDetail);
        _repo.GetLeagueJuiceMappingAsync(1).Returns(new List<LeagueJuiceMapping>());
    }

    private LeagueJuiceLockJob BuildJob() => new(_repo, _observer);

    [Fact]
    public async Task DoesNothing_WhenJuiceWasConfiguredSinceScheduling()
    {
        _repo.GetLeagueJuiceMappingAsync(1, 2026).Returns(new LeagueJuiceMapping { LeagueId = 1, Season = 2026 });

        await BuildJob().Execute(_context);

        await _repo.DidNotReceive().AddLeagueJuiceMappingAsync(Arg.Any<LeagueJuiceMapping>());
    }

    [Fact]
    public async Task CarriesForwardPriorSeason_WhenOneExists()
    {
        _repo.GetLeagueJuiceMappingAsync(1, 2026).Returns((LeagueJuiceMapping?)null);
        _repo.GetLeagueJuiceMappingAsync(1).Returns(new List<LeagueJuiceMapping> {
            new() { LeagueId = 1, Season = 2025, Juice = 20, JuiceDivisional = 15, JuiceConference = 8, WeeklyCost = 12 },
        });

        await BuildJob().Execute(_context);

        await _repo.Received(1).AddLeagueJuiceMappingAsync(Arg.Is<LeagueJuiceMapping>(m =>
            m.LeagueId == 1 && m.Season == 2026 &&
            m.Juice == 20 && m.JuiceDivisional == 15 && m.JuiceConference == 8 && m.WeeklyCost == 12));
    }

    [Fact]
    public async Task FallsBackToDefaults_WhenNoPriorSeasonExists()
    {
        _repo.GetLeagueJuiceMappingAsync(1, 2026).Returns((LeagueJuiceMapping?)null);
        var expectedDefaults = new LeagueJuiceMapping();

        await BuildJob().Execute(_context);

        await _repo.Received(1).AddLeagueJuiceMappingAsync(Arg.Is<LeagueJuiceMapping>(m =>
            m.LeagueId == 1 && m.Season == 2026 &&
            m.Juice == expectedDefaults.Juice && m.JuiceDivisional == expectedDefaults.JuiceDivisional &&
            m.JuiceConference == expectedDefaults.JuiceConference && m.WeeklyCost == expectedDefaults.WeeklyCost));
    }

    [Fact]
    public async Task RecordsJobSuccess_AfterFillingJuice()
    {
        _repo.GetLeagueJuiceMappingAsync(1, 2026).Returns((LeagueJuiceMapping?)null);

        await BuildJob().Execute(_context);

        await _observer.Received(1).RecordJobSuccessAsync("Juice Lock 1-2026", Arg.Any<string>());
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
        var boom = new InvalidOperationException("DB unavailable");
        _repo.AddLeagueJuiceMappingAsync(Arg.Any<LeagueJuiceMapping>()).Returns<Task>(_ => throw boom);

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => BuildJob().Execute(_context));

        Assert.Same(boom, thrown);
        await _observer.Received(1).RecordJobFailureAsync("Juice Lock 1-2026", "DB unavailable");
    }
}
