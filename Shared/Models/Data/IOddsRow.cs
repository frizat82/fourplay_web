namespace FourPlayWebApp.Shared.Models.Data;

// Common shape NflSpreads and CfbSpreads both already have — lets SpreadCalculator (Server/Services)
// compute juice-adjusted spreads/totals and pick outcomes once, shared by both sports, instead of
// each sport carrying its own copy of that arithmetic.
public interface IOddsRow {
    string HomeTeam { get; }
    string AwayTeam { get; }
    double HomeTeamSpread { get; }
    double AwayTeamSpread { get; }
    double OverUnder { get; }
    DateTimeOffset DateCreated { get; }
}
