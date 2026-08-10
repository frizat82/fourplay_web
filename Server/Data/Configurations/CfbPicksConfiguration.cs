using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Models.Identity;
using FourPlayWebApp.Shared.Models.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FourPlayWebApp.Server.Data.Configurations;

public class CfbPicksConfiguration : IEntityTypeConfiguration<CfbPicks>
{
    public void Configure(EntityTypeBuilder<CfbPicks> entity)
    {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.DateCreated)
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Mirrors NflPicks' unique index — CfbPicksController already checks for an existing pick
        // before inserting, but that's a read-then-write race (two concurrent submits can both
        // pass the check); this is the DB-level backstop against an actual duplicate pick.
        entity.HasIndex(e => new { e.UserId, e.LeagueId, e.CfbSlateId, e.Season, e.Team, e.PickType }).IsUnique();
        entity.HasIndex(e => e.LeagueId);
        entity.HasIndex(e => e.CfbSlateId);

        // UserId/LeagueId/CfbSlateId were previously unenforced "soft FKs" — no DB-level
        // referential integrity at all, unlike NflPicks (frizat-896 schema audit). See
        // CfbSpreadsConfiguration for the full CfbSlateId/Restrict rationale. UserId/LeagueId are
        // the one delta from that: they mirror NflPicks' Cascade exactly, since deleting a user or
        // a league is meant to take their picks with them.
        entity.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne<LeagueInfo>()
            .WithMany()
            .HasForeignKey(e => e.LeagueId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne<CfbSlates>()
            .WithMany()
            .HasForeignKey(e => e.CfbSlateId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
