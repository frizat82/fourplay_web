

namespace FourPlayWebApp.Shared.Models.Data.Dtos;

public class InvitationDto
{
    public int Id { get; set; }
    public string InvitationCode { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? InvitedByUserId { get; set; }
    public string? InvitedByUserName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool IsUsed { get; set; }
    public DateTime? UsedAt { get; set; }
    public string? RegisteredUserId { get; set; }
    public string? RegisteredUserName { get; set; }
    public bool IsExpired { get; set; }
    public bool IsValid { get; set; }
    public int? LeagueId { get; set; }
    public string? LeagueName { get; set; }
}