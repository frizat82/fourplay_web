using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Shared.Models.Data;
using FourPlayWebApp.Shared.Models.Enum;


namespace FourPlayWebApp.Server.Services;

// Shared by NFL and CFB (SpreadCalculatorBuilder / CfbPicksController.GetSpreads): applies a
// league's configured tease ("juice") to the raw Vegas spread/total and determines pick outcomes.
// The two sports resolve that juice amount from different tier boundaries — NFL from week number
// (the constructor below), CFB from slate number (CfbLeaderboardService.JuiceForSlate) — but once
// resolved, the arithmetic is identical, so it lives in one place rather than two.
public class SpreadCalculator : ISpreadCalculator {
    private readonly IEnumerable<IOddsRow> odds;
    private readonly double juice;

    // NFL path: resolves juice from NFL's week-number tier boundaries.
    public SpreadCalculator(IEnumerable<IOddsRow> odds, LeagueJuiceMapping juiceMapping, int week)
        : this(odds, ResolveNflJuice(juiceMapping, week)) {
    }

    // Shared path: caller supplies an already-resolved tease amount (e.g. CFB's slate-based tiers).
    public SpreadCalculator(IEnumerable<IOddsRow> odds, double juice) {
        this.odds = odds;
        this.juice = juice;
    }

    private static double ResolveNflJuice(LeagueJuiceMapping juiceMapping, int week) {
        if (juiceMapping is null)
            throw new NullReferenceException("League Spread not configured");
        return week switch {
            <= 18 => juiceMapping.Juice,
            < 21 => juiceMapping.JuiceDivisional,
            21 => juiceMapping.JuiceConference,
            _ => 0
        };
    }

    public bool DoOddsExist() {
        return odds.Any();
    }

    public double? GetOverUnder(string teamAbbr, PickType pickType) {
        //TODO: Add Caching
        var spread = GetOverUnderFromAbbreviation(teamAbbr);
        if (spread is null)
            return null;
        if (pickType == PickType.Spread)
            return null;
        if (pickType == PickType.Over)
            return spread - juice;
        return spread + juice;
    }

    public double? GetSpread(string teamAbbr) {
        //TODO: Add Caching
        var spread = GetSpreadFromAbbreviation(teamAbbr);
        if (spread is null)
            return null;
        return spread + juice;
    }

    public DateTimeOffset? GetDateCreated(string teamAbbr) {
        var spread = odds.FirstOrDefault(x => x.HomeTeam == teamAbbr || x.AwayTeam == teamAbbr);
        return spread?.DateCreated;
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
