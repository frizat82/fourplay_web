using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace FourPlayWebApp.Shared.Models.Data;
[ExcludeFromCodeCoverage]
public class NflSpreads {
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    public int Season { get; set; }
    public int NflWeek { get; set; }
    public string HomeTeam { get; set; }
    public string AwayTeam { get; set; }
    public double HomeTeamSpread { get; set; }
    public double AwayTeamSpread { get; set; }
    public double OverUnder { get; set; }
    public DateTimeOffset GameTime { get; set; }
    public DateTimeOffset DateCreated { get; set; } = DateTimeOffset.UtcNow;
}