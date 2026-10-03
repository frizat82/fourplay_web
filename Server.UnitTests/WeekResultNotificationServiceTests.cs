using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Models.Identity;
using FourPlayWebApp.Server.Services;
using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Server.Services.Repositories.Interfaces;
using FourPlayWebApp.Shared.Models;
using FourPlayWebApp.Shared.Models.Data.Dtos;
using FourPlayWebApp.Shared.Models.Enum;
using NSubstitute;
using Xunit;

namespace FourPlayWebApp.Server.UnitTests;

/// <summary>
/// WeekResultNotificationService is called directly by NflScoresJob/CfbScoresJob right after they
/// persist new final scores (see NflScoresJobTests/CfbScoresJobTests for that wiring) — this file
/// tests the shared per-league notify/dedup logic in isolation, for both sports. Exactly one
/// notification per (user, league, week), fired the moment the week's fate is sealed either way:
/// "You Won the Week!" once every pick is decided and correct, or "You Lost {Team} - You Lost the
/// Week!" the instant any one pick loses (never waiting for the rest of the week to finish).
/// </summary>
public class WeekResultNotificationServiceTests
{
    private readonly ILeagueRepository _repo = Substitute.For<ILeagueRepository>();
    private readonly ILeaderboardService _nflLeaderboard = Substitute.For<ILeaderboardService>();
    private readonly ICfbLeaderboardService _cfbLeaderboard = Substitute.For<ICfbLeaderboardService>();
    private readonly INotificationDispatcher _dispatcher = Substitute.For<INotificationDispatcher>();

    public WeekResultNotificationServiceTests()
    {
        _repo.GetLeaguesByTypeAsync(Arg.Any<LeagueType>()).Returns(new List<LeagueInfo>());
    }

    private WeekResultNotificationService BuildService() => new(_repo, _nflLeaderboard, _cfbLeaderboard, _dispatcher);

    private static ApplicationUser User(string id) => new() { Id = id, UserName = id };

    private static LeaderboardModel Row(ApplicationUser user, params LeaderboardWeekResults[] weeks) =>
        new() { User = user, Total = 0, Rank = "1", WeekResults = weeks };

    private static LeagueInfo League(int id, LeagueType type) =>
        new() { Id = id, LeagueName = $"League {id}", OwnerUserId = "owner", LeagueType = type };

    [Fact]
    public async Task CheckNflWeekResultsAsync_SendsNotification_ForAWonWeek_NotYetSent()
    {
        var league = League(1, LeagueType.Nfl);
        _repo.GetLeaguesByTypeAsync(LeagueType.Nfl).Returns(new List<LeagueInfo> { league });
        _repo.GetWeekResultNotificationsSentAsync(1, 2026).Returns(new HashSet<(string, int)>());
        _nflLeaderboard.BuildLeaderboard(1, 2026L).Returns(new List<LeaderboardModel> {
            Row(User("user-1"), new LeaderboardWeekResults { Week = 5, WeekResult = WeekResult.Won, Score = 100 })
        });

        var sent = await BuildService().CheckNflWeekResultsAsync(2026);

        Assert.Equal(1, sent);
        await _dispatcher.Received(1).DispatchAsync("user-1", Arg.Any<Func<NotificationPreferencesDto, bool>>(),
            Arg.Is<PushPayload>(p => p.Body.Contains("You Won the Week") && p.Body.Contains("League 1")), Arg.Any<CancellationToken>());
        await _repo.Received(1).RecordWeekResultNotificationsSentAsync(
            Arg.Is<IEnumerable<(string UserId, int Week)>>(e => e.Any(x => x.UserId == "user-1" && x.Week == 5)), 1, 2026);
    }

