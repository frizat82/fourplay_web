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
    private readonly IEspnCacheService _espnCache = Substitute.For<IEspnCacheService>();
    private readonly ICfbCacheService _cfbCache = Substitute.For<ICfbCacheService>();
    private readonly ICfbRepository _cfbRepo = Substitute.For<ICfbRepository>();
    private static readonly DateTimeOffset Now = new(2026, 10, 4, 18, 0, 0, TimeSpan.Zero);

    public WeekResultNotificationServiceTests()
    {
        _repo.GetLeaguesByTypeAsync(Arg.Any<LeagueType>()).Returns(new List<LeagueInfo>());
        // Default: every week/slate ends in the future, i.e. is current — existing tests exercise
        // the send logic itself; the staleness tests below override these.
        SetNflWeekEnds(Enumerable.Range(1, 22).Select(w => (w, Now.UtcDateTime.AddDays(3))).ToArray());
        SetCfbSlateEnds(Enumerable.Range(1, 18).Select(s => (s, DateOnly.FromDateTime(Now.UtcDateTime.AddDays(3)))).ToArray());
    }

    private void SetNflWeekEnds(params (int Week, DateTime End)[] weeks) =>
        _repo.GetNflSeasonWeekConfigsAsync(2026).Returns(weeks.Select(w => new NflSeasonWeekConfig { Season = 2026, WeekId = w.Week, WeekEndDatetime = w.End }).ToList());

    private void SetCfbSlateEnds(params (int Slate, DateOnly End)[] slates) =>
        _cfbRepo.GetAllSlatesAsync().Returns(slates.Select(s => new CfbSlates { Season = 2026, SlateNumber = s.Slate, EndDate = s.End }).ToList());

    private WeekResultNotificationService BuildService() => new(_repo, _cfbRepo, _nflLeaderboard, _cfbLeaderboard, _dispatcher, _espnCache, _cfbCache, new FakeTimeProvider(Now));

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
            Arg.Is<PushPayload>(p => p.Body.StartsWith("CFB Week 9:") && p.Body.Contains("You Won the Week") && !p.Body.Contains("Slate") && p.Sport == LeagueType.Cfb), Arg.Any<CancellationToken>());
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
            Arg.Is<PushPayload>(p => p.Body.StartsWith("CFB Week 9:") && p.Body.Contains("You Lost OSU")), Arg.Any<CancellationToken>());
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
            Arg.Is<PushPayload>(p => p.Body.StartsWith("NFL Week 5:") && p.Sport == LeagueType.Nfl), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckCfbWeekResultsAsync_PostseasonSlate_UsesTheRoundName()
    {
        var league = League(2, LeagueType.Cfb);
        _repo.GetLeaguesByTypeAsync(LeagueType.Cfb).Returns(new List<LeagueInfo> { league });
        _repo.GetWeekResultNotificationsSentAsync(2, 2026).Returns(new HashSet<(string, int)>());
        _cfbLeaderboard.BuildLeaderboard(2, 2026).Returns(new List<LeaderboardModel> {
            Row(User("cfb-user"), new LeaderboardWeekResults { Week = 17, WeekResult = WeekResult.Won, Score = 100 })
        });

        await BuildService().CheckCfbWeekResultsAsync(2026);

        await _dispatcher.Received(1).DispatchAsync("cfb-user", Arg.Any<Func<NotificationPreferencesDto, bool>>(),
            Arg.Is<PushPayload>(p => p.Body.StartsWith("CFB CFP Semifinals:")), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckCfbWeekResultsAsync_LostSlate_NamesTheLosingTeamInFull_WhenTheScoreboardHasIt()
    {
        var league = League(2, LeagueType.Cfb);
        _repo.GetLeaguesByTypeAsync(LeagueType.Cfb).Returns(new List<LeagueInfo> { league });
        _repo.GetWeekResultNotificationsSentAsync(2, 2026).Returns(new HashSet<(string, int)>());
        _cfbLeaderboard.BuildLeaderboard(2, 2026).Returns(new List<LeaderboardModel> {
            Row(User("cfb-user"), new LeaderboardWeekResults { Week = 9, WeekResult = WeekResult.Lost, Score = 0, LosingTeams = ["BUFF"] })
        });
        _cfbCache.GetScoresAsync().Returns(new EspnScores { Events = [new Event { Competitions = [new Competition {
            Competitors = [
                new Competitor { Team = new EspnTeam { Abbreviation = "BUFF", DisplayName = "Buffalo Bulls" }, Records = [] },
                new Competitor { Team = new EspnTeam { Abbreviation = "WMU", DisplayName = "Western Michigan Broncos" }, Records = [] },
            ], Odds = [] }] }] });

        await BuildService().CheckCfbWeekResultsAsync(2026);

        await _dispatcher.Received(1).DispatchAsync("cfb-user", Arg.Any<Func<NotificationPreferencesDto, bool>>(),
            Arg.Is<PushPayload>(p => p.Body.Contains("You Lost Buffalo Bulls") && !p.Body.Contains("BUFF")), Arg.Any<CancellationToken>());
    }

    // ─── Never send stale ("back") notifications ──────────────────────────────────────────
    // A week's result may only be pushed until 48h after that week's scheduled end. Anything
    // older — e.g. weeks decided before this feature shipped, on its first run mid-season — is
    // recorded as sent silently so it can never be pushed later either.

    [Fact]
    public async Task CheckNflWeekResultsAsync_FirstRunMidSeason_PushesOnlyTheCurrentWeek_AndSilentlyRecordsOldOnes()
    {
        var league = League(1, LeagueType.Nfl);
        _repo.GetLeaguesByTypeAsync(LeagueType.Nfl).Returns(new List<LeagueInfo> { league });
        _repo.GetWeekResultNotificationsSentAsync(1, 2026).Returns(new HashSet<(string, int)>());
        SetNflWeekEnds((1, Now.UtcDateTime.AddDays(-21)), (2, Now.UtcDateTime.AddDays(-14)), (3, Now.UtcDateTime.AddDays(-7)), (4, Now.UtcDateTime.AddDays(2)));
        _nflLeaderboard.BuildLeaderboard(1, 2026L).Returns(new List<LeaderboardModel> {
            Row(User("user-1"),
                new LeaderboardWeekResults { Week = 1, WeekResult = WeekResult.Won, Score = 100 },
                new LeaderboardWeekResults { Week = 2, WeekResult = WeekResult.Lost, Score = 0, LosingTeams = ["BAL"] },
                new LeaderboardWeekResults { Week = 3, WeekResult = WeekResult.Won, Score = 100 },
                new LeaderboardWeekResults { Week = 4, WeekResult = WeekResult.Won, Score = 100 })
        });

        await BuildService().CheckNflWeekResultsAsync(2026);

        await _dispatcher.Received(1).DispatchAsync(Arg.Any<string>(), Arg.Any<Func<NotificationPreferencesDto, bool>>(), Arg.Any<PushPayload>(), Arg.Any<CancellationToken>());
        await _dispatcher.Received(1).DispatchAsync("user-1", Arg.Any<Func<NotificationPreferencesDto, bool>>(),
            Arg.Is<PushPayload>(p => p.Body.StartsWith("NFL Week 4:")), Arg.Any<CancellationToken>());
        await _repo.Received(1).RecordWeekResultNotificationsSentAsync(
            Arg.Is<IEnumerable<(string UserId, int Week)>>(e => e.Select(x => x.Week).OrderBy(w => w).SequenceEqual(new[] { 1, 2, 3, 4 })), 1, 2026);
    }

    [Fact]
    public async Task CheckNflWeekResultsAsync_WeekEndedWithinTheLast48Hours_StillPushes_ForALateMondayNightFinal()
    {
        var league = League(1, LeagueType.Nfl);
        _repo.GetLeaguesByTypeAsync(LeagueType.Nfl).Returns(new List<LeagueInfo> { league });
        _repo.GetWeekResultNotificationsSentAsync(1, 2026).Returns(new HashSet<(string, int)>());
        SetNflWeekEnds((4, Now.UtcDateTime.AddHours(-24)));
        _nflLeaderboard.BuildLeaderboard(1, 2026L).Returns(new List<LeaderboardModel> {
            Row(User("user-1"), new LeaderboardWeekResults { Week = 4, WeekResult = WeekResult.Won, Score = 100 })
        });

        await BuildService().CheckNflWeekResultsAsync(2026);

        await _dispatcher.Received(1).DispatchAsync("user-1", Arg.Any<Func<NotificationPreferencesDto, bool>>(), Arg.Any<PushPayload>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(-47.9, true)]
    [InlineData(-48.1, false)]
    public async Task CheckNflWeekResultsAsync_PushWindowBoundary_IsExactly48HoursAfterTheWeekEnds(double weekEndHoursFromNow, bool expectPush)
    {
        var league = League(1, LeagueType.Nfl);
        _repo.GetLeaguesByTypeAsync(LeagueType.Nfl).Returns(new List<LeagueInfo> { league });
        _repo.GetWeekResultNotificationsSentAsync(1, 2026).Returns(new HashSet<(string, int)>());
        SetNflWeekEnds((4, Now.UtcDateTime.AddHours(weekEndHoursFromNow)));
        _nflLeaderboard.BuildLeaderboard(1, 2026L).Returns(new List<LeaderboardModel> {
            Row(User("user-1"), new LeaderboardWeekResults { Week = 4, WeekResult = WeekResult.Won, Score = 100 })
        });

        var pushed = await BuildService().CheckNflWeekResultsAsync(2026);

        Assert.Equal(expectPush ? 1 : 0, pushed);
        await _repo.Received(1).RecordWeekResultNotificationsSentAsync(
            Arg.Is<IEnumerable<(string UserId, int Week)>>(e => e.Single().Week == 4), 1, 2026);
    }

    [Fact]
    public async Task CheckNflWeekResultsAsync_WeekWithNoScheduleRow_IsNeverPushed()
    {
        var league = League(1, LeagueType.Nfl);
        _repo.GetLeaguesByTypeAsync(LeagueType.Nfl).Returns(new List<LeagueInfo> { league });
        _repo.GetWeekResultNotificationsSentAsync(1, 2026).Returns(new HashSet<(string, int)>());
        SetNflWeekEnds();
        _nflLeaderboard.BuildLeaderboard(1, 2026L).Returns(new List<LeaderboardModel> {
            Row(User("user-1"), new LeaderboardWeekResults { Week = 4, WeekResult = WeekResult.Won, Score = 100 })
        });

        await BuildService().CheckNflWeekResultsAsync(2026);

        await _dispatcher.DidNotReceive().DispatchAsync(Arg.Any<string>(), Arg.Any<Func<NotificationPreferencesDto, bool>>(), Arg.Any<PushPayload>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckCfbWeekResultsAsync_FirstRunMidSeason_PushesOnlyTheCurrentSlate()
    {
        var league = League(2, LeagueType.Cfb);
        _repo.GetLeaguesByTypeAsync(LeagueType.Cfb).Returns(new List<LeagueInfo> { league });
        _repo.GetWeekResultNotificationsSentAsync(2, 2026).Returns(new HashSet<(string, int)>());
        var today = DateOnly.FromDateTime(Now.UtcDateTime);
        SetCfbSlateEnds((1, today.AddDays(-28)), (4, today.AddDays(-7)), (5, today.AddDays(1)));
        _cfbLeaderboard.BuildLeaderboard(2, 2026).Returns(new List<LeaderboardModel> {
            Row(User("cfb-user"),
                new LeaderboardWeekResults { Week = 1, WeekResult = WeekResult.Won, Score = 100 },
                new LeaderboardWeekResults { Week = 4, WeekResult = WeekResult.Lost, Score = 0, LosingTeams = ["OSU"] },
                new LeaderboardWeekResults { Week = 5, WeekResult = WeekResult.Won, Score = 100 })
        });

        await BuildService().CheckCfbWeekResultsAsync(2026);

        await _dispatcher.Received(1).DispatchAsync(Arg.Any<string>(), Arg.Any<Func<NotificationPreferencesDto, bool>>(), Arg.Any<PushPayload>(), Arg.Any<CancellationToken>());
        await _dispatcher.Received(1).DispatchAsync("cfb-user", Arg.Any<Func<NotificationPreferencesDto, bool>>(),
            Arg.Is<PushPayload>(p => p.Body.StartsWith("CFB Week 5:")), Arg.Any<CancellationToken>());
        await _repo.Received(1).RecordWeekResultNotificationsSentAsync(
            Arg.Is<IEnumerable<(string UserId, int Week)>>(e => e.Select(x => x.Week).OrderBy(w => w).SequenceEqual(new[] { 1, 4, 5 })), 2, 2026);
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
