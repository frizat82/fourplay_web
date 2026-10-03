using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Services;
using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Server.Services.Repositories.Interfaces;
using FourPlayWebApp.Shared.Models;
using FourPlayWebApp.Shared.Models.Data;
using FourPlayWebApp.Shared.Models.Data.Dtos;
using FourPlayWebApp.Shared.Models.Enum;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace FourPlayWebApp.Server.UnitTests;

/// <summary>
/// LivePickTransitionService.RecomputeAsync is the one shared implementation both
/// LivePickNotificationWatcher call sites (NFL via IEspnCacheService, CFB via ICfbCacheService)
/// route through — see the canary test at the bottom. Covers the bead's acceptance criteria:
/// quiet-window suppression, exactly one push per direction change, final-ping dedup independent
/// of during-game dedup, and restart-recovery from a persisted row (no in-memory history needed).
/// </summary>
public class LivePickTransitionServiceTests
{
    private readonly IEspnCacheService _espnCache = Substitute.For<IEspnCacheService>();
    private readonly ICfbCacheService _cfbCache = Substitute.For<ICfbCacheService>();
    private readonly INflCurrentWeekService _nflCurrentWeek = Substitute.For<INflCurrentWeekService>();
    private readonly ICfbCurrentSlateService _cfbCurrentSlate = Substitute.For<ICfbCurrentSlateService>();
    private readonly ILeagueRepository _leagueRepository = Substitute.For<ILeagueRepository>();
    private readonly ICfbPicksRepository _cfbPicksRepository = Substitute.For<ICfbPicksRepository>();
    private readonly ICfbRepository _cfbRepository = Substitute.For<ICfbRepository>();
    private readonly IPickLiveNotificationStateService _stateService = Substitute.For<IPickLiveNotificationStateService>();
    private readonly INotificationPreferencesService _preferencesService = Substitute.For<INotificationPreferencesService>();
    private readonly INotificationDispatcher _dispatcher = Substitute.For<INotificationDispatcher>();

