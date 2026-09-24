namespace FourPlayWebApp.Shared.Models.Data;

/// <summary>
/// A final (or in-progress) game score, sport-agnostic — the score-side counterpart of
/// <see cref="IOddsRow"/>, so NFL and CFB scoring run through one implementation (WeekOutcome).
/// </summary>
public interface IScoreRow {
    string HomeTeam { get; }
    string AwayTeam { get; }
    int HomeTeamScore { get; }
    int AwayTeamScore { get; }
}
