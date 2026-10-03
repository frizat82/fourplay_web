using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Server.Services.Repositories.Interfaces;
using FourPlayWebApp.Shared.Models;
using FourPlayWebApp.Shared.Models.Data;
using FourPlayWebApp.Shared.Models.Data.Dtos;
using FourPlayWebApp.Shared.Models.Enum;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace FourPlayWebApp.Server.Services;

public class LivePickTransitionService(
    IEspnCacheService espnCache,
    ICfbCacheService cfbCache,
    INflCurrentWeekService nflCurrentWeek,
    ICfbCurrentSlateService cfbCurrentSlate,
    ILeagueRepository leagueRepository,
    ICfbPicksRepository cfbPicksRepository,
    ICfbRepository cfbRepository,
    IPickLiveNotificationStateService stateService,
    INotificationPreferencesService preferencesService,
    INotificationDispatcher dispatcher,
    LiveScoreSnapshotStore snapshotStore,
    TimeProvider timeProvider,
    ILogger<LivePickTransitionService> logger) : ILivePickTransitionService
{
    private sealed record LivePick(int PickId, string UserId, int LeagueId, PickRow Pick);

    public async Task RecomputeAsync(LeagueType sport, CancellationToken cancellationToken = default)
    {
        // Default preferences are all-off; most of the time this should end the whole recompute
        // before any other work happens.
        if (!await preferencesService.AnyLiveNotificationPreferenceEnabledAsync()) return;

        var current = sport == LeagueType.Nfl ? await espnCache.GetScoresAsync() : await cfbCache.GetScoresAsync();
        if (current?.Events is null) return;

        var previous = snapshotStore.Exchange(sport, current);
        var changed = DiffCompetitions(previous, current);
        if (changed.Count == 0) return;

        var resolved = sport == LeagueType.Nfl ? await ResolveNflPeriodAsync() : await ResolveCfbPeriodAsync();
        if (resolved is not { } period) return;

        var teams = changed.SelectMany(c => c.Competitors.Select(x => x.Team.Abbreviation)).Distinct().ToList();
        if (teams.Count == 0) return;

        var picks = sport == LeagueType.Nfl
            ? (await leagueRepository.GetNflPicksForTeamsAsync(period.Season, period.Id, teams))
                .Select(p => new LivePick(p.Id, p.UserId, p.LeagueId, new PickRow(p.Team, p.Pick))).ToList()
            : (await cfbPicksRepository.GetCfbPicksForTeamsAsync(period.Season, period.Id, teams))
                .Select(p => new LivePick(p.Id, p.UserId, p.LeagueId, new PickRow(p.Team, p.PickType))).ToList();
        if (picks.Count == 0) return;

        var spreads = sport == LeagueType.Nfl
            ? (await leagueRepository.GetNflSpreadsAsync(period.Season, period.Id) ?? []).Cast<IOddsRow>().ToList()
            : (await cfbRepository.GetLeagueEligibleSpreadsForSeasonAsync(period.Season))
                .Where(s => s.CfbSlateId == period.Id).Cast<IOddsRow>().ToList();

        var now = timeProvider.GetUtcNow();
        var existingStates = await stateService.GetStatesAsync(sport, picks.Select(p => p.PickId).ToList());
        var newStates = new List<PickLiveNotificationState>();

        // Grouped by league so each league's own tease (JuiceTiers) is applied — "covering" is a
        // league-specific answer, not a single global one, exactly like LeaderboardEngine.Build.
        foreach (var byLeague in picks.GroupBy(p => p.LeagueId))
        {
            var juiceMapping = await leagueRepository.GetLeagueJuiceMappingAsync(byLeague.Key, period.Season);
            if (juiceMapping is null) continue;
            // JuiceTiers/GetCfbRequiredPicks need the CFB slate NUMBER (1-18), never its DB row id
            // — those are two distinct identifiers for the same slate (see ResolveCfbPeriodAsync).
            var calculator = new SpreadCalculator(spreads, JuiceTiers.For(sport, period.JuiceTierNumber, juiceMapping));
            var members = await leagueRepository.GetLeagueUserMappingsAsync(byLeague.Key);

            foreach (var pick in byLeague)
            {
                var competition = changed.FirstOrDefault(c => c.Competitors.Any(x => x.Team.Abbreviation == pick.Pick.Team));
                if (competition is null) continue;
                // Trivial artifact at kickoff (one side is already mathematically bloody/covering
                // purely from the spread's sign) — leave the pick alone until the quiet window elapses.
                if (now < competition.Date + LivePickNotificationCadence.QuietWindow) continue;

                var mine = competition.Competitors.First(c => c.Team.Abbreviation == pick.Pick.Team);
                var other = competition.Competitors.First(c => c.Team.Abbreviation != pick.Pick.Team);

                bool covering;
                try { covering = calculator.DidUserWinPick(pick.Pick.Team, (int)mine.Score, (int)other.Score, pick.Pick.PickType); }
                catch (Exception ex) {
                    logger.LogError(ex, "LivePickTransitionService: error scoring live pick {@Pick}", pick);
                    continue;
                }

                existingStates.TryGetValue(pick.PickId, out var prior);
                var newState = new PickLiveNotificationState {
                    Sport = sport, PickId = pick.PickId, LeagueId = pick.LeagueId,
                    LastNotifiedCovering = prior?.LastNotifiedCovering,
                    LastNotifiedAt = prior?.LastNotifiedAt,
                    FinalNotifiedAt = prior?.FinalNotifiedAt,
                };

                var stateChanged = false;
                if (prior?.LastNotifiedCovering is null) {
                    // First read at/after the quiet window — silent baseline, never a push.
                    newState.LastNotifiedCovering = covering;
                    newState.LastNotifiedAt = now;
                    stateChanged = true;
                } else if (prior.LastNotifiedCovering != covering) {
                    newState.LastNotifiedCovering = covering;
                    newState.LastNotifiedAt = now;
                    stateChanged = true;
                    await NotifyAsync(pick, members, covering, atFinal: false);
                }

                // Independent of the above — fires once at final regardless of recent transition
                // history, even if this tick's status flip happens to also be a cover-state flip.
                var isFinal = competition.Status.Type.Name == TypeName.StatusFinal;
                if (isFinal && prior?.FinalNotifiedAt is null) {
                    newState.FinalNotifiedAt = now;
                    stateChanged = true;
                    await NotifyAsync(pick, members, covering, atFinal: true);
                }

                if (stateChanged) newStates.Add(newState);
            }
        }

        if (newStates.Count > 0) await stateService.UpsertStatesAsync(newStates);
    }

    private async Task NotifyAsync(LivePick pick, List<LeagueUserMapping> members, bool covering, bool atFinal)
    {
        var payload = atFinal
            ? new PushPayload("IV League", covering ? $"{pick.Pick.Team} covered! Final. 🏆" : $"{pick.Pick.Team} didn't cover. Final. 💀")
            : new PushPayload("IV League", covering ? $"{pick.Pick.Team} is covering! 🟢" : $"{pick.Pick.Team} is bloody right now 🔴");

        var mine = SafeDispatchAsync(pick.UserId, MinePreference(covering, atFinal), payload);
        var others = members.Where(m => m.UserId != pick.UserId)
            .Select(m => SafeDispatchAsync(m.UserId, OthersPreference(covering, atFinal), payload));
        await Task.WhenAll([mine, .. others]);
    }

    private static Func<NotificationPreferencesDto, bool> MinePreference(bool covering, bool atFinal) => atFinal
        ? (covering ? p => p.NotifyMineCoveringAtFinal : p => p.NotifyMineBloodyAtFinal)
        : (covering ? p => p.NotifyMineCoveringDuringGame : p => p.NotifyMineBloodyDuringGame);

    private static Func<NotificationPreferencesDto, bool> OthersPreference(bool covering, bool atFinal) => atFinal
        ? (covering ? p => p.NotifyOthersCoveringAtFinal : p => p.NotifyOthersBloodyAtFinal)
        : (covering ? p => p.NotifyOthersCoveringDuringGame : p => p.NotifyOthersBloodyDuringGame);

    // One bad dispatch must not cost any other user their notification this tick — mirrors
    // WeekResultNotificationService's identical per-row isolation.
    private async Task SafeDispatchAsync(string userId, Func<NotificationPreferencesDto, bool> isEnabled, PushPayload payload)
    {
        try { await dispatcher.DispatchAsync(userId, isEnabled, payload); }
        catch (Exception ex) { logger.LogError(ex, "LivePickTransitionService: dispatch failed for user {UserId}", userId); }
    }

    /// <param name="Season">Season year.</param>
    /// <param name="Id">The value FK-scoped repository calls key on — NflSeasonWeekConfig.WeekId
    /// for NFL, CfbSlates' own DB row id for CFB.</param>
    /// <param name="JuiceTierNumber">The value JuiceTiers.For/GetCfbRequiredPicks expect — for NFL
    /// this is the same WeekId as Id (NFL's week number already serves both roles); for CFB it's
    /// the slate's 1-18 SlateNumber, which is NOT the same as its DB row id (Id grows across every
    /// season and never resets, so reusing it here would silently apply the wrong tease tier).</param>
    private readonly record struct ResolvedPeriod(int Season, int Id, int JuiceTierNumber);

    private async Task<ResolvedPeriod?> ResolveNflPeriodAsync()
    {
        var week = await nflCurrentWeek.GetCurrentWeekAsync();
        return new ResolvedPeriod(week.Season, week.WeekId, week.WeekId);
    }

    private async Task<ResolvedPeriod?> ResolveCfbPeriodAsync()
    {
        var slate = await cfbCurrentSlate.GetCurrentSlateAsync();
        return slate is null ? null : new ResolvedPeriod(slate.Season, slate.Id, slate.SlateNumber);
    }

    private static List<Competition> DiffCompetitions(EspnScores? previous, EspnScores current)
    {
        var previousById = (previous?.Events ?? []).SelectMany(e => e.Competitions ?? [])
            .ToDictionary(c => c.Id, ScoreStatusFingerprint);
        var changed = new List<Competition>();
        foreach (var competition in (current.Events ?? []).SelectMany(e => e.Competitions ?? []))
        {
            if (!previousById.TryGetValue(competition.Id, out var priorFingerprint) || priorFingerprint != ScoreStatusFingerprint(competition))
                changed.Add(competition);
        }
        return changed;
    }

    // Deliberately NOT EspnScoresFingerprint.Compute (used by EspnCacheService/CfbCacheService to
    // decide whether to raise ScoresChanged at all, so the SSE-driven Scores page updates for
    // down/distance/clock/possession too) — cover state only depends on score + final-status, so
    // reusing the broader fingerprint here would re-score every live pick on every down-and-distance
    // tick, not just on an actual scoring play. A second, narrower "did this specific thing change"
    // question, answered independently on purpose.
    private static string ScoreStatusFingerprint(Competition c) =>
        $"{string.Join(",", c.Competitors.Select(x => $"{x.Team.Abbreviation}:{x.Score}"))}|{c.Status.Type.Name}";
}
