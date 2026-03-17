using System.Diagnostics.CodeAnalysis;

namespace FourPlayWebApp.Shared.Models.Dtos;
[ExcludeFromCodeCoverage]
public record LeaderboardDto {
    public string UserId { get; set; }
    public string UserName { get; set; }
    public string Rank { get; set; }
    public long Total { get; set; }
    public LeaderboardWeekResults[] WeekResults { get; set; }
}