using FourPlayWebApp.Server.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FourPlayWebApp.Server.Data.Configurations;

public class InvitationConfiguration : IEntityTypeConfiguration<Invitation>
{
    public void Configure(EntityTypeBuilder<Invitation> entity)
    {
        // frizat-5rp: all 3 FKs off AspNetUsers/LeagueInfo default to EF's convention for optional
        // relationships (NO ACTION), the only tables in the schema not consistent with everything
        // else referencing AspNetUsers/LeagueInfo (LeagueInfo, LeagueJuiceMapping,
        // LeagueUserMapping, NflPicks, CfbPicks, RefreshTokens all CASCADE). That inconsistency
        // made deleting a user/league with any invitation history throw an unhandled FK violation.
        //
        // InvitedByUserId is SetNull rather than Cascade, unlike the other two: the invitation row
        // itself belongs to the RECIPIENT (and the league), not the inviter — deleting whoever sent
        // the invite shouldn't destroy another, unrelated, still-active user's registration audit
        // trail. RegisteredUserId/LeagueId cascading is safe because deleting either of THOSE really
        // does mean "this invitation no longer has anything to refer to."
        entity.HasOne(e => e.InvitedByUser)
            .WithMany()
            .HasForeignKey(e => e.InvitedByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        entity.HasOne(e => e.RegisteredUser)
            .WithMany()
            .HasForeignKey(e => e.RegisteredUserId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(e => e.League)
            .WithMany()
            .HasForeignKey(e => e.LeagueId)
            .OnDelete(DeleteBehavior.Cascade);

        // frizat-9vm follow-up: the [Index(Email, LeagueId)] on the model only enforces uniqueness
        // when LeagueId is non-null — Postgres treats every NULL as distinct, so two global
        // (LeagueId == null) invites to the same email wouldn't collide against that index. This
        // partial index closes that gap by enforcing Email uniqueness specifically among the
        // LeagueId-null rows, matching what CreateInvitationAsync's upsert already assumes.
        entity.HasIndex(e => e.Email)
            .IsUnique()
            .HasFilter("\"LeagueId\" IS NULL");
    }
}
