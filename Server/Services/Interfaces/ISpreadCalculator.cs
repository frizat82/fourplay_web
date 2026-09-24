using FourPlayWebApp.Shared.Models.Enum;

namespace FourPlayWebApp.Server.Services.Interfaces;

public interface ISpreadCalculator {
    public bool DoOddsExist();
    /// <summary>Every team (home and away) with an odds row in this calculator's week.</summary>
    IReadOnlyList<string> GetTeams();
    double? GetOverUnder(string teamAbbr, PickType pickType);
    double? GetSpread(string teamAbbr);
    DateTimeOffset? GetDateCreated(string teamAbbr);
    bool DidUserWinPick(string team, int pickTeamScore, int otherTeamScore, PickType pick = PickType.Spread);
}
