using FourPlayWebApp.Server.Models.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FourPlayWebApp.Server.Data.Configurations;

public class CfbSlatesConfiguration : IEntityTypeConfiguration<CfbSlates>
{
    public void Configure(EntityTypeBuilder<CfbSlates> entity)
    {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.DateCreated)
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Mirrors NflWeeks' unique index — CfbSlateSeederJob already checks slate count before
        // reseeding, but this is the DB-level backstop against a duplicate slate row.
        entity.HasIndex(e => new { e.Season, e.SlateNumber }).IsUnique();
    }
}
