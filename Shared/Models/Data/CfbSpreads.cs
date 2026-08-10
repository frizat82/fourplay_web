using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace FourPlayWebApp.Shared.Models.Data;

[ExcludeFromCodeCoverage]
public class CfbSpreads {
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    public int CfbSlateId { get; set; }
    public int EspnEventId { get; set; }
    public string HomeTeam { get; set; } = string.Empty;
    public string AwayTeam { get; set; } = string.Empty;
    public double HomeTeamSpread { get; set; }
    public double AwayTeamSpread { get; set; }
    public double OverUnder { get; set; }
    public DateTimeOffset GameTime { get; set; }
    public DateTimeOffset DateCreated { get; set; } = DateTimeOffset.UtcNow;

    // True when either team was ranked (or the game is CFP postseason) and it isn't a Tue/Wed
    // MAC-only game, computed once at ingestion from that week's CfbRanking rows. The full FBS
    // slate is always persisted regardless of this flag — it only gates what's served to users.
    public bool IsLeagueEligible { get; set; }
}
