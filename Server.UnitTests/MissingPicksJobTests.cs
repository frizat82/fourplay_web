using FourPlayWebApp.Server.Jobs;
using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Models.Identity;
using FourPlayWebApp.Server.Services;
using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Server.Services.Repositories.Interfaces;
using FourPlayWebApp.Shared.Models;
using Microsoft.AspNetCore.Identity.UI.Services;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Quartz;

namespace FourPlayWebApp.Server.UnitTests;

/// <summary>
/// Tests for MissingPicksJob — the weekly reminder that emails users
/// who have not submitted their required picks before the deadline.
///
/// These tests establish a baseline so future changes don't silently break
/// the reminder email flow.
/// </summary>
public class MissingPicksJobTests : IDisposable
{
    private const string AppUrl = "https://test.fourplay.app";
    private const int Season = 2024;
    private const int RegularSeasonType = (int)TypeOfSeason.RegularSeason;

    private readonly ILeagueRepository _repo;
    private readonly IEspnApiService _espnApi;
    private readonly IEmailSender _emailSender;
    private readonly IJobObserverService _observer;
    private readonly IJobExecutionContext _context;
    private readonly string? _previousAppUrl;

    public MissingPicksJobTests()
    {
        _previousAppUrl = Environment.GetEnvironmentVariable("APP_URL");
        Environment.SetEnvironmentVariable("APP_URL", AppUrl);

        _repo = Substitute.For<ILeagueRepository>();
        _espnApi = Substitute.For<IEspnApiService>();
        _emailSender = Substitute.For<IEmailSender>();
        _observer = Substitute.For<IJobObserverService>();
        _context = Substitute.For<IJobExecutionContext>();
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("APP_URL", _previousAppUrl);
    }

    private MissingPicksJob BuildJob() =>
        new(_repo, _espnApi, _emailSender, _observer);

    private static EspnScores BuildScoreboard(int weekNumber, int seasonType = RegularSeasonType) =>
        new()
        {
            Season = new Season { Year = Season, Type = seasonType },
            Week = new Week { Number = weekNumber }
        };

    private static ApplicationUser BuildUser(string email = "user@example.com") =>
        new() { Id = Guid.NewGuid().ToString(), UserName = "testuser", Email = email };

    private static LeagueInfo BuildLeague(string name = "Test League") =>
        new() { Id = 1, LeagueName = name, OwnerUserId = "owner" };

    private static LeagueUserMapping BuildMapping(ApplicationUser user, LeagueInfo league) =>
        new() { LeagueId = league.Id, League = league, UserId = user.Id, User = user };

