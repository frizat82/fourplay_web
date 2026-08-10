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
    }
}
