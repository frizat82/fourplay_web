using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Server.Services.Repositories.Interfaces;
using FourPlayWebApp.Shared.Models.Enum;
using Serilog;

namespace FourPlayWebApp.Server.Services;

public class WeekResultNotificationService(
    ILeagueRepository leagueRepository,
    ILeaderboardService nflLeaderboardService,
    ICfbLeaderboardService cfbLeaderboardService,
    INotificationDispatcher dispatcher) : IWeekResultNotificationService
{
    public Task<int> CheckNflWeekResultsAsync(int season) =>
        CheckWeekResultsAsync(LeagueType.Nfl, season, "Week", league => nflLeaderboardService.BuildLeaderboard(league.Id, season));

    public Task<int> CheckCfbWeekResultsAsync(int season) =>
        CheckWeekResultsAsync(LeagueType.Cfb, season, "Slate", league => cfbLeaderboardService.BuildLeaderboard(league.Id, season));

    private async Task<int> CheckWeekResultsAsync(LeagueType sport, int season, string periodLabel,
        Func<LeagueInfo, Task<List<LeaderboardModel>>> buildLeaderboard)
    {
        var leagues = await leagueRepository.GetLeaguesByTypeAsync(sport);
        var sentCount = 0;
        foreach (var league in leagues)
        {
            var leaderboard = await buildLeaderboard(league);
            sentCount += await NotifyDecidedWeeksAsync(league.Id, league.LeagueName, season, periodLabel, leaderboard);
        }
        return sentCount;
    }

    private async Task<int> NotifyDecidedWeeksAsync(int leagueId, string leagueName, int season, string periodLabel,
        List<LeaderboardModel> leaderboard)
    {
        if (leaderboard.Count == 0) return 0;

        // One query for every already-sent (user, week) pair in this league/season, checked in
        // memory per row below — not one query per user per week. Generic "already notified"
        // regardless of outcome: a week's result, once decided (Won or Lost), is final and can
        // never flip, so one dedup flag per (user, week) covers both cases with no schema change.
        var alreadySent = await leagueRepository.GetWeekResultNotificationsSentAsync(leagueId, season);
        var newlySent = new List<(string UserId, int Week)>();

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

                var payload = weekResult.WeekResult == WeekResult.Won
                    ? new PushPayload("IV League", $"{periodLabel} {weekResult.Week}: You Won the Week in {leagueName}! 🏆")
                    : BuildLostPayload(leagueName, periodLabel, weekResult.Week, weekResult.LosingTeams);

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
                } catch (Exception ex) {
                    Log.Error(ex, "WeekResultNotificationService: dispatch failed for user {UserId}, league {LeagueId}, week {Week}",
                        row.User.Id, leagueId, weekResult.Week);
                }
            }
        }

        if (newlySent.Count > 0)
            await leagueRepository.RecordWeekResultNotificationsSentAsync(newlySent, leagueId, season);

        return newlySent.Count;
    }

    private static PushPayload BuildLostPayload(string leagueName, string periodLabel, int week, IReadOnlyList<string> losingTeams) =>
        losingTeams.Count > 0
            ? new PushPayload("IV League", $"{periodLabel} {week}: You Lost {losingTeams[0]} — You Lost the Week in {leagueName}! 💀")
            : new PushPayload("IV League", $"{periodLabel} {week}: You Lost the Week in {leagueName}! 💀");
}
