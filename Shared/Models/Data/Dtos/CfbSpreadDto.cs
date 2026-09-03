namespace FourPlayWebApp.Shared.Models.Data.Dtos;

public class CfbSpreadDto {
    public int            Id             { get; set; }
    public int            CfbSlateId     { get; set; }
    public string         HomeTeam       { get; set; } = string.Empty;
    public string         AwayTeam       { get; set; } = string.Empty;
    public double         HomeTeamSpread { get; set; }
    public double         AwayTeamSpread { get; set; }
    public double         OverUnder      { get; set; }
    public DateTimeOffset GameTime       { get; set; }
    public DateTimeOffset DateCreated    { get; set; }
    // AP Top 25 rank (1-25), null when unranked — see CfbSpreads.HomeTeamRank/AwayTeamRank.
    public int?           HomeTeamRank   { get; set; }
    public int?           AwayTeamRank   { get; set; }
}
