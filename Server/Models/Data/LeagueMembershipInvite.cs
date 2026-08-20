using FourPlayWebApp.Server.Models.Identity;
using FourPlayWebApp.Shared.Models.Enum;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FourPlayWebApp.Server.Models.Data;

// A commissioner inviting an ALREADY-REGISTERED user to their league. Distinct from
// Invitation (new-user registration flow — registering IS joining, one step) and
// LeagueInviteLink (shareable, non-personalized link) — this is personalized, in-app only,
// and requires the invitee to explicitly accept or decline. No token: acceptance is always
// by the already-authenticated invitee, never via an emailed link.
public class LeagueMembershipInvite
{
    [Key]
    public int Id { get; set; }

    public int LeagueId { get; set; }

    [ForeignKey("LeagueId")]
    public LeagueInfo League { get; set; } = null!;

    [Required]
    public string InvitedUserId { get; set; } = null!;

    [ForeignKey("InvitedUserId")]
    public ApplicationUser InvitedUser { get; set; } = null!;

    public string? InvitedByUserId { get; set; }

    [ForeignKey("InvitedByUserId")]
    public ApplicationUser? InvitedByUser { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public MembershipInviteStatus Status { get; set; } = MembershipInviteStatus.Pending;

    public DateTimeOffset? RespondedAt { get; set; }
}