    [Fact]
    public async Task CheckNflWeekResultsAsync_SendsNotification_ForALostWeek_NamingTheLosingPick()
    {
        var league = League(1, LeagueType.Nfl);
        _repo.GetLeaguesByTypeAsync(LeagueType.Nfl).Returns(new List<LeagueInfo> { league });
        _repo.GetWeekResultNotificationsSentAsync(1, 2026).Returns(new HashSet<(string, int)>());
        _nflLeaderboard.BuildLeaderboard(1, 2026L).Returns(new List<LeaderboardModel> {
            Row(User("user-1"), new LeaderboardWeekResults { Week = 5, WeekResult = WeekResult.Lost, Score = 0, LosingTeams = ["BAL"] })
        });

        var sent = await BuildService().CheckNflWeekResultsAsync(2026);

        Assert.Equal(1, sent);
        await _dispatcher.Received(1).DispatchAsync("user-1", Arg.Any<Func<NotificationPreferencesDto, bool>>(),
            Arg.Is<PushPayload>(p => p.Body.Contains("You Lost BAL") && p.Body.Contains("You Lost the Week") && p.Body.Contains("League 1")),
            Arg.Any<CancellationToken>());
        await _repo.Received(1).RecordWeekResultNotificationsSentAsync(
            Arg.Is<IEnumerable<(string UserId, int Week)>>(e => e.Any(x => x.UserId == "user-1" && x.Week == 5)), 1, 2026);
    }

    [Fact]
    public async Task CheckNflWeekResultsAsync_LostWeek_FallsBackToGenericWording_WhenNoLosingTeamWasRecorded()
    {
        // Defensive case: WeekResult.Lost with an empty LosingTeams list shouldn't normally happen,
        // but must still produce a sensible message rather than crash or show a blank team name.
        var league = League(1, LeagueType.Nfl);
        _repo.GetLeaguesByTypeAsync(LeagueType.Nfl).Returns(new List<LeagueInfo> { league });
        _repo.GetWeekResultNotificationsSentAsync(1, 2026).Returns(new HashSet<(string, int)>());
        _nflLeaderboard.BuildLeaderboard(1, 2026L).Returns(new List<LeaderboardModel> {
            Row(User("user-1"), new LeaderboardWeekResults { Week = 5, WeekResult = WeekResult.Lost, Score = 0 })
        });

        await BuildService().CheckNflWeekResultsAsync(2026);

        await _dispatcher.Received(1).DispatchAsync("user-1", Arg.Any<Func<NotificationPreferencesDto, bool>>(),
            Arg.Is<PushPayload>(p => p.Body.Contains("You Lost the Week") && !p.Body.Contains("You Lost  ")),
            Arg.Any<CancellationToken>());
    }

    // /code-review: a Lost week caused by a pick that failed to score (bad data) must not be
    // permanently notified+deduped — unlike a leaderboard page render, which self-corrects once the
    // data bug is fixed, a push and its dedup row are irreversible. Skipping here (not recording
    // dedup) lets the next score-ingestion run re-evaluate and still send the correct push once the
    // underlying data issue clears.
    [Fact]
    public async Task CheckNflWeekResultsAsync_DoesNotNotifyOrDedupe_WhenALostWeekHadAScoringError()
    {
        var league = League(1, LeagueType.Nfl);
        _repo.GetLeaguesByTypeAsync(LeagueType.Nfl).Returns(new List<LeagueInfo> { league });
        _repo.GetWeekResultNotificationsSentAsync(1, 2026).Returns(new HashSet<(string, int)>());
        _nflLeaderboard.BuildLeaderboard(1, 2026L).Returns(new List<LeaderboardModel> {
            Row(User("user-1"), new LeaderboardWeekResults { Week = 5, WeekResult = WeekResult.Lost, Score = 0, LosingTeams = ["KC"], HadScoringError = true })
        });

        var sent = await BuildService().CheckNflWeekResultsAsync(2026);

        Assert.Equal(0, sent);
        await _dispatcher.DidNotReceive().DispatchAsync(Arg.Any<string>(), Arg.Any<Func<NotificationPreferencesDto, bool>>(), Arg.Any<PushPayload>(), Arg.Any<CancellationToken>());
        await _repo.DidNotReceive().RecordWeekResultNotificationsSentAsync(Arg.Any<IEnumerable<(string UserId, int Week)>>(), Arg.Any<int>(), Arg.Any<int>());
    }

