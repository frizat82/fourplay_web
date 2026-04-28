using System.Diagnostics.CodeAnalysis;

namespace FourPlayWebApp.Shared.Models.Dtos;
[ExcludeFromCodeCoverage]
public class LeaderboardSummaryDto
{
    public int LeagueId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public int Season { get; set; }
    public int TotalWins { get; set; }
    public int TotalLosses { get; set; }
    public decimal WinPercentage { get; set; }
    public decimal TotalJuice { get; set; }
}
