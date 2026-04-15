using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Shared.Models.Data;
using FourPlayWebApp.Shared.Models.Enum;


namespace FourPlayWebApp.Server.Services;
public class SpreadCalculator(List<NflSpreads> odds, LeagueJuiceMapping juiceMapping, int week) : ISpreadCalculator {

    public bool DoOddsExist() {
        return odds.Count > 0;
    }

    public double? GetOverUnder(string teamAbbr, PickType pickType) {
        //TODO: Add Caching
        var spread = GetOverUnderFromAbbreviation(teamAbbr);
        if (spread is null)
            return null;
        if (pickType == PickType.Spread)
            return null;
        if (pickType == PickType.Over)
            return spread - CalculateLeagueSpread();
        return spread + CalculateLeagueSpread();
    }

    public double? GetSpread(string teamAbbr) {
        //TODO: Add Caching
        var spread = GetSpreadFromAbbreviation(teamAbbr);
        if (spread is null)
            return null;
        return spread + CalculateLeagueSpread();
    }

    public double CalculateLeagueSpread() {
        if (juiceMapping is null)
            throw new NullReferenceException("League Spread not configured");
        return week switch {
            <= 18 => juiceMapping.Juice,
            < 21 => juiceMapping.JuiceDivisional,
            21 => juiceMapping.JuiceConference,
            _ => 0
        };
    }

    private bool DidUserWinSpread(string team, int pickTeamScore, int otherTeamScore) {
        var spread = GetSpread(team);
        if (spread is null) return false;
        return pickTeamScore + spread - otherTeamScore > 0;
    }

    public bool DidUserWinPick(string team, int pickTeamScore, int otherTeamScore, PickType pick = PickType.Spread) {
        if (!DoOddsExist()) return false;
        switch (pick) {
            case PickType.Spread: {
                return DidUserWinSpread(team, pickTeamScore, otherTeamScore);
            }
            case PickType.Over: {
                var overUnder = GetOverUnder(team, pick);
                if (overUnder is null) return false;
                return pickTeamScore + otherTeamScore > overUnder;
            }
            case PickType.Under: {
                var overUnder = GetOverUnder(team, pick);
                if (overUnder is null) return false;
                return pickTeamScore + otherTeamScore < overUnder;
            }
            default:
                return false;
        }
    }

    private double? GetSpreadFromAbbreviation(string teamAbbr) {
        var spread = odds.FirstOrDefault(x => x.HomeTeam == teamAbbr);
        if (spread is not null)
            return spread.HomeTeamSpread;
        spread = odds.FirstOrDefault(x => x.AwayTeam == teamAbbr);
        return spread?.AwayTeamSpread;
    }

    public double? GetOverUnderFromAbbreviation(string teamAbbr) {
        var spread = odds.FirstOrDefault(x => x.HomeTeam == teamAbbr);
        if (spread is not null)
            return spread.OverUnder;
        spread = odds.FirstOrDefault(x => x.AwayTeam == teamAbbr);
        return spread?.OverUnder;
    }
}
