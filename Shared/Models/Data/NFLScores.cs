using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace FourPlayWebApp.Shared.Models.Data;
[ExcludeFromCodeCoverage]
public class NflScores {
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    public int Season { get; set; }
    public int NflWeek { get; set; }
    public string HomeTeam { get; set; }
    public string AwayTeam { get; set; }
    public int HomeTeamScore { get; set; }
    public int AwayTeamScore { get; set; }
    public DateTime GameTime { get; set; }
    public DateTimeOffset DateCreated { get; set; } = DateTime.UtcNow;
}