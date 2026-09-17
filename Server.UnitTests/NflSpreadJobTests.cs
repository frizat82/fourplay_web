using FourPlayWebApp.Server.Jobs;
using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Server.Services.Repositories.Interfaces;
using FourPlayWebApp.Shared.Models;
using FourPlayWebApp.Shared.Models.Data;
using FourPlayWebApp.Shared.Models.Enum;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Quartz;

namespace FourPlayWebApp.Server.UnitTests;

/// <summary>
/// Tests for NflSpreadJob — fetches odds from the ESPN Core Odds API and
/// persists spread data for games that are still scheduled.
/// </summary>
public class NflSpreadJobTests
{
    private readonly IEspnCoreOddsService _oddsService;
    private readonly INflLiveScoreFetcher _fetcher;
    private readonly ILeagueRepository _repo;
    private readonly INflCurrentWeekService _nflCurrentWeekService;
    private readonly IJobExecutionContext _context;
    private readonly TimeProvider _timeProvider;
    private readonly IJobObserverService _observer;

    // Fixed, controlled "now" — not tied to the real wall clock, so lock-time boundary tests are
    // deterministic regardless of when the suite actually runs.
    private static readonly DateTimeOffset FakeNow = new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);

    private sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider {
        public override DateTimeOffset GetUtcNow() => now;
    }

    // Default regular-season week used by most tests — lock time in the past (relative to
    // FakeNow) so the lock-time write-guard (frizat-pxy follow-on) doesn't gate the existing
    // happy-path tests.
    private static readonly DateTime PastLockTime = FakeNow.UtcDateTime.AddDays(-1);
    private static readonly DateTime FutureLockTime = FakeNow.UtcDateTime.AddDays(1);
    private static readonly NflWeekInfo DefaultWeek = new(5, 2024, false, "Week 5", "Standard", PastLockTime);

    public NflSpreadJobTests()
    {
        _oddsService = Substitute.For<IEspnCoreOddsService>();
        _fetcher = Substitute.For<INflLiveScoreFetcher>();
        _repo = Substitute.For<ILeagueRepository>();
        _nflCurrentWeekService = Substitute.For<INflCurrentWeekService>();
        _context = Substitute.For<IJobExecutionContext>();
        _timeProvider = new FakeTimeProvider(FakeNow);
        _observer = Substitute.For<IJobObserverService>();

        _nflCurrentWeekService.GetCurrentWeekAsync().Returns(DefaultWeek);
        _context.MergedJobDataMap.Returns(new JobDataMap());
        var jobDetail = Substitute.For<IJobDetail>();
        jobDetail.Key.Returns(new JobKey("NflSpreadJob-test"));
        _context.JobDetail.Returns(jobDetail);

        // NflSpreadJob now resolves the current week's full control-table row (date window) to
        // fetch by, mirroring CfbSpreadJob — matches DefaultWeek (Season=2024, WeekId=5); tests
        // using a different current week override this with their own BuildConfig call.
        _repo.GetNflSeasonWeekConfigsAsync(2024).Returns(new List<NflSeasonWeekConfig> { BuildConfig(weekId: 5, season: 2024) });
    }

    private static NflSeasonWeekConfig BuildConfig(int weekId, int season) => new() {
        Id = weekId,
        Season = season,
        WeekId = weekId,
        WeekLabel = $"Week {weekId}",
        WeekType = weekId > 18 ? "PostSeason" : "RegularSeason",
        ScoringFormat = "Standard",
        WeekStartDatetime = new DateTime(season, 11, 1, 0, 0, 0, DateTimeKind.Utc),
        WeekEndDatetime = new DateTime(season, 11, 10, 23, 59, 59, DateTimeKind.Utc),
    };

    private NflSpreadJob BuildJob() => new(_oddsService, _fetcher, _repo, _nflCurrentWeekService, _timeProvider, _observer);

    // -----------------------------------------------------------------------
    // Helper builders
    // -----------------------------------------------------------------------

    private static EspnScores BuildScoreboard(
        int weekNumber = 5,
        int seasonType = (int)TypeOfSeason.RegularSeason,
        int year = 2024,
        TypeName statusName = TypeName.StatusScheduled,
        string homeAbbr = "KC",
        string awayAbbr = "BUF",
        string eventId = "401547605",
        bool emptyEvents = false)
    {
        var competition = new Competition
        {
            Date = new DateTimeOffset(year, 11, 10, 18, 0, 0, TimeSpan.Zero),
            Competitors = new[]
            {
                new Competitor
                {
                    Id = "1",
                    HomeAway = HomeAway.Home,
                    Score = 0,
                    Team = new EspnTeam { Abbreviation = homeAbbr },
                    Records = Array.Empty<EspnRecord>()
                },
                new Competitor
                {
                    Id = "2",
                    HomeAway = HomeAway.Away,
                    Score = 0,
                    Team = new EspnTeam { Abbreviation = awayAbbr },
                    Records = Array.Empty<EspnRecord>()
                }
            },
            Status = new EspnStatus
            {
                Type = new StatusType { Name = statusName, Completed = false, Description = statusName switch {
                    TypeName.StatusFinal => Description.Final,
                    TypeName.StatusHalftime => Description.Halftime,
                    TypeName.StatusInProgress => Description.InProgress,
                    TypeName.StatusScheduled => Description.Scheduled,
                    _ => Description.EndOfPeriod,
                } }
            },
            Odds = Array.Empty<Odd>()
        };

        return new EspnScores
        {
            Season = new Season { Year = year, Type = seasonType },
            Week = new Week { Number = weekNumber },
            Events = emptyEvents ? [] : new[]
            {
                new Event
                {
                    Id = eventId,
                    Season = new Season { Year = year, Type = seasonType },
                    Week = new Week { Number = weekNumber },
                    Date = new DateTimeOffset(year, 11, 10, 18, 0, 0, TimeSpan.Zero),
                    Competitions = new[] { competition }
                }
            }
        };
    }

    private static EspnCoreOddsItem BuildOddsItem(
        string homeSpread = "-7",
        string awaySpread = "+7",
        double overUnder = 48.5)
    {
        return new EspnCoreOddsItem
        {
            Provider = new EspnCoreOddsProvider { Name = "DraftKings", Id = "100" },
            OverUnder = overUnder,
            HomeTeamOdds = new EspnCoreTeamOdds
            {
                Current = new EspnCoreTeamOddsDetail
                {
                    PointSpread = new EspnCorePointSpread { American = homeSpread }
                }
            },
            AwayTeamOdds = new EspnCoreTeamOdds
            {
                Current = new EspnCoreTeamOddsDetail
                {
                    PointSpread = new EspnCorePointSpread { American = awaySpread }
                }
            }
        };
    }

    // -----------------------------------------------------------------------
    // Early-return when GetWeekScores returns null
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Execute_WhenGetWeekScoresReturnsNull_ThrowsAndSavesNoSpreads()
    {
        _fetcher.FetchForWeekAsync(Arg.Any<NflSeasonWeekConfig>())
                .Returns((EspnScores?)null);

        await Assert.ThrowsAsync<InvalidOperationException>(() => BuildJob().Execute(_context));

        await _repo.DidNotReceive().UpsertAsync(Arg.Any<List<NflSpreads>>());
        await _oddsService.DidNotReceive()
                          .GetEventsWithOddsAsync(Arg.Any<int>(), Arg.Any<int>());
    }

    // -----------------------------------------------------------------------
    // No scheduled games — nothing saved
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Execute_WhenAllGamesAreAlreadyFinal_ThrowsAndSavesNoSpreads()
    {
        // frizat-4gn: a scoreboard with only Final games (nothing Scheduled) past lock time is
        // no longer a quiet no-op — the schedule exists specifically so real games' odds should
        // be gettable by lock time, so ending up with zero spreads now surfaces as a failure
        // (via the same job-failure/Discord alert pipeline every other job already relies on).
        _fetcher.FetchForWeekAsync(Arg.Any<NflSeasonWeekConfig>())
                .Returns(BuildScoreboard(statusName: TypeName.StatusFinal));

        await Assert.ThrowsAsync<InvalidOperationException>(() => BuildJob().Execute(_context));

        await _repo.DidNotReceive().UpsertAsync(Arg.Any<List<NflSpreads>>());
    }

    // -----------------------------------------------------------------------
    // Happy path — DraftKings odds available
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Execute_WhenDraftKingsOddsAvailable_CallsAddNewNflSpreads()
    {
        _fetcher.FetchForWeekAsync(Arg.Any<NflSeasonWeekConfig>())
                .Returns(BuildScoreboard());
        _oddsService.GetEventsWithOddsAsync(401547605, (int)EspnOddsProviders.DraftKings)
                    .Returns(BuildOddsItem());

        await BuildJob().Execute(_context);

        await _repo.Received(1).UpsertAsync(Arg.Is<List<NflSpreads>>(l => l.Count > 0));
    }

    [Fact]
    public async Task Execute_WhenDraftKingsOddsAvailable_ParsesHomeSpreadCorrectly()
    {
        _fetcher.FetchForWeekAsync(Arg.Any<NflSeasonWeekConfig>())
                .Returns(BuildScoreboard());
        _oddsService.GetEventsWithOddsAsync(401547605, (int)EspnOddsProviders.DraftKings)
                    .Returns(BuildOddsItem(homeSpread: "-7", awaySpread: "+7"));

        List<NflSpreads>? captured = null;
        await _repo.UpsertAsync(Arg.Do<List<NflSpreads>>(l => captured = l));

        await BuildJob().Execute(_context);

        Assert.NotNull(captured);
        Assert.Single(captured);
        Assert.Equal(-7.0, captured[0].HomeTeamSpread);
        Assert.Equal(7.0, captured[0].AwayTeamSpread);
    }

    [Fact]
    public async Task Execute_WhenDraftKingsOddsAvailable_ParsesDecimalSpreadCorrectly()
    {
        _fetcher.FetchForWeekAsync(Arg.Any<NflSeasonWeekConfig>())
                .Returns(BuildScoreboard());
        _oddsService.GetEventsWithOddsAsync(401547605, (int)EspnOddsProviders.DraftKings)
                    .Returns(BuildOddsItem(homeSpread: "-3.5", awaySpread: "+3.5"));

        List<NflSpreads>? captured = null;
        await _repo.UpsertAsync(Arg.Do<List<NflSpreads>>(l => captured = l));

        await BuildJob().Execute(_context);

        Assert.NotNull(captured);
        Assert.Equal(-3.5, captured[0].HomeTeamSpread);
        Assert.Equal(3.5, captured[0].AwayTeamSpread);
    }

    [Fact]
    public async Task Execute_WhenDraftKingsOddsAvailable_SetsOverUnder()
    {
        _fetcher.FetchForWeekAsync(Arg.Any<NflSeasonWeekConfig>())
                .Returns(BuildScoreboard());
        _oddsService.GetEventsWithOddsAsync(401547605, (int)EspnOddsProviders.DraftKings)
                    .Returns(BuildOddsItem(overUnder: 48.5));

        List<NflSpreads>? captured = null;
        await _repo.UpsertAsync(Arg.Do<List<NflSpreads>>(l => captured = l));

        await BuildJob().Execute(_context);

        Assert.NotNull(captured);
        Assert.Equal(48.5, captured[0].OverUnder);
    }

    [Fact]
    public async Task Execute_WhenDraftKingsOddsAvailable_PassesCorrectTeamAbbreviations()
    {
        _fetcher.FetchForWeekAsync(Arg.Any<NflSeasonWeekConfig>())
                .Returns(BuildScoreboard(homeAbbr: "SF", awayAbbr: "DAL"));
        _oddsService.GetEventsWithOddsAsync(401547605, (int)EspnOddsProviders.DraftKings)
                    .Returns(BuildOddsItem());

        List<NflSpreads>? captured = null;
        await _repo.UpsertAsync(Arg.Do<List<NflSpreads>>(l => captured = l));

        await BuildJob().Execute(_context);

        Assert.NotNull(captured);
        Assert.Equal("SF", captured[0].HomeTeam);
        Assert.Equal("DAL", captured[0].AwayTeam);
    }

    // -----------------------------------------------------------------------
    // Plus-sign stripping before parse
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Execute_PlusSignInSpreadString_IsStrippedBeforeParsing()
    {
        // "+7" should be parsed as 7.0 after stripping the leading plus
        _fetcher.FetchForWeekAsync(Arg.Any<NflSeasonWeekConfig>())
                .Returns(BuildScoreboard());
        _oddsService.GetEventsWithOddsAsync(401547605, (int)EspnOddsProviders.DraftKings)
                    .Returns(BuildOddsItem(homeSpread: "+3", awaySpread: "-3"));

        List<NflSpreads>? captured = null;
        await _repo.UpsertAsync(Arg.Do<List<NflSpreads>>(l => captured = l));

        await BuildJob().Execute(_context);

        Assert.NotNull(captured);
        Assert.Equal(3.0, captured[0].HomeTeamSpread);
        Assert.Equal(-3.0, captured[0].AwayTeamSpread);
    }

    // -----------------------------------------------------------------------
    // FK sentinel value — game skipped (double.TryParse fails for "FK")
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Execute_WhenHomeSpreadIsFk_GameIsSkippedAndThrowsSinceNoSpreadsSaved()
    {
        // "FK" cannot be parsed by double.TryParse → the game is skipped via `continue`. The only
        // game this run had ends up unsaved, so the run as a whole now throws (frizat-4gn) —
        // still worth a distinct test from a totally-failed fetch, since this path exercises the
        // per-game skip logic, not the fetch layer.
        _fetcher.FetchForWeekAsync(Arg.Any<NflSeasonWeekConfig>())
                .Returns(BuildScoreboard());
        _oddsService.GetEventsWithOddsAsync(401547605, (int)EspnOddsProviders.DraftKings)
                    .Returns(BuildOddsItem(homeSpread: "FK", awaySpread: "+7"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => BuildJob().Execute(_context));

        await _repo.DidNotReceive().UpsertAsync(Arg.Any<List<NflSpreads>>());
    }

    [Fact]
    public async Task Execute_WhenAwaySpreadIsFk_GameIsSkippedAndThrowsSinceNoSpreadsSaved()
    {
        _fetcher.FetchForWeekAsync(Arg.Any<NflSeasonWeekConfig>())
                .Returns(BuildScoreboard());
        _oddsService.GetEventsWithOddsAsync(401547605, (int)EspnOddsProviders.DraftKings)
                    .Returns(BuildOddsItem(homeSpread: "-7", awaySpread: "FK"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => BuildJob().Execute(_context));

        await _repo.DidNotReceive().UpsertAsync(Arg.Any<List<NflSpreads>>());
    }

    // -----------------------------------------------------------------------
    // DraftKings unavailable — falls back to all-providers list
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Execute_WhenDraftKingsNull_FallsBackToFirstAvailableProvider()
    {
        _fetcher.FetchForWeekAsync(Arg.Any<NflSeasonWeekConfig>())
                .Returns(BuildScoreboard());

        // DraftKings returns null
        _oddsService.GetEventsWithOddsAsync(401547605, (int)EspnOddsProviders.DraftKings)
                    .Returns((EspnCoreOddsItem?)null);

        // All-providers fallback returns a list with one item
        var fallbackItem = BuildOddsItem(homeSpread: "-6", awaySpread: "+6", overUnder: 45.0);
        fallbackItem.Provider.Name = "Caesars";
        _oddsService.GetEventsWithOddsAsync(401547605)
                    .Returns(new EspnCoreOddsApiResponse
                    {
                        Count = 1,  // job checks allResults.Count == 0 to skip
                        Items = new List<EspnCoreOddsItem> { fallbackItem }
                    });

        await BuildJob().Execute(_context);

        await _repo.Received(1).UpsertAsync(Arg.Is<List<NflSpreads>>(l => l.Count > 0));
    }

    [Fact]
    public async Task Execute_WhenDraftKingsNullAndFallbackEmpty_GameIsSkippedAndThrows()
    {
        _fetcher.FetchForWeekAsync(Arg.Any<NflSeasonWeekConfig>())
                .Returns(BuildScoreboard());

        _oddsService.GetEventsWithOddsAsync(401547605, (int)EspnOddsProviders.DraftKings)
                    .Returns((EspnCoreOddsItem?)null);
        _oddsService.GetEventsWithOddsAsync(401547605)
                    .Returns(new EspnCoreOddsApiResponse { Items = new List<EspnCoreOddsItem>() });

        await Assert.ThrowsAsync<InvalidOperationException>(() => BuildJob().Execute(_context));

        await _repo.DidNotReceive().UpsertAsync(Arg.Any<List<NflSpreads>>());
    }

    [Fact]
    public async Task Execute_WhenDraftKingsNullAndFallbackNull_GameIsSkippedAndThrows()
    {
        _fetcher.FetchForWeekAsync(Arg.Any<NflSeasonWeekConfig>())
                .Returns(BuildScoreboard());

        _oddsService.GetEventsWithOddsAsync(401547605, (int)EspnOddsProviders.DraftKings)
                    .Returns((EspnCoreOddsItem?)null);
        _oddsService.GetEventsWithOddsAsync(401547605)
                    .Returns((EspnCoreOddsApiResponse?)null);

        await Assert.ThrowsAsync<InvalidOperationException>(() => BuildJob().Execute(_context));

        await _repo.DidNotReceive().UpsertAsync(Arg.Any<List<NflSpreads>>());
    }

    // -----------------------------------------------------------------------
    // ESPN odds API exception — per-game exception is caught, job continues
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Execute_WhenOddsApiThrowsForTheOnlyGame_PerGameExceptionCaughtButRunStillThrows()
    {
        // The per-game try/catch still swallows the individual odds-fetch exception (it's not
        // what propagates) — but with only one game in this run and its odds fetch failing, the
        // run ends up with zero spreads, which now throws its own (different) exception, per
        // frizat-4gn.
        _fetcher.FetchForWeekAsync(Arg.Any<NflSeasonWeekConfig>())
                .Returns(BuildScoreboard());
        _oddsService.GetEventsWithOddsAsync(401547605, (int)EspnOddsProviders.DraftKings)
                    .ThrowsAsync(new HttpRequestException("Odds API down"));

        var exception = await Record.ExceptionAsync(() => BuildJob().Execute(_context));

        Assert.IsType<InvalidOperationException>(exception);
        Assert.DoesNotContain("Odds API down", exception!.Message);
    }

    [Fact]
    public async Task Execute_WhenOddsApiThrowsForOneGame_OtherGamesAreStillProcessed()
    {
        // Two events: first throws, second succeeds
        var scoreboard = new EspnScores
        {
            Season = new Season { Year = 2024, Type = (int)TypeOfSeason.RegularSeason },
            Week = new Week { Number = 5 },
            Events = new[]
            {
                new Event
                {
                    Id = "111",
                    Season = new Season { Year = 2024, Type = (int)TypeOfSeason.RegularSeason },
                    Week = new Week { Number = 5 },
                    Date = new DateTimeOffset(2024, 11, 10, 18, 0, 0, TimeSpan.Zero),
                    Competitions = new[]
                    {
                        new Competition
                        {
                            Date = new DateTimeOffset(2024, 11, 10, 18, 0, 0, TimeSpan.Zero),
                            Competitors = new[]
                            {
                                new Competitor { Id = "1", HomeAway = HomeAway.Home, Score = 0,
                                    Team = new EspnTeam { Abbreviation = "KC" }, Records = Array.Empty<EspnRecord>() },
                                new Competitor { Id = "2", HomeAway = HomeAway.Away, Score = 0,
                                    Team = new EspnTeam { Abbreviation = "BUF" }, Records = Array.Empty<EspnRecord>() }
                            },
                            Status = new EspnStatus { Type = new StatusType { Name = TypeName.StatusScheduled, Description = Description.Scheduled } },
                            Odds = Array.Empty<Odd>()
                        }
                    }
                },
                new Event
                {
                    Id = "222",
                    Season = new Season { Year = 2024, Type = (int)TypeOfSeason.RegularSeason },
                    Week = new Week { Number = 5 },
                    Date = new DateTimeOffset(2024, 11, 10, 18, 0, 0, TimeSpan.Zero),
                    Competitions = new[]
                    {
                        new Competition
                        {
                            Date = new DateTimeOffset(2024, 11, 10, 18, 0, 0, TimeSpan.Zero),
                            Competitors = new[]
                            {
                                new Competitor { Id = "3", HomeAway = HomeAway.Home, Score = 0,
                                    Team = new EspnTeam { Abbreviation = "SF" }, Records = Array.Empty<EspnRecord>() },
                                new Competitor { Id = "4", HomeAway = HomeAway.Away, Score = 0,
                                    Team = new EspnTeam { Abbreviation = "DAL" }, Records = Array.Empty<EspnRecord>() }
                            },
                            Status = new EspnStatus { Type = new StatusType { Name = TypeName.StatusScheduled, Description = Description.Scheduled } },
                            Odds = Array.Empty<Odd>()
                        }
                    }
                }
            }
        };

        _fetcher.FetchForWeekAsync(Arg.Any<NflSeasonWeekConfig>()).Returns(scoreboard);

        // First game throws
        _oddsService.GetEventsWithOddsAsync(111, (int)EspnOddsProviders.DraftKings)
                    .ThrowsAsync(new HttpRequestException("Odds API down"));

        // Second game succeeds
        _oddsService.GetEventsWithOddsAsync(222, (int)EspnOddsProviders.DraftKings)
                    .Returns(BuildOddsItem(homeSpread: "-3", awaySpread: "+3", overUnder: 47.0));

        await BuildJob().Execute(_context);

        // Second game's spread should be persisted
        await _repo.Received(1).UpsertAsync(
            Arg.Is<List<NflSpreads>>(l => l.Count == 1 && l[0].HomeTeam == "SF"));
    }

    // -----------------------------------------------------------------------
    // Zero scheduled competitions — frizat-4gn: individual teams have byes, but the league as a
    // whole never has zero scheduled games in an in-scope week, so this is no longer treated as
    // a legitimate "bye week" no-op — it now throws like any other zero-spreads-past-lock-time run.
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Execute_WhenZeroCompetitions_ThrowsAndSavesNoSpreads()
    {
        _fetcher.FetchForWeekAsync(Arg.Any<NflSeasonWeekConfig>())
                .Returns(BuildScoreboard(emptyEvents: true));

        await Assert.ThrowsAsync<InvalidOperationException>(() => BuildJob().Execute(_context));

        await _repo.DidNotReceive().UpsertAsync(Arg.Any<List<NflSpreads>>());
        await _oddsService.DidNotReceive().GetEventsWithOddsAsync(Arg.Any<int>(), Arg.Any<int>());
    }

    [Fact]
    public async Task Execute_RegularSeasonWeekWithGames_IsNotTreatedAsByeWeek()
    {
        _fetcher.FetchForWeekAsync(Arg.Any<NflSeasonWeekConfig>())
                .Returns(BuildScoreboard(weekNumber: 5, seasonType: (int)TypeOfSeason.RegularSeason));
        _oddsService.GetEventsWithOddsAsync(401547605, (int)EspnOddsProviders.DraftKings)
                    .Returns(BuildOddsItem());

        await BuildJob().Execute(_context);

        await _repo.Received(1).UpsertAsync(Arg.Is<List<NflSpreads>>(l => l.Count > 0));
    }

    [Fact]
    public async Task Execute_PostSeasonConferenceChampionshipWeek_IsNotTreatedAsByeWeek()
    {
        _nflCurrentWeekService.GetCurrentWeekAsync()
            .Returns(new NflWeekInfo(21, 2024, true, "Conference Championship", "Standard", PastLockTime));
        _repo.GetNflSeasonWeekConfigsAsync(2024).Returns(new List<NflSeasonWeekConfig> { BuildConfig(weekId: 21, season: 2024) });
        _fetcher.FetchForWeekAsync(Arg.Any<NflSeasonWeekConfig>())
                .Returns(BuildScoreboard(weekNumber: 3, seasonType: (int)TypeOfSeason.PostSeason));
        _oddsService.GetEventsWithOddsAsync(401547605, (int)EspnOddsProviders.DraftKings)
                    .Returns(BuildOddsItem());

        await BuildJob().Execute(_context);

        await _repo.Received(1).UpsertAsync(Arg.Is<List<NflSpreads>>(l => l.Count > 0));
    }

    // -----------------------------------------------------------------------
    // Post-season week mapping — WeekId from NflCurrentWeekService drives NflWeek on spread
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Execute_PostSeasonWeek1_MapsToWeek19()
    {
        // Wild Card: NflCurrentWeekService returns WeekId=19, IsPostSeason=true
        _nflCurrentWeekService.GetCurrentWeekAsync()
            .Returns(new NflWeekInfo(19, 2024, true, "Wild Card Weekend", "Standard", PastLockTime));
        _repo.GetNflSeasonWeekConfigsAsync(2024).Returns(new List<NflSeasonWeekConfig> { BuildConfig(weekId: 19, season: 2024) });
        _fetcher.FetchForWeekAsync(Arg.Any<NflSeasonWeekConfig>())
                .Returns(BuildScoreboard(weekNumber: 1, seasonType: (int)TypeOfSeason.PostSeason));
        _oddsService.GetEventsWithOddsAsync(401547605, (int)EspnOddsProviders.DraftKings)
                    .Returns(BuildOddsItem());

        List<NflSpreads>? captured = null;
        await _repo.UpsertAsync(Arg.Do<List<NflSpreads>>(l => captured = l));

        await BuildJob().Execute(_context);

        Assert.NotNull(captured);
        Assert.Single(captured);
        Assert.Equal(19, captured[0].NflWeek);
    }

    // -----------------------------------------------------------------------
    // Lock-time write guard (frizat-pxy follow-on): no automated or manual write
    // before a week's SpreadLockDatetime, unless explicitly forced via JobDataMap.
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Execute_LockTimeInFuture_NoForce_SkipsWithoutFetching()
    {
        _nflCurrentWeekService.GetCurrentWeekAsync()
            .Returns(new NflWeekInfo(5, 2024, false, "Week 5", "Standard", FutureLockTime));

        await BuildJob().Execute(_context);

        await _fetcher.DidNotReceive().FetchForWeekAsync(Arg.Any<NflSeasonWeekConfig>());
        await _repo.DidNotReceive().UpsertAsync(Arg.Any<List<NflSpreads>>());
    }

    [Fact]
    public async Task Execute_LockTimeInFuture_Forced_WritesAnyway()
    {
        _nflCurrentWeekService.GetCurrentWeekAsync()
            .Returns(new NflWeekInfo(5, 2024, false, "Week 5", "Standard", FutureLockTime));
        var forceMap = new JobDataMap();
        forceMap.Put("force", true);
        _context.MergedJobDataMap.Returns(forceMap);
        _fetcher.FetchForWeekAsync(Arg.Any<NflSeasonWeekConfig>())
                .Returns(BuildScoreboard());
        _oddsService.GetEventsWithOddsAsync(401547605, (int)EspnOddsProviders.DraftKings)
                    .Returns(BuildOddsItem());

        await BuildJob().Execute(_context);

        await _repo.Received(1).UpsertAsync(Arg.Is<List<NflSpreads>>(l => l.Count > 0));
    }

    [Fact]
    public async Task Execute_LockTimeInPast_NoForce_WritesNormally()
    {
        // DefaultWeek's lock time is already in the past — the common/happy-path case
        _fetcher.FetchForWeekAsync(Arg.Any<NflSeasonWeekConfig>())
                .Returns(BuildScoreboard());
        _oddsService.GetEventsWithOddsAsync(401547605, (int)EspnOddsProviders.DraftKings)
                    .Returns(BuildOddsItem());

        await BuildJob().Execute(_context);

        await _repo.Received(1).UpsertAsync(Arg.Is<List<NflSpreads>>(l => l.Count > 0));
    }
}
