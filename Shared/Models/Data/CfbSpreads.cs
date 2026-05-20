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
}
