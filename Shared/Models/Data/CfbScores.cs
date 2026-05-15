using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace FourPlayWebApp.Shared.Models.Data;

[ExcludeFromCodeCoverage]
public class CfbScores {
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    public int CfbSlateId { get; set; }
    public int EspnEventId { get; set; }
    public string HomeTeam { get; set; } = string.Empty;
    public string AwayTeam { get; set; } = string.Empty;
    public int HomeTeamScore { get; set; }
    public int AwayTeamScore { get; set; }
    public string GameStatus { get; set; } = string.Empty; // STATUS_FINAL, STATUS_IN_PROGRESS, STATUS_SCHEDULED
    public DateTimeOffset GameTime { get; set; }
    public DateTimeOffset DateCreated { get; set; } = DateTimeOffset.UtcNow;
}