    [Theory]
    [InlineData(WeekResult.MissingPicks)]
    [InlineData(WeekResult.MissingGameResults)]
    [InlineData(WeekResult.Excluded)]
    public async Task CheckNflWeekResultsAsync_NeverSends_WhileTheWeekIsStillUndecided(WeekResult result)
    {
        var league = League(1, LeagueType.Nfl);
        _repo.GetLeaguesByTypeAsync(LeagueType.Nfl).Returns(new List<LeagueInfo> { league });
        _repo.GetWeekResultNotificationsSentAsync(1, 2026).Returns(new HashSet<(string, int)>());
        _nflLeaderboard.BuildLeaderboard(1, 2026L).Returns(new List<LeaderboardModel> {
            Row(User("user-1"), new LeaderboardWeekResults { Week = 5, WeekResult = result, Score = 0 })
        });

        var sent = await BuildService().CheckNflWeekResultsAsync(2026);

        Assert.Equal(0, sent);
        await _dispatcher.DidNotReceive().DispatchAsync(Arg.Any<string>(), Arg.Any<Func<NotificationPreferencesDto, bool>>(), Arg.Any<PushPayload>(), Arg.Any<CancellationToken>());
        await _repo.DidNotReceive().RecordWeekResultNotificationsSentAsync(Arg.Any<IEnumerable<(string UserId, int Week)>>(), Arg.Any<int>(), Arg.Any<int>());
    }

    [Theory]
    [InlineData(WeekResult.Won)]
    [InlineData(WeekResult.Lost)]
    public async Task CheckNflWeekResultsAsync_DoesNotResend_WhenAlreadyRecordedForThatUserAndWeek(WeekResult result)
    {
        var league = League(1, LeagueType.Nfl);
        _repo.GetLeaguesByTypeAsync(LeagueType.Nfl).Returns(new List<LeagueInfo> { league });
        _repo.GetWeekResultNotificationsSentAsync(1, 2026).Returns(new HashSet<(string, int)> { ("user-1", 5) });
        _nflLeaderboard.BuildLeaderboard(1, 2026L).Returns(new List<LeaderboardModel> {
            Row(User("user-1"), new LeaderboardWeekResults { Week = 5, WeekResult = result, Score = 0, LosingTeams = ["BAL"] })
        });

        var sent = await BuildService().CheckNflWeekResultsAsync(2026);

        Assert.Equal(0, sent);
        await _dispatcher.DidNotReceive().DispatchAsync(Arg.Any<string>(), Arg.Any<Func<NotificationPreferencesDto, bool>>(), Arg.Any<PushPayload>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckNflWeekResultsAsync_AsksTheRepositoryForNflLeaguesOnly_NotCfb()
    {
        // Filtering now happens in SQL (GetLeaguesByTypeAsync), not an in-memory .Where over
        // every league of both sports — confirm the service asks for the right sport rather than
        // fetching everything and discarding the other sport's rows.
        await BuildService().CheckNflWeekResultsAsync(2026);

        await _repo.Received(1).GetLeaguesByTypeAsync(LeagueType.Nfl);
        await _repo.DidNotReceive().GetLeaguesByTypeAsync(LeagueType.Cfb);
    }

    [Fact]
    public async Task CheckCfbWeekResultsAsync_SendsNotification_ForAWonSlate_NotYetSent()
    {
        var league = League(2, LeagueType.Cfb);
        _repo.GetLeaguesByTypeAsync(LeagueType.Cfb).Returns(new List<LeagueInfo> { league });
        _repo.GetWeekResultNotificationsSentAsync(2, 2026).Returns(new HashSet<(string, int)>());
        _cfbLeaderboard.BuildLeaderboard(2, 2026).Returns(new List<LeaderboardModel> {
            Row(User("cfb-user"), new LeaderboardWeekResults { Week = 9, WeekResult = WeekResult.Won, Score = 100 })
        });

        var sent = await BuildService().CheckCfbWeekResultsAsync(2026);

        Assert.Equal(1, sent);
        await _dispatcher.Received(1).DispatchAsync("cfb-user", Arg.Any<Func<NotificationPreferencesDto, bool>>(),
            Arg.Is<PushPayload>(p => p.Body.Contains("Slate 9") && p.Body.Contains("You Won the Week")), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckCfbWeekResultsAsync_SendsNotification_ForALostSlate_NamingTheLosingPick()
    {
        var league = League(2, LeagueType.Cfb);
        _repo.GetLeaguesByTypeAsync(LeagueType.Cfb).Returns(new List<LeagueInfo> { league });
        _repo.GetWeekResultNotificationsSentAsync(2, 2026).Returns(new HashSet<(string, int)>());
        _cfbLeaderboard.BuildLeaderboard(2, 2026).Returns(new List<LeaderboardModel> {
            Row(User("cfb-user"), new LeaderboardWeekResults { Week = 9, WeekResult = WeekResult.Lost, Score = 0, LosingTeams = ["OSU"] })
        });

        await BuildService().CheckCfbWeekResultsAsync(2026);

        await _dispatcher.Received(1).DispatchAsync("cfb-user", Arg.Any<Func<NotificationPreferencesDto, bool>>(),
            Arg.Is<PushPayload>(p => p.Body.Contains("Slate 9") && p.Body.Contains("You Lost OSU")), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckNflWeekResultsAsync_UsesWeekLabel_NotSlateLabel_InThePayload()
    {
        var league = League(1, LeagueType.Nfl);
        _repo.GetLeaguesByTypeAsync(LeagueType.Nfl).Returns(new List<LeagueInfo> { league });
        _repo.GetWeekResultNotificationsSentAsync(1, 2026).Returns(new HashSet<(string, int)>());
        _nflLeaderboard.BuildLeaderboard(1, 2026L).Returns(new List<LeaderboardModel> {
            Row(User("user-1"), new LeaderboardWeekResults { Week = 5, WeekResult = WeekResult.Won, Score = 100 })
        });

        await BuildService().CheckNflWeekResultsAsync(2026);

        await _dispatcher.Received(1).DispatchAsync("user-1", Arg.Any<Func<NotificationPreferencesDto, bool>>(),
            Arg.Is<PushPayload>(p => p.Body.Contains("Week 5")), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckNflWeekResultsAsync_NoLeagues_ReturnsZero_AndNeverQueriesDedup()
    {
        var sent = await BuildService().CheckNflWeekResultsAsync(2026);

        Assert.Equal(0, sent);
        await _repo.DidNotReceive().GetWeekResultNotificationsSentAsync(Arg.Any<int>(), Arg.Any<int>());
    }

    // /code-review regression: dedup rows used to be recorded in one batch after the whole
    // league's double loop — a dispatch failure for ANY one user lost the dedup row for every
    // OTHER user already successfully notified earlier in that same loop, guaranteeing they'd be
    // re-notified (a real duplicate push) on the next run. User 1 must still be recorded even
    // though user 2's dispatch throws.
    [Fact]
    public async Task CheckNflWeekResultsAsync_WhenDispatchFailsForOneUser_StillRecordsTheOthersThatSucceeded()
    {
        var league = League(1, LeagueType.Nfl);
        _repo.GetLeaguesByTypeAsync(LeagueType.Nfl).Returns(new List<LeagueInfo> { league });
        _repo.GetWeekResultNotificationsSentAsync(1, 2026).Returns(new HashSet<(string, int)>());
        _nflLeaderboard.BuildLeaderboard(1, 2026L).Returns(new List<LeaderboardModel> {
            Row(User("user-1"), new LeaderboardWeekResults { Week = 5, WeekResult = WeekResult.Won, Score = 100 }),
            Row(User("user-2"), new LeaderboardWeekResults { Week = 5, WeekResult = WeekResult.Won, Score = 100 }),
        });
        _dispatcher.DispatchAsync("user-2", Arg.Any<Func<NotificationPreferencesDto, bool>>(), Arg.Any<PushPayload>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new InvalidOperationException("transient push failure"));

        var sent = await BuildService().CheckNflWeekResultsAsync(2026);

        Assert.Equal(1, sent);
        await _repo.Received(1).RecordWeekResultNotificationsSentAsync(
            Arg.Is<IEnumerable<(string UserId, int Week)>>(e =>
                e.Any(x => x.UserId == "user-1" && x.Week == 5) &&
                !e.Any(x => x.UserId == "user-2")),
            1, 2026);
    }
}
