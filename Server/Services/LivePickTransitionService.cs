using FourPlayWebApp.Shared.Helpers;
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
    ITeamLiveNotificationStateService teamStateService,
    INotificationPreferencesService preferencesService,
    INotificationDispatcher dispatcher,
    LiveScoreSnapshotStore snapshotStore,
    TimeProvider timeProvider,
    ILogger<LivePickTransitionService> logger) : ILivePickTransitionService
{
    private sealed record LivePick(int PickId, string UserId, int LeagueId, PickRow Pick);

    // One (team, pickType) group's shared result for this tick — every picker making the SAME bet
    // (same team, same PickType: an Over and a Spread pick anchored on the same team are different
    // bets with different win conditions) computes an identical cover state, so "others" is
    // aggregated and deduped once per bet, not once per picker (frizat-cov).
    // A class, not a record — PickerUserIds is mutated in place as more pickers are found, which
    // would be misleading on a type that signals value semantics.
    private sealed class BetResult(bool covering, bool isFinal, string firstPickerUserId)
    {
        public bool Covering { get; } = covering;
        public bool IsFinal { get; } = isFinal;
        public List<string> PickerUserIds { get; } = [firstPickerUserId];
    }

    // Shared by both the per-pick "mine" state machine and the per-bet "others" one — baseline on
    // the first read after the quiet window (silent, never a push), a flip from that baseline
    // notifies once, and the final ping is tracked and notified independently of the above.
    private readonly record struct Transition(bool Changed, bool? Covering, DateTimeOffset? LastNotifiedAt,
        bool NotifyTransition, DateTimeOffset? FinalNotifiedAt, bool NotifyFinal);

    private static Transition ComputeTransition(bool? priorCovering, DateTimeOffset? priorLastNotifiedAt,
        DateTimeOffset? priorFinalNotifiedAt, bool covering, bool isFinal, DateTimeOffset now)
    {
        var covering_ = priorCovering;
        var lastNotifiedAt = priorLastNotifiedAt;
        var coveringChanged = false;
        var notifyTransition = false;
        if (priorCovering is null) {
            covering_ = covering;
            lastNotifiedAt = now;
            coveringChanged = true;
        } else if (priorCovering != covering) {
            covering_ = covering;
            lastNotifiedAt = now;
            coveringChanged = true;
            notifyTransition = true;
        }

        var finalNotifiedAt = priorFinalNotifiedAt;
        var notifyFinal = false;
        var finalChanged = false;
        if (isFinal && priorFinalNotifiedAt is null) {
            finalNotifiedAt = now;
            notifyFinal = true;
            finalChanged = true;
        }

        return new Transition(coveringChanged || finalChanged, covering_, lastNotifiedAt, notifyTransition, finalNotifiedAt, notifyFinal);
    }

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
        // Pushes show the full name ("Buffalo Bulls"); dedup state stays keyed by abbreviation.
        var teamNames = GameHelpers.GetTeamDisplayNames(current);
        string NameOf(string abbr) => teamNames.GetValueOrDefault(abbr, abbr);

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
        // Accumulated across every league, not persisted until the end — must share the same
        // all-or-nothing write boundary as newStates below, or a later league throwing mid-loop
        // could leave "others" state committed for an earlier league while "mine" state for that
        // same earlier league is lost, letting the two channels silently diverge.
        var newTeamStates = new List<TeamLiveNotificationState>();

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
            var betResults = new Dictionary<(string Team, PickType PickType), BetResult>();

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

                var isFinal = competition.Status.Type.Name == TypeName.StatusFinal;

                // "Mine" — one push to the pick's own owner, unaffected by how many other members
                // made the same bet.
                existingStates.TryGetValue(pick.PickId, out var prior);
                var mineTransition = ComputeTransition(prior?.LastNotifiedCovering, prior?.LastNotifiedAt, prior?.FinalNotifiedAt, covering, isFinal, now);
                if (mineTransition.Changed) {
                    newStates.Add(new PickLiveNotificationState {
                        Sport = sport, PickId = pick.PickId, LeagueId = pick.LeagueId,
                        LastNotifiedCovering = mineTransition.Covering,
                        LastNotifiedAt = mineTransition.LastNotifiedAt,
                        FinalNotifiedAt = mineTransition.FinalNotifiedAt,
                    });
                }
                if (mineTransition.NotifyTransition)
                    await SafeDispatchAsync(pick.UserId, MinePreference(covering, false), MinePayload(sport, NameOf(pick.Pick.Team), covering, atFinal: false));
                // Independent of the above — fires once at final regardless of recent transition
                // history, even if this tick's status flip happens to also be a cover-state flip.
                if (mineTransition.NotifyFinal)
                    await SafeDispatchAsync(pick.UserId, MinePreference(covering, true), MinePayload(sport, NameOf(pick.Pick.Team), covering, atFinal: true));

                // Record for the per-bet "others" pass below — every picker on the same
                // (team, pickType) computes this exact same covering/isFinal, so only the picker
                // list differs across rows.
                var key = (pick.Pick.Team, pick.Pick.PickType);
                if (betResults.TryGetValue(key, out var bet)) bet.PickerUserIds.Add(pick.UserId);
                else betResults[key] = new BetResult(covering, isFinal, pick.UserId);
            }

            if (betResults.Count == 0) continue;

            // "Others" — exactly one aggregated push per (team, pickType) bet that actually
            // transitioned, naming how many members made it, not one push per picker (frizat-cov).
            var priorTeamStates = await teamStateService.GetStatesAsync(sport, byLeague.Key, period.Id, betResults.Keys.ToList());
            foreach (var (bet, result) in betResults)
            {
                priorTeamStates.TryGetValue(bet, out var priorTeam);
                var teamTransition = ComputeTransition(priorTeam?.LastNotifiedCovering, priorTeam?.LastNotifiedAt, priorTeam?.FinalNotifiedAt, result.Covering, result.IsFinal, now);
                if (teamTransition.Changed) {
                    newTeamStates.Add(new TeamLiveNotificationState {
                        Sport = sport, LeagueId = byLeague.Key, Team = bet.Team, PickType = bet.PickType, Period = period.Id,
                        LastNotifiedCovering = teamTransition.Covering,
                        LastNotifiedAt = teamTransition.LastNotifiedAt,
                        FinalNotifiedAt = teamTransition.FinalNotifiedAt,
                    });
                }

                var otherMembers = members.Where(m => !result.PickerUserIds.Contains(m.UserId)).ToList();
                if (teamTransition.NotifyTransition)
                    await NotifyOthersAsync(sport, otherMembers, NameOf(bet.Team), result.Covering, result.PickerUserIds.Count, atFinal: false);
                if (teamTransition.NotifyFinal)
                    await NotifyOthersAsync(sport, otherMembers, NameOf(bet.Team), result.Covering, result.PickerUserIds.Count, atFinal: true);
            }
        }

        if (newStates.Count > 0) await stateService.UpsertStatesAsync(newStates);
        if (newTeamStates.Count > 0) await teamStateService.UpsertStatesAsync(newTeamStates);
    }

    private async Task NotifyOthersAsync(LeagueType sport, List<LeagueUserMapping> otherMembers, string team, bool covering, int pickerCount, bool atFinal)
    {
        if (otherMembers.Count == 0) return;
        var payload = OthersPayload(team, covering, pickerCount, atFinal) with { Sport = sport };
        await Task.WhenAll(otherMembers.Select(m => SafeDispatchAsync(m.UserId, OthersPreference(covering, atFinal), payload)));
    }

    private static PushPayload MinePayload(LeagueType sport, string team, bool covering, bool atFinal) => (atFinal
        ? new PushPayload("IV League", covering ? $"{team} covered! Final. 🏆" : $"{team} didn't cover. Final. 💀")
        : new PushPayload("IV League", covering ? $"{team} are covering! 🟢" : $"{team} are bloody right now 🔴")) with { Sport = sport };

    private static PushPayload OthersPayload(string team, bool covering, int pickerCount, bool atFinal)
    {
        var usersLabel = pickerCount == 1 ? "1 user picked" : $"{pickerCount} users picked";
        return atFinal
            ? new PushPayload("IV League", covering ? $"{team} covered! Final — {usersLabel} 🏆" : $"{team} didn't cover! Final — {usersLabel} 💀")
            : new PushPayload("IV League", covering ? $"{team} are Covering — {usersLabel} 🟢" : $"{team} are Bloody — {usersLabel} 🔴");
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