    private static readonly DateTimeOffset FakeNow = new(2026, 9, 20, 13, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset KickoffLongAgo = FakeNow.AddHours(-1); // well past the quiet window
    private static readonly DateTimeOffset KickoffJustNow = FakeNow.AddMinutes(-5); // inside the quiet window

    private sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider {
        public override DateTimeOffset GetUtcNow() => now;
    }
    private readonly TimeProvider _timeProvider = new FakeTimeProvider(FakeNow);

    private LivePickTransitionService BuildService(LiveScoreSnapshotStore? store = null) => new(
        _espnCache, _cfbCache, _nflCurrentWeek, _cfbCurrentSlate,
        _leagueRepository, _cfbPicksRepository, _cfbRepository,
        _stateService, _preferencesService, _dispatcher,
        store ?? new LiveScoreSnapshotStore(), _timeProvider, NullLogger<LivePickTransitionService>.Instance);

    private static EspnScores NflScores(long kcScore, long balScore, TypeName status, DateTimeOffset kickoff) => new() {
        Events = [new Event { Id = "e1", Competitions = [new Competition {
            Id = "c1",
            Date = kickoff,
            Status = new EspnStatus { Type = new StatusType {
                Name = status,
                Description = status == TypeName.StatusFinal ? Description.Final : Description.InProgress,
            } },
            Competitors = [
                new Competitor { HomeAway = HomeAway.Home, Score = kcScore, Team = new EspnTeam { Abbreviation = "KC" }, Records = [] },
                new Competitor { HomeAway = HomeAway.Away, Score = balScore, Team = new EspnTeam { Abbreviation = "BAL" }, Records = [] },
            ],
            Odds = [],
        }] }],
    };

    private static EspnScores CfbScores(long osuScore, long michScore, TypeName status, DateTimeOffset kickoff) => new() {
        Events = [new Event { Id = "ce1", Competitions = [new Competition {
            Id = "cc1",
            Date = kickoff,
            Status = new EspnStatus { Type = new StatusType {
                Name = status,
                Description = status == TypeName.StatusFinal ? Description.Final : Description.InProgress,
            } },
            Competitors = [
                new Competitor { HomeAway = HomeAway.Home, Score = osuScore, Team = new EspnTeam { Abbreviation = "OSU" }, Records = [] },
                new Competitor { HomeAway = HomeAway.Away, Score = michScore, Team = new EspnTeam { Abbreviation = "MICH" }, Records = [] },
            ],
            Odds = [],
        }] }],
    };

    // KC -3 at home, no juice: KC must win by more than 3 to cover.
    private static readonly NflSpreads KcSpread = new() { HomeTeam = "KC", AwayTeam = "BAL", HomeTeamSpread = -3, AwayTeamSpread = 3, OverUnder = 45 };
    private static readonly LeagueJuiceMapping NoJuice = new() { Juice = 0, JuiceDivisional = 0, JuiceConference = 0 };
    private static readonly NflPicks KcPick = new() { Id = 101, UserId = "user-1", LeagueId = 1, Team = "KC", Pick = PickType.Spread, NflWeek = 5, Season = 2026 };
    private static readonly List<LeagueUserMapping> Members = [
        new() { UserId = "user-1" },
        new() { UserId = "user-2" },
    ];

    // Takes the already-built "only this flag true" DTO rather than a setter lambda — a setter
    // lambda's assignment expression can't appear inside the Arg.Is expression tree at the call site.
    private static bool PredicateMatches(Func<NotificationPreferencesDto, bool> predicate, NotificationPreferencesDto whenTrue) =>
        predicate(whenTrue) && !predicate(new NotificationPreferencesDto());

    private void SetUpNflHappyPath(Dictionary<int, PickLiveNotificationState>? priorStates = null)
    {
        _preferencesService.AnyLiveNotificationPreferenceEnabledAsync().Returns(true);
        _nflCurrentWeek.GetCurrentWeekAsync().Returns(new NflWeekInfo(5, 2026, false, "Week 5", "Standard", default));
        _leagueRepository.GetNflPicksForTeamsAsync(2026, 5, Arg.Any<IReadOnlyCollection<string>>()).Returns([KcPick]);
        _leagueRepository.GetNflSpreadsAsync(2026, 5).Returns([KcSpread]);
        _leagueRepository.GetLeagueJuiceMappingAsync(1, 2026).Returns(NoJuice);
        _leagueRepository.GetLeagueUserMappingsAsync(1).Returns(Members);
        _stateService.GetStatesAsync(LeagueType.Nfl, Arg.Any<IReadOnlyCollection<int>>()).Returns(priorStates ?? []);
    }

    [Fact]
    public async Task RecomputeAsync_ShortCircuits_WhenNoLiveNotificationPreferencesEnabled()
    {
        _preferencesService.AnyLiveNotificationPreferenceEnabledAsync().Returns(false);

        await BuildService().RecomputeAsync(LeagueType.Nfl);

        await _espnCache.DidNotReceive().GetScoresAsync();
        await _stateService.DidNotReceive().GetStatesAsync(Arg.Any<LeagueType>(), Arg.Any<IReadOnlyCollection<int>>());
        await _dispatcher.DidNotReceive().DispatchAsync(Arg.Any<string>(), Arg.Any<Func<NotificationPreferencesDto, bool>>(), Arg.Any<PushPayload>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecomputeAsync_NoOp_WhenNoGameActuallyChanged()
    {
        _preferencesService.AnyLiveNotificationPreferenceEnabledAsync().Returns(true);
        var scores = NflScores(0, 0, TypeName.StatusScheduled, KickoffLongAgo.AddHours(1));
        _espnCache.GetScoresAsync().Returns(scores);
        var store = new LiveScoreSnapshotStore();
        store.Exchange(LeagueType.Nfl, scores); // pre-seed: identical fingerprint next call

        await BuildService(store).RecomputeAsync(LeagueType.Nfl);

        await _stateService.DidNotReceive().GetStatesAsync(Arg.Any<LeagueType>(), Arg.Any<IReadOnlyCollection<int>>());
        await _dispatcher.DidNotReceive().DispatchAsync(Arg.Any<string>(), Arg.Any<Func<NotificationPreferencesDto, bool>>(), Arg.Any<PushPayload>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecomputeAsync_QuietWindow_SuppressesBaselineAndPush_BeforeWindowElapses()
    {
        SetUpNflHappyPath();
        _espnCache.GetScoresAsync().Returns(NflScores(27, 20, TypeName.StatusInProgress, KickoffJustNow));

        await BuildService().RecomputeAsync(LeagueType.Nfl);

        await _dispatcher.DidNotReceive().DispatchAsync(Arg.Any<string>(), Arg.Any<Func<NotificationPreferencesDto, bool>>(), Arg.Any<PushPayload>(), Arg.Any<CancellationToken>());
        await _stateService.DidNotReceive().UpsertStatesAsync(Arg.Any<IReadOnlyCollection<PickLiveNotificationState>>());
    }

    [Fact]
    public async Task RecomputeAsync_FirstReadAfterQuietWindow_EstablishesSilentBaseline_NoPush()
    {
        SetUpNflHappyPath(); // no prior state row
        _espnCache.GetScoresAsync().Returns(NflScores(27, 20, TypeName.StatusInProgress, KickoffLongAgo)); // KC covers (margin 7 > 3)

        await BuildService().RecomputeAsync(LeagueType.Nfl);

        await _dispatcher.DidNotReceive().DispatchAsync(Arg.Any<string>(), Arg.Any<Func<NotificationPreferencesDto, bool>>(), Arg.Any<PushPayload>(), Arg.Any<CancellationToken>());
        await _stateService.Received(1).UpsertStatesAsync(Arg.Is<IReadOnlyCollection<PickLiveNotificationState>>(
            list => list.Any(s => s.PickId == 101 && s.LastNotifiedCovering == true && s.LeagueId == 1)));
    }

    [Fact]
    public async Task RecomputeAsync_TransitionAfterBaseline_FiresExactlyOnePush_ToOwnerAndOtherMembers()
    {
        SetUpNflHappyPath(new Dictionary<int, PickLiveNotificationState> {
            [101] = new() { Sport = LeagueType.Nfl, PickId = 101, LeagueId = 1, LastNotifiedCovering = true, LastNotifiedAt = FakeNow.AddMinutes(-10) },
        });
        // KC 20-20: margin 0, does not cover (-3 spread needs >3 margin) — a flip from the prior "covering" baseline.
        _espnCache.GetScoresAsync().Returns(NflScores(20, 20, TypeName.StatusInProgress, KickoffLongAgo));

        await BuildService().RecomputeAsync(LeagueType.Nfl);

        await _dispatcher.Received(1).DispatchAsync("user-1",
            Arg.Is<Func<NotificationPreferencesDto, bool>>(f => PredicateMatches(f, new NotificationPreferencesDto { NotifyMineBloodyDuringGame = true })),
            Arg.Any<PushPayload>(), Arg.Any<CancellationToken>());
        await _dispatcher.Received(1).DispatchAsync("user-2",
            Arg.Is<Func<NotificationPreferencesDto, bool>>(f => PredicateMatches(f, new NotificationPreferencesDto { NotifyOthersBloodyDuringGame = true })),
            Arg.Any<PushPayload>(), Arg.Any<CancellationToken>());
        await _dispatcher.Received(2).DispatchAsync(Arg.Any<string>(), Arg.Any<Func<NotificationPreferencesDto, bool>>(), Arg.Any<PushPayload>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecomputeAsync_NoTransition_NoPush_NoStateWrite()
    {
        SetUpNflHappyPath(new Dictionary<int, PickLiveNotificationState> {
            [101] = new() { Sport = LeagueType.Nfl, PickId = 101, LeagueId = 1, LastNotifiedCovering = true, LastNotifiedAt = FakeNow.AddMinutes(-10) },
        });
        // Still covering (margin 7 > 3) — same as the prior baseline, no transition.
        _espnCache.GetScoresAsync().Returns(NflScores(27, 20, TypeName.StatusInProgress, KickoffLongAgo));

        await BuildService().RecomputeAsync(LeagueType.Nfl);

        await _dispatcher.DidNotReceive().DispatchAsync(Arg.Any<string>(), Arg.Any<Func<NotificationPreferencesDto, bool>>(), Arg.Any<PushPayload>(), Arg.Any<CancellationToken>());
        await _stateService.DidNotReceive().UpsertStatesAsync(Arg.Any<IReadOnlyCollection<PickLiveNotificationState>>());
    }

    [Fact]
    public async Task RecomputeAsync_FinalPing_FiresIndependentlyOfDuringGameDedup()
    {
        SetUpNflHappyPath(new Dictionary<int, PickLiveNotificationState> {
            // Same covering state as before (no during-game transition) but FinalNotifiedAt not yet set.
            [101] = new() { Sport = LeagueType.Nfl, PickId = 101, LeagueId = 1, LastNotifiedCovering = true, LastNotifiedAt = FakeNow.AddMinutes(-10), FinalNotifiedAt = null },
        });
        _espnCache.GetScoresAsync().Returns(NflScores(27, 20, TypeName.StatusFinal, KickoffLongAgo));

        await BuildService().RecomputeAsync(LeagueType.Nfl);

        await _dispatcher.Received(1).DispatchAsync("user-1",
            Arg.Is<Func<NotificationPreferencesDto, bool>>(f => PredicateMatches(f, new NotificationPreferencesDto { NotifyMineCoveringAtFinal = true })),
            Arg.Any<PushPayload>(), Arg.Any<CancellationToken>());
        // Never the during-game predicate — no transition happened, only the independent final ping.
        await _dispatcher.DidNotReceive().DispatchAsync(Arg.Any<string>(),
            Arg.Is<Func<NotificationPreferencesDto, bool>>(f => PredicateMatches(f, new NotificationPreferencesDto { NotifyMineCoveringDuringGame = true })),
            Arg.Any<PushPayload>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecomputeAsync_FinalPing_NeverFiresTwice()
    {
        SetUpNflHappyPath(new Dictionary<int, PickLiveNotificationState> {
            [101] = new() { Sport = LeagueType.Nfl, PickId = 101, LeagueId = 1, LastNotifiedCovering = true, LastNotifiedAt = FakeNow.AddMinutes(-10), FinalNotifiedAt = FakeNow.AddMinutes(-2) },
        });
        _espnCache.GetScoresAsync().Returns(NflScores(27, 20, TypeName.StatusFinal, KickoffLongAgo));

        await BuildService().RecomputeAsync(LeagueType.Nfl);

        await _dispatcher.DidNotReceive().DispatchAsync(Arg.Any<string>(), Arg.Any<Func<NotificationPreferencesDto, bool>>(), Arg.Any<PushPayload>(), Arg.Any<CancellationToken>());
        await _stateService.DidNotReceive().UpsertStatesAsync(Arg.Any<IReadOnlyCollection<PickLiveNotificationState>>());
    }

    // A mid-Sunday Railway restart loses LiveScoreSnapshotStore's in-memory snapshot entirely (a
    // fresh instance, previous=null) — dedup correctness must come purely from the persisted row,
    // not from in-memory tick history.
    [Fact]
    public async Task RecomputeAsync_RestartRecovery_UsesPersistedState_NotInMemoryHistory()
    {
        SetUpNflHappyPath(new Dictionary<int, PickLiveNotificationState> {
            [101] = new() { Sport = LeagueType.Nfl, PickId = 101, LeagueId = 1, LastNotifiedCovering = true, LastNotifiedAt = FakeNow.AddHours(-1) },
        });
        _espnCache.GetScoresAsync().Returns(NflScores(20, 20, TypeName.StatusInProgress, KickoffLongAgo)); // flips to not-covering

        await BuildService(new LiveScoreSnapshotStore()).RecomputeAsync(LeagueType.Nfl); // fresh store = simulated restart

        await _dispatcher.Received(1).DispatchAsync("user-1",
            Arg.Is<Func<NotificationPreferencesDto, bool>>(f => PredicateMatches(f, new NotificationPreferencesDto { NotifyMineBloodyDuringGame = true })),
            Arg.Any<PushPayload>(), Arg.Any<CancellationToken>());
    }

    // Canary: both sports must route through this exact same method body, parameterized only by
    // which repositories/cache get called — never a second, CFB-shaped copy of the logic above.
    //
    // Id (119) and SlateNumber (9) are deliberately DIFFERENT here (frizat: a real bug shipped
    // where ResolveCfbPeriodAsync's single int got reused both for FK-scoped repo calls, where
    // the DB row id is correct, AND for JuiceTiers.For, which needs the 1-18 slate number — a
    // coincidental Id==SlateNumber in an earlier version of this test masked it completely).
    // With a non-zero Juice (13) and this spread, the wrong tier (period > 17 falls into
    // GetCfbRequiredPicks' "_ => 1" branch => 0 tease) flips the computed cover state, so this
    // test fails loudly if that bug reappears instead of silently passing.
    [Fact]
    public async Task RecomputeAsync_Cfb_RoutesThroughTheSameMethod_UsingSlateNumberNotId_ForJuiceTiers()
    {
        _preferencesService.AnyLiveNotificationPreferenceEnabledAsync().Returns(true);
        _cfbCache.GetScoresAsync().Returns(CfbScores(27, 20, TypeName.StatusInProgress, KickoffLongAgo));
        _cfbCurrentSlate.GetCurrentSlateAsync().Returns(new CfbSlateInfo(119, 2026, 9, "Slate 9", "Regular", default, default, null, default));
        var cfbPick = new CfbPicks { Id = 201, UserId = "user-1", LeagueId = 2, CfbSlateId = 119, Team = "OSU", PickType = PickType.Spread, Season = 2026 };
        _cfbPicksRepository.GetCfbPicksForTeamsAsync(2026, 119, Arg.Any<IReadOnlyCollection<string>>()).Returns([cfbPick]);
        _cfbRepository.GetLeagueEligibleSpreadsForSeasonAsync(2026).Returns([
            new CfbSpreads { CfbSlateId = 119, HomeTeam = "OSU", AwayTeam = "MICH", HomeTeamSpread = -10, AwayTeamSpread = 10, OverUnder = 45, IsLeagueEligible = true },
        ]);
        var juiced = new LeagueJuiceMapping { Juice = 13, JuiceDivisional = 10, JuiceConference = 6 };
        _leagueRepository.GetLeagueJuiceMappingAsync(2, 2026).Returns(juiced);
        _leagueRepository.GetLeagueUserMappingsAsync(2).Returns(Members);
        _stateService.GetStatesAsync(LeagueType.Cfb, Arg.Any<IReadOnlyCollection<int>>()).Returns([]);

        await BuildService().RecomputeAsync(LeagueType.Cfb);

        await _cfbCache.Received(1).GetScoresAsync();
        await _cfbPicksRepository.Received(1).GetCfbPicksForTeamsAsync(2026, 119, Arg.Any<IReadOnlyCollection<string>>());
        await _espnCache.DidNotReceive().GetScoresAsync();
        await _leagueRepository.DidNotReceive().GetNflPicksForTeamsAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<IReadOnlyCollection<string>>());
        // Baseline established (no prior state) with the CORRECT regular-season (SlateNumber=9)
        // tease applied: OSU 27-20 (margin 7) with a -10 spread + 13 juice = +3 net spread => covers.
        // The bug would've used Id=119 (> 17 => "_ => 1" => 0 tease), computing margin 7-10=-3 =>
        // not covering — a different, observably wrong LastNotifiedCovering value.
        await _stateService.Received(1).UpsertStatesAsync(Arg.Is<IReadOnlyCollection<PickLiveNotificationState>>(
            list => list.Any(s => s.Sport == LeagueType.Cfb && s.PickId == 201 && s.LastNotifiedCovering == true)));
    }
}
