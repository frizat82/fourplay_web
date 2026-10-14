using System.Text.Json;
using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Shared.Models;
using Serilog;

namespace FourPlayWebApp.Server.Services;

/// <summary>
/// Serves an ordered sequence of real captured ESPN snapshots (scheduled -> halftime ->
/// in_progress -> final — see sample_espn_nfl_*.json at the repo root, captured frizat-703.5) and
/// advances on an explicit external trigger (a test-only admin endpoint) rather than a timer, so
/// the live click-through E2E test can drive real state transitions on demand instead of waiting
/// for an actual game (frizat-703.6). Implements both IEspnCacheService and ICfbCacheService —
/// identical shape after the frizat-703.6 unification — so ONE replay sequence backs either
/// sport's live-data endpoint and SSE stream with zero frontend changes. Registered only when
/// DEMO_REPLAY_MODE=true; never used in dev or prod.
/// </summary>
public class ReplayCacheService : IEspnCacheService, ICfbCacheService {
    private readonly EspnScores[] _snapshots;
    private int _index;

    public event Action? ScoresChanged;

    // Which (season, week) the replayed game IS: the control table's current week (frizat-d47) —
    // replay drives the live week, so no ESPN week numbering is ever translated.
    private readonly INflCurrentWeekService _currentWeek;

    public ReplayCacheService(IReadOnlyList<EspnScores> snapshots, INflCurrentWeekService currentWeek) {
        _currentWeek = currentWeek;
        if (snapshots.Count == 0) throw new ArgumentException("At least one snapshot is required.", nameof(snapshots));
        _snapshots = snapshots.ToArray();
    }

    // Chronological order — real captured values throughout except "scheduled", which is
    // definitionally 0-0/not-started for any game (see frizat-703.5 for how each was captured).
    private static readonly string[] FixtureFileOrder = [
        "sample_espn_nfl_scheduled.json",
        "sample_espn_nfl_halftime.json",
        "sample_espn_nfl_in_progress.json",
        "sample_espn_nfl_final.json",
    ];

    public static ReplayCacheService LoadFromFixtureFiles(string repoRootPath, INflCurrentWeekService currentWeek) {
        var snapshots = new List<EspnScores>();
        foreach (var fileName in FixtureFileOrder) {
            var path = Path.Combine(repoRootPath, fileName);
            if (!File.Exists(path)) {
                Log.Warning("ReplayCacheService: fixture {Path} not found — skipping", path);
                continue;
            }
            var json = File.ReadAllText(path);
            var scores = JsonSerializer.Deserialize<EspnScores>(json, EspnApiServiceJsonConverter.Settings);
            if (scores is not null) snapshots.Add(scores);
        }
        RewriteKickoffTimes(snapshots);
        Log.Information("ReplayCacheService: loaded {Count} replay snapshots", snapshots.Count);
        return new ReplayCacheService(snapshots, currentWeek);
    }

    // The captured fixtures embed the real game's original 2025 kickoff time, which is always in
    // the past by the time this runs — PicksPage locks picks once gameTime <= now (see
    // gameIsLocked in PicksPage.tsx). Rewrite every snapshot's kickoff to the same near-future
    // timestamp (computed once, applied uniformly) so the game stays pickable regardless of when
    // this process happens to start, without baking a timestamp into the fixture files themselves.
    private static void RewriteKickoffTimes(List<EspnScores> snapshots) {
        var kickoff = DateTimeOffset.UtcNow.AddMinutes(30);
        foreach (var scores in snapshots) {
            foreach (var ev in scores.Events ?? []) {
                ev.Date = kickoff;
                foreach (var competition in ev.Competitions ?? []) {
                    competition.Date = kickoff;
                }
            }
        }
    }

    public Task<EspnScores?> GetScoresAsync() => Task.FromResult<EspnScores?>(_snapshots[_index]);

    // Replay mode drives one fixed game through scheduled->final — it has no concept of "other
    // weeks". It answers only for the control table's current week (the live week replay stands
    // in for); never falls through to a real ESPN call (that would defeat the point of replay).
    public async Task<EspnScores?> GetWeekScoresAsync(int season, int nflWeek) {
        var current = await _currentWeek.GetCurrentWeekAsync();
        return current.Season == season && current.WeekId == nflWeek ? _snapshots[_index] : null;
    }

    /// <summary>Advances to the next captured snapshot. No-op once at the last one.</summary>
    public void Advance() {
        if (_index >= _snapshots.Length - 1) return;
        _index++;
        ScoresChanged?.Invoke();
    }

    /// <summary>
    /// Rewinds to the first (scheduled) snapshot. NFL and CFB E2E specs both drive this same
    /// instance through its own full scheduled->final sequence — without a reset, whichever spec
    /// runs first exhausts it, leaving nothing for the other to observe from "scheduled". Each
    /// spec calls this before picking, so either can run standalone or after the other.
    /// </summary>
    public void Reset() {
        if (_index == 0) return;
        _index = 0;
        ScoresChanged?.Invoke();
    }

    // No-op: replay mode drives fixed captured snapshots, never a live NflScoresJob upsert.
    public void InvalidateWeekCache(int season, int week) { }

    // Replay mode drives one fixed game/slate through scheduled->final — it has no concept of
    // "other slates" (mirrors GetWeekScoresAsync's identical NFL constraint). Always returns the
    // current snapshot regardless of which slateId was requested — the CFB Scores/Picks page
    // (cfbAdapter.ts's loadScoresForSlate) always asks for the one real slate the replay game
    // was seeded into (frizat-d0t: this endpoint now goes through ICfbCacheService.
    // GetSlateScoresAsync instead of ICfbLiveScoreFetcher.FetchForSlateAsync directly).
    public Task<EspnScores?> GetSlateScoresAsync(int slateId) => Task.FromResult<EspnScores?>(_snapshots[_index]);

    // No-op: replay mode drives fixed captured snapshots, never a live CfbScoresJob upsert.
    public void InvalidateSlateCache(int slateId) { }
}
