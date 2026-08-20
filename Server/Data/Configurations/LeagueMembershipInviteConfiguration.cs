using FourPlayWebApp.Server.Models.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FourPlayWebApp.Server.Data.Configurations;

public class LeagueMembershipInviteConfiguration : IEntityTypeConfiguration<LeagueMembershipInvite>
{
    public void Configure(EntityTypeBuilder<LeagueMembershipInvite> entity)
    {
        // Mirrors InvitationConfiguration's reasoning: the row belongs to the INVITEE (and the
        // league) — deleting either really does mean "this invite no longer refers to anything."
        // Deleting the inviter shouldn't destroy the invitee's still-active pending invite.
        entity.HasOne(e => e.InvitedUser)
            .WithMany()
            .HasForeignKey(e => e.InvitedUserId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(e => e.InvitedByUser)
            .WithMany()
            .HasForeignKey(e => e.InvitedByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        entity.HasOne(e => e.League)
            .WithMany()
            .HasForeignKey(e => e.LeagueId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasIndex(e => new { e.LeagueId, e.InvitedUserId }).IsUnique();
    }
}
