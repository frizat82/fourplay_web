using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Shared.Models.Data;
using FourPlayWebApp.Shared.Models.Enum;


namespace FourPlayWebApp.Server.Services;

// Shared by NFL and CFB: applies a league's tease ("juice") to the raw Vegas spread/total and
// decides pick outcomes. Which tease applies to a given week/slate is JuiceTiers' job — the
// caller passes the resolved amount, so there is no sport-specific path in here at all.
public class SpreadCalculator : ISpreadCalculator {
    private readonly double juice;
    // Team -> (its odds row, whether it's the home side). Built once; every lookup below used to be
    // a linear FirstOrDefault scan over the week's rows (twice — home, then away — per call).
    private readonly Dictionary<string, (IOddsRow Row, bool IsHome)> byTeam = new();

    public SpreadCalculator(IEnumerable<IOddsRow> odds, double juice) {
        var rows = odds.ToList();
        this.juice = juice;
        // Home sides first: the scans this replaces checked every row's HomeTeam before any AwayTeam,
        // so a team listed in two rows resolves to its home row, exactly as before. A null team (bad
        // row) is skipped rather than thrown on.
        foreach (var row in rows) if (row.HomeTeam is not null) byTeam.TryAdd(row.HomeTeam, (row, true));
        foreach (var row in rows) if (row.AwayTeam is not null) byTeam.TryAdd(row.AwayTeam, (row, false));
    }

    public bool DoOddsExist() => byTeam.Count > 0;

    public IReadOnlyList<string> GetTeams() => [.. byTeam.Keys];

    private (IOddsRow Row, bool IsHome)? Find(string teamAbbr) =>
        teamAbbr is not null && byTeam.TryGetValue(teamAbbr, out var t) ? t : null;

    public double? GetOverUnder(string teamAbbr, PickType pickType) {
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
        var spread = GetSpreadFromAbbreviation(teamAbbr);
        if (spread is null)
            return null;
        return spread + juice;
    }

    public DateTimeOffset? GetDateCreated(string teamAbbr) => Find(teamAbbr)?.Row.DateCreated;

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

    private double? GetSpreadFromAbbreviation(string teamAbbr) =>
        Find(teamAbbr) is { } t ? (t.IsHome ? t.Row.HomeTeamSpread : t.Row.AwayTeamSpread) : null;

    public double? GetOverUnderFromAbbreviation(string teamAbbr) => Find(teamAbbr)?.Row.OverUnder;
}
