using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Models.Identity;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FourPlayWebApp.Server.Models;
[Index(nameof(Email), IsUnique = true)]
public class Invitation
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string InvitationCode { get; set; } = Guid.NewGuid().ToString("N");

    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    public string? InvitedByUserId { get; set; }

    [ForeignKey("InvitedByUserId")]
    public ApplicationUser? InvitedByUser { get; set; }

    public int? LeagueId { get; set; }

    [ForeignKey("LeagueId")]
    public LeagueInfo? League { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ExpiresAt { get; set; } = DateTime.UtcNow.AddDays(7);

    public bool IsUsed { get; set; } = false;

    public DateTime? UsedAt { get; set; }

    public string? RegisteredUserId { get; set; }

    [ForeignKey("RegisteredUserId")]
    public ApplicationUser? RegisteredUser { get; set; }

    [NotMapped]
    public bool IsExpired => ExpiresAt.HasValue && ExpiresAt.Value < DateTime.UtcNow;

    [NotMapped]
    public bool IsValid => !IsUsed && !IsExpired;
}
