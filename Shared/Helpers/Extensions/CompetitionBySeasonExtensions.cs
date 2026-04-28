using FourPlayWebApp.Shared.Models;
using FourPlayWebApp.Shared.Models.Data;

namespace FourPlayWebApp.Shared.Helpers.Extensions;

public static class CompetitionBySeasonExtensions {
    public static List<NflScores> ParseCompetitionToNflScore(this IEnumerable<CompetitionBySeason> competitions, int week) {
        var scoreList = new List<NflScores>();
        foreach (var result in competitions) {
            if (result.Competition.Status.Type.Name != TypeName.StatusFinal) continue;
            var dbScore = new NflScores();
            var ht = result.Competition.Competitors.First(x => x.HomeAway == HomeAway.Home);
            var at = result.Competition.Competitors.First(x => x.HomeAway == HomeAway.Away);
            //dbScore.Id = Guid.NewGuid();
            dbScore.HomeTeam = ht.Team.Abbreviation;
            dbScore.AwayTeam = at.Team.Abbreviation;
            dbScore.HomeTeamScore = (int)ht.Score;
            dbScore.AwayTeamScore = (int)at.Score;
            dbScore.NflWeek = week;
            dbScore.Season = (int)result.Season.Year;
            dbScore.GameTime = result.Competition.Date.UtcDateTime;
            scoreList.Add(dbScore);
        }
        return scoreList;
    }
    public static NflSpreads? ParseCompetitionToNflSpreads(this CompetitionBySeason competition, int week) {
        if (GameHelpers.IsGameOver(competition.Competition)) return null;
        var dbScore = new NflSpreads();
        var ht = competition.Competition.Competitors.First(x => x.HomeAway == HomeAway.Home);
        var at = competition.Competition.Competitors.First(x => x.HomeAway == HomeAway.Away);
        dbScore.HomeTeam = ht.Team.Abbreviation;
        dbScore.AwayTeam = at.Team.Abbreviation;
        dbScore.NflWeek = week;
        dbScore.Season = (int)competition.Season.Year;
        dbScore.GameTime = competition.Competition.Date.UtcDateTime;
        return dbScore;
    }
    public static List<NflSpreads> ParseCompetitionToNflSpreads(this IEnumerable<CompetitionBySeason> competitions, int week) {
        return competitions.Select(x => x.ParseCompetitionToNflSpreads(week))
            .Where(x => x is not null)
            .ToList()!;
    }
}
