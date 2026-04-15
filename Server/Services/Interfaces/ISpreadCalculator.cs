using FourPlayWebApp.Shared.Models.Enum;

namespace FourPlayWebApp.Server.Services.Interfaces;

public interface ISpreadCalculator {
    public bool DoOddsExist();
    double? GetOverUnder(string teamAbbr, PickType pickType);
    double? GetSpread(string teamAbbr);
    bool DidUserWinPick(string team, int pickTeamScore, int otherTeamScore, PickType pick = PickType.Spread);
}