    // -------------------------------------------------------------------------
    // Scoreboard failures
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Execute_WhenScoreboardNull_RecordsFailure_SendsNoEmails()
    {
        _espnApi.GetScores().Returns((EspnScores?)null);

        await BuildJob().Execute(_context);

        await _observer.Received(1).RecordJobFailureAsync(
            nameof(MissingPicksJob),
            Arg.Any<string>());
        await _emailSender.DidNotReceive().SendEmailAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task Execute_WhenWeekNull_RecordsFailure_SendsNoEmails()
    {
        _espnApi.GetScores().Returns(new EspnScores
        {
            Season = new Season { Year = Season, Type = RegularSeasonType },
            Week = null
        });

        await BuildJob().Execute(_context);

        await _observer.Received(1).RecordJobFailureAsync(
            nameof(MissingPicksJob),
            Arg.Any<string>());
        await _emailSender.DidNotReceive().SendEmailAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    // -------------------------------------------------------------------------
    // No users
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Execute_WhenNoUsers_RecordsSuccess_SendsNoEmails()
    {
        _espnApi.GetScores().Returns(BuildScoreboard(weekNumber: 5));
        _repo.GetUsersAsync().Returns(new List<ApplicationUser>());

        await BuildJob().Execute(_context);

        await _observer.Received(1).RecordJobSuccessAsync(
            nameof(MissingPicksJob),
            Arg.Any<string>());
        await _emailSender.DidNotReceive().SendEmailAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    // -------------------------------------------------------------------------
    // User has all required picks — no email
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Execute_WhenUserHasAllRequiredPicks_SendsNoEmail()
    {
        var user = BuildUser();
        var league = BuildLeague();
        var mapping = BuildMapping(user, league);

        _espnApi.GetScores().Returns(BuildScoreboard(weekNumber: 5));
        _repo.GetUsersAsync().Returns(new List<ApplicationUser> { user });
        _repo.GetLeagueUserMappingsAsync(user).Returns(new List<LeagueUserMapping> { mapping });

        // 4 picks for a regular-season week — meets the requirement
        _repo.GetUserNflPicksAsync(user.Id, league.Id, Season, 5)
             .Returns(new List<NflPicks>
             {
                 new() { UserId = user.Id }, new() { UserId = user.Id },
                 new() { UserId = user.Id }, new() { UserId = user.Id }
             });

        await BuildJob().Execute(_context);

        await _emailSender.DidNotReceive().SendEmailAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    // -------------------------------------------------------------------------
    // User is missing picks — email sent
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Execute_WhenUserMissingPicks_SendsReminderEmail()
    {
        var user = BuildUser("picks@example.com");
        var league = BuildLeague();
        var mapping = BuildMapping(user, league);

        _espnApi.GetScores().Returns(BuildScoreboard(weekNumber: 5));
        _repo.GetUsersAsync().Returns(new List<ApplicationUser> { user });
        _repo.GetLeagueUserMappingsAsync(user).Returns(new List<LeagueUserMapping> { mapping });

        // Only 2 of required 4 picks submitted
        _repo.GetUserNflPicksAsync(user.Id, league.Id, Season, 5)
             .Returns(new List<NflPicks> { new() { UserId = user.Id }, new() { UserId = user.Id } });

        await BuildJob().Execute(_context);

        await _emailSender.Received(1).SendEmailAsync(
            "picks@example.com",
            Arg.Is<string>(s => s.Contains("Missing")),
            Arg.Any<string>());
    }

    [Fact]
    public async Task Execute_WhenUserHasNoPicks_SendsReminderEmail()
    {
        var user = BuildUser("nopicks@example.com");
        var league = BuildLeague();
        var mapping = BuildMapping(user, league);

        _espnApi.GetScores().Returns(BuildScoreboard(weekNumber: 3));
        _repo.GetUsersAsync().Returns(new List<ApplicationUser> { user });
        _repo.GetLeagueUserMappingsAsync(user).Returns(new List<LeagueUserMapping> { mapping });
        _repo.GetUserNflPicksAsync(user.Id, league.Id, Season, 3)
             .Returns(new List<NflPicks>());

        await BuildJob().Execute(_context);

        await _emailSender.Received(1).SendEmailAsync(
            "nopicks@example.com",
            Arg.Any<string>(),
            Arg.Any<string>());
    }

    // -------------------------------------------------------------------------
    // User has no email address
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Execute_WhenUserHasNoEmail_SkipsEmail_DoesNotThrow()
    {
        var user = BuildUser(email: null!);
        user.Email = null;
        var league = BuildLeague();
        var mapping = BuildMapping(user, league);

        _espnApi.GetScores().Returns(BuildScoreboard(weekNumber: 5));
        _repo.GetUsersAsync().Returns(new List<ApplicationUser> { user });
        _repo.GetLeagueUserMappingsAsync(user).Returns(new List<LeagueUserMapping> { mapping });
        _repo.GetUserNflPicksAsync(user.Id, league.Id, Season, 5)
             .Returns(new List<NflPicks>());

        // Should not throw
        var exception = await Record.ExceptionAsync(() => BuildJob().Execute(_context));
        Assert.Null(exception);

        await _emailSender.DidNotReceive().SendEmailAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    // -------------------------------------------------------------------------
    // Email content assertions
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Execute_EmailBody_ContainsLeagueName_AndMissingPickCount()
    {
        var user = BuildUser("content@example.com");
        var league = BuildLeague("My Test League");
        var mapping = BuildMapping(user, league);

        _espnApi.GetScores().Returns(BuildScoreboard(weekNumber: 7));
        _repo.GetUsersAsync().Returns(new List<ApplicationUser> { user });
        _repo.GetLeagueUserMappingsAsync(user).Returns(new List<LeagueUserMapping> { mapping });
        _repo.GetUserNflPicksAsync(user.Id, league.Id, Season, 7)
             .Returns(new List<NflPicks> { new() { UserId = user.Id } }); // 1 of 4

        string? capturedBody = null;
        await _emailSender.SendEmailAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Do<string>(b => capturedBody = b));

        await BuildJob().Execute(_context);

        Assert.NotNull(capturedBody);
        Assert.Contains("My Test League", capturedBody);
        Assert.Contains("3", capturedBody); // 3 missing (4 required - 1 submitted)
    }

    [Fact]
    public async Task Execute_EmailBody_ContainsAppUrl()
    {
        var user = BuildUser("url@example.com");
        var league = BuildLeague();
        var mapping = BuildMapping(user, league);

        _espnApi.GetScores().Returns(BuildScoreboard(weekNumber: 7));
        _repo.GetUsersAsync().Returns(new List<ApplicationUser> { user });
        _repo.GetLeagueUserMappingsAsync(user).Returns(new List<LeagueUserMapping> { mapping });
        _repo.GetUserNflPicksAsync(user.Id, league.Id, Season, 7)
             .Returns(new List<NflPicks>());

        string? capturedBody = null;
        await _emailSender.SendEmailAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Do<string>(b => capturedBody = b));

        await BuildJob().Execute(_context);

        Assert.NotNull(capturedBody);
        Assert.Contains(AppUrl, capturedBody);
    }

    // -------------------------------------------------------------------------
    // Resilience — email failure does not abort processing other users
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Execute_WhenEmailSendFails_ContinuesProcessingOtherUsers()
    {
        var user1 = BuildUser("fail@example.com");
        user1.Id = "user-1";
        var user2 = BuildUser("succeed@example.com");
        user2.Id = "user-2";
        var league = BuildLeague();

        _espnApi.GetScores().Returns(BuildScoreboard(weekNumber: 5));
        _repo.GetUsersAsync().Returns(new List<ApplicationUser> { user1, user2 });

        _repo.GetLeagueUserMappingsAsync(user1)
             .Returns(new List<LeagueUserMapping> { BuildMapping(user1, league) });
        _repo.GetLeagueUserMappingsAsync(user2)
             .Returns(new List<LeagueUserMapping> { BuildMapping(user2, league) });

        _repo.GetUserNflPicksAsync(user1.Id, league.Id, Season, 5).Returns(new List<NflPicks>());
        _repo.GetUserNflPicksAsync(user2.Id, league.Id, Season, 5).Returns(new List<NflPicks>());

        // First user's email fails
        _emailSender
            .SendEmailAsync("fail@example.com", Arg.Any<string>(), Arg.Any<string>())
            .ThrowsAsync(new InvalidOperationException("SMTP failure"));

        await BuildJob().Execute(_context);

        // Second user's email must still be sent despite first failing
        await _emailSender.Received(1).SendEmailAsync(
            "succeed@example.com",
            Arg.Any<string>(),
            Arg.Any<string>());

        await _observer.Received(1).RecordJobSuccessAsync(nameof(MissingPicksJob), Arg.Any<string>());
    }

    // -------------------------------------------------------------------------
    // Multiple leagues aggregated into a single email per user
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Execute_WhenUserMissingPicksInMultipleLeagues_SendsOneEmailWithAllLeagues()
    {
        var user = BuildUser("multi@example.com");
        var league1 = new LeagueInfo { Id = 1, LeagueName = "Alpha League", OwnerUserId = "owner" };
        var league2 = new LeagueInfo { Id = 2, LeagueName = "Beta League", OwnerUserId = "owner" };
        var mapping1 = BuildMapping(user, league1);
        var mapping2 = new LeagueUserMapping { LeagueId = league2.Id, League = league2, UserId = user.Id, User = user };

        _espnApi.GetScores().Returns(BuildScoreboard(weekNumber: 8));
        _repo.GetUsersAsync().Returns(new List<ApplicationUser> { user });
        _repo.GetLeagueUserMappingsAsync(user)
             .Returns(new List<LeagueUserMapping> { mapping1, mapping2 });

        _repo.GetUserNflPicksAsync(user.Id, 1, Season, 8).Returns(new List<NflPicks>());
        _repo.GetUserNflPicksAsync(user.Id, 2, Season, 8).Returns(new List<NflPicks>());

        string? capturedBody = null;
        await _emailSender.SendEmailAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Do<string>(b => capturedBody = b));

        await BuildJob().Execute(_context);

        // Only ONE email sent (aggregated)
        await _emailSender.Received(1).SendEmailAsync(
            "multi@example.com",
            Arg.Any<string>(),
            Arg.Any<string>());

        Assert.NotNull(capturedBody);
        Assert.Contains("Alpha League", capturedBody);
        Assert.Contains("Beta League", capturedBody);
    }

    // -------------------------------------------------------------------------
    // Postseason — uses correct required picks count
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Execute_WildCardWeek_UsesThreeRequiredPicks()
    {
        var user = BuildUser("playoff@example.com");
        var league = BuildLeague();
        var mapping = BuildMapping(user, league);

        // Week 1 of postseason = Wild Card = 3 required picks
        _espnApi.GetScores().Returns(BuildScoreboard(weekNumber: 1, seasonType: (int)TypeOfSeason.PostSeason));
        _repo.GetUsersAsync().Returns(new List<ApplicationUser> { user });
        _repo.GetLeagueUserMappingsAsync(user).Returns(new List<LeagueUserMapping> { mapping });

        // User has 3 picks for Wild Card — should NOT get an email
        _repo.GetUserNflPicksAsync(user.Id, league.Id, Season, 1)
             .Returns(new List<NflPicks>
             {
                 new() { UserId = user.Id },
                 new() { UserId = user.Id },
                 new() { UserId = user.Id }
             });

        await BuildJob().Execute(_context);

        await _emailSender.DidNotReceive().SendEmailAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }
}
