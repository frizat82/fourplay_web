using FourPlayWebApp.Server.Models.Identity;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FourPlayWebApp.Server.Models.Data;

[Index(nameof(Token), IsUnique = true)]
public class LeagueInviteLink
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string Token { get; set; } = Guid.NewGuid().ToString("N");

    public int LeagueId { get; set; }

    [ForeignKey("LeagueId")]
    public LeagueInfo League { get; set; } = null!;

    [Required]
    public string CreatedByUserId { get; set; } = null!;

    [ForeignKey("CreatedByUserId")]
    public ApplicationUser CreatedByUser { get; set; } = null!;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset ExpiresAt { get; set; } = DateTimeOffset.UtcNow.AddHours(24);

    public bool IsRevoked { get; set; }

    [NotMapped]
    public bool IsExpired => ExpiresAt < DateTimeOffset.UtcNow;

    [NotMapped]
    public bool IsValid => !IsRevoked && !IsExpired;
}
