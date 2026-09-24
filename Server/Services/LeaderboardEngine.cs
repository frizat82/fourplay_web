using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Shared.Helpers;
using FourPlayWebApp.Shared.Models;
using FourPlayWebApp.Shared.Models.Data;
using FourPlayWebApp.Shared.Models.Enum;

namespace FourPlayWebApp.Server.Services;

/// <summary>One NFL week or CFB slate as the leaderboard sees it: its number and its rows.</summary>
public sealed record LeaderboardPeriod(int Number, IReadOnlyCollection<IOddsRow> Spreads, IReadOnlyCollection<IScoreRow> Scores);

/// <summary>
/// Builds and settles a season leaderboard — one implementation for both sports. Each sport's
/// service only loads its rows (spreads, scores, picks) and hands them over as periods; which
/// tease, how many picks, StartWeek exclusion, how a week resolves and settlement are all here.
/// </summary>
public static class LeaderboardEngine {
    /// <param name="periods">Every period to show, in order (index i is WeekResults[i]).</param>
    /// <param name="picksFor">A user's picks for a period number.</param>
    /// <param name="now">
    /// Captured once for the whole run: a wall-clock tick past a kickoff mid-run must not give two
    /// users a different verdict for the same week.
    /// </param>
    public static List<LeaderboardModel> Build(LeagueType sport, IReadOnlyList<LeagueUserMapping> users,
        IReadOnlyList<LeaderboardPeriod> periods, Func<string, int, IEnumerable<PickRow>> picksFor,
        LeagueJuiceMapping mapping, DateTimeOffset now, Action<PickRow, int, Exception> onPickError) {
        // Each period's inputs are the same for every member, so they're built once per period, not
        // per user × period. frizat-o3x: a period before the league's StartWeek isn't evaluated at
        // all — Excluded, not a win, loss or pending state.
        var inputs = periods.Select(p => GameHelpers.IsWeekExcludedFromSeason(p.Number, mapping.StartWeek) ? null : new {
            p.Scores,
            Calculator = new SpreadCalculator(p.Spreads, JuiceTiers.For(sport, p.Number, mapping)),
            AllGamesStarted = GameHelpers.AllGamesStarted(p.Spreads.Select(s => s.GameTime), now),
            RequiredPicks = sport == LeagueType.Cfb ? GameHelpers.GetCfbRequiredPicks(p.Number) : GameHelpers.GetRequiredPicks(p.Number),
        }).ToList();

        var leaderboard = users.Select(user => new LeaderboardModel {
            User = user.User,
            WeekResults = periods.Select((period, i) => new LeaderboardWeekResults {
                Week = period.Number,
                WeekResult = inputs[i] is { } week
                    ? WeekOutcome.Evaluate(picksFor(user.UserId, period.Number).ToList(), week.Scores, week.Calculator,
                        week.RequiredPicks, week.AllGamesStarted, (pick, ex) => onPickError(pick, period.Number, ex))
                    : WeekResult.Excluded,
            }).ToArray(),
        }).ToList();

        return LeaderboardSettlementHelper.SettleWeeks(leaderboard, mapping.WeeklyCost, periods.Count, mapping.StartWeek);
    }
}
