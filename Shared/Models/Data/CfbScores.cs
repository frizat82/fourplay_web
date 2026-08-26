using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
using FourPlayWebApp.Shared.Models;

namespace FourPlayWebApp.Shared.Models.Data;

[ExcludeFromCodeCoverage]
public class CfbScores {
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    public int CfbSlateId { get; set; }
    public string HomeTeam { get; set; } = string.Empty;
    public string AwayTeam { get; set; } = string.Empty;
    public int HomeTeamScore { get; set; }
    public int AwayTeamScore { get; set; }
    // Reuses the same TypeName enum ESPN status parsing already uses everywhere else — only
    // StatusFinal is ever actually persisted here (CfbScoresJob only ever writes final games),
    // but the column preserves whatever the real ESPN status was rather than assuming.
    public TypeName GameStatus { get; set; } = TypeName.StatusScheduled;
    public DateTimeOffset GameTime { get; set; }
    public string? WeatherDisplayValue { get; set; }
    public string? WeatherConditionId { get; set; }
    public int? WeatherTemperatureF { get; set; }
    public DateTimeOffset DateCreated { get; set; } = DateTimeOffset.UtcNow;
}
