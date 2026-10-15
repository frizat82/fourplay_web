using Microsoft.Extensions.DependencyInjection;
using FourPlayWebApp.Shared.Helpers;
using FourPlayWebApp.Shared.Models;
using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Server.Services.Repositories.Interfaces;
using FourPlayWebApp.Shared.Models.Enum;
using Serilog;

namespace FourPlayWebApp.Server.Services;

public class WeekResultNotificationService(
    ILeagueRepository leagueRepository,
    ICfbRepository cfbRepository,
    ILeaderboardService nflLeaderboardService,
    ICfbLeaderboardService cfbLeaderboardService,
    INotificationDispatcher dispatcher,
    IEspnCacheService nflScores,
    ICfbCacheService cfbScores,
    [FromKeyedServices(CurrentWeekClock.Key)] TimeProvider clock) : IWeekResultNotificationService
{
    // A week's result may only be pushed until this long after the week's scheduled end — long
    // enough for a late Monday-night / late-slate final, never long enough to replay history.
    // Anything older (e.g. every week decided before this feature first ran mid-season) is
    // recorded as sent silently, so it can never be pushed later either.
    public static readonly TimeSpan PushWindowAfterPeriodEnd = TimeSpan.FromHours(48);

    // Each period's end and name come from its control-table row (frizat-d47) — the label is the
    // row's own WeekLabel, never derived from the week number.
    private sealed record Period(DateTimeOffset End, string Label);

    public Task<int> CheckNflWeekResultsAsync(int season) =>
        CheckWeekResultsAsync(LeagueType.Nfl, season, "NFL",
            async () => (await leagueRepository.GetNflSeasonWeekConfigsAsync(season))
                .GroupBy(c => c.WeekId)
                .ToDictionary(g => g.Key, g => new Period(
                    new DateTimeOffset(DateTime.SpecifyKind(g.Max(c => c.WeekEndDatetime), DateTimeKind.Utc)), g.First().WeekLabel)),
            nflScores.GetScoresAsync, league => nflLeaderboardService.BuildLeaderboard(league.Id, season));

    public Task<int> CheckCfbWeekResultsAsync(int season) =>
        CheckWeekResultsAsync(LeagueType.Cfb, season, "CFB",
            // Per-season read: fails loudly on a slate with no control-table row rather than
            // pushing a blank round name.
            async () => (await cfbRepository.GetSlatesForSeasonAsync(season))
                .ToDictionary(s => s.SlateNumber, s => new Period(new DateTimeOffset(s.EndUtc), s.WeekLabel)),
            cfbScores.GetScoresAsync, league => cfbLeaderboardService.BuildLeaderboard(league.Id, season));

    private async Task<int> CheckWeekResultsAsync(LeagueType sport, int season, string sportLabel,
        Func<Task<Dictionary<int, Period>>> getPeriods,
        Func<Task<EspnScores?>> getScores, Func<LeagueInfo, Task<List<LeaderboardModel>>> buildLeaderboard)
    {
        var leagues = await leagueRepository.GetLeaguesByTypeAsync(sport);
        if (leagues.Count == 0) return 0;
        var periods = await getPeriods();
        var now = clock.GetUtcNow();
        // A week with no schedule row is treated as stale — never pushed (so it never needs a label).
        bool IsPushable(int week) => periods.TryGetValue(week, out var p) && now <= p.End + PushWindowAfterPeriodEnd;
        string periodLabel(int week) => $"{sportLabel} {periods[week].Label}";
        // The just-decided week is the one the live scoreboard holds; any team missing from it
        // (an older week catching up) falls back to its abbreviation.
        var teamNames = GameHelpers.GetTeamDisplayNames(await getScores());
        var sentCount = 0;
        foreach (var league in leagues)
        {
            var leaderboard = await buildLeaderboard(league);
            sentCount += await NotifyDecidedWeeksAsync(sport, league.Id, league.LeagueName, season, periodLabel, IsPushable, teamNames, leaderboard);
        }
        return sentCount;
    }

    private async Task<int> NotifyDecidedWeeksAsync(LeagueType sport, int leagueId, string leagueName, int season, Func<int, string> periodLabel,
        Func<int, bool> isPushable, IReadOnlyDictionary<string, string> teamNames, List<LeaderboardModel> leaderboard)
    {
        if (leaderboard.Count == 0) return 0;

        // One query for every already-sent (user, week) pair in this league/season, checked in
        // memory per row below — not one query per user per week. Generic "already notified"
        // regardless of outcome: a week's result, once decided (Won or Lost), is final and can
        // never flip, so one dedup flag per (user, week) covers both cases with no schema change.
        var alreadySent = await leagueRepository.GetWeekResultNotificationsSentAsync(leagueId, season);
        var newlySent = new List<(string UserId, int Week)>();
        var pushed = 0;

        foreach (var row in leaderboard)
        {
            foreach (var weekResult in row.WeekResults)
            {
                // Never MissingPicks/MissingGameResults/Excluded — only a week whose fate is
                // actually sealed. Won and Lost are each reported exactly once, at the moment the
                // week becomes decided — not per individual game, so a user never gets both a
                // per-game "covered" ping AND a "you won the week" ping for the same deciding game.
                if (weekResult.WeekResult is not (WeekResult.Won or WeekResult.Lost)) continue;
                if (alreadySent.Contains((row.User.Id, weekResult.Week))) continue;
                // A Lost week caused by a pick that failed to score (bad data) must not be
                // permanently notified+deduped — unlike a leaderboard page render, a push can't be
                // corrected after the fact. Skip silently; once the data bug is fixed, the next
                // score-ingestion run re-evaluates this week from scratch and can still notify.
                if (weekResult.HadScoringError) continue;
                if (!isPushable(weekResult.Week)) {
                    newlySent.Add((row.User.Id, weekResult.Week));
                    continue;
                }

                var label = periodLabel(weekResult.Week);
                var payload = weekResult.WeekResult == WeekResult.Won
                    ? new PushPayload("IV League", $"{label}: You Won the Week in {leagueName}! 🏆", Url: "/leaderboard", Sport: sport)
                    : BuildLostPayload(sport, leagueName, label, weekResult.LosingTeams.Select(t => teamNames.GetValueOrDefault(t, t)).ToList());

                // /code-review: dedup rows used to be recorded in one batch after this whole
                // double loop — if DispatchAsync ever threw partway through (a transient push
                // failure, a DB blip), every user already successfully notified before that point
                // lost its dedup row (the batched write below was never reached), guaranteeing a
                // duplicate push to them on the next run. Mirrors NflScoresJob/CfbScoresJob's
                // identical "one bad X must not cost the others" fetch-loop pattern: catch
                // per-row, keep every success collected so far, and only the row that actually
                // failed is retried later (not silently dropped).
                try {
                    await dispatcher.DispatchAsync(row.User.Id, p => p.NotifyWeekResult, payload);
                    newlySent.Add((row.User.Id, weekResult.Week));
                    pushed++;
                } catch (Exception ex) {
                    Log.Error(ex, "WeekResultNotificationService: dispatch failed for user {UserId}, league {LeagueId}, week {Week}",
                        row.User.Id, leagueId, weekResult.Week);
                }
            }
        }

        if (newlySent.Count > 0)
            await leagueRepository.RecordWeekResultNotificationsSentAsync(newlySent, leagueId, season);

        return pushed;
    }

    private static PushPayload BuildLostPayload(LeagueType sport, string leagueName, string label, IReadOnlyList<string> losingTeams) =>
        losingTeams.Count > 0
            ? new PushPayload("IV League", $"{label}: You Lost {losingTeams[0]} — You Lost the Week in {leagueName}! 💀", Url: "/leaderboard", Sport: sport)
            : new PushPayload("IV League", $"{label}: You Lost the Week in {leagueName}! 💀", Url: "/leaderboard", Sport: sport);
}
