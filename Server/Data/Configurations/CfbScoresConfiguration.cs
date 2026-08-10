using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Shared.Models.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FourPlayWebApp.Server.Data.Configurations;

public class CfbScoresConfiguration : IEntityTypeConfiguration<CfbScores>
{
    public void Configure(EntityTypeBuilder<CfbScores> entity)
    {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.DateCreated)
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Mirrors NflScores' unique index — backstops UpsertCfbScoresAsync's app-level
        // upsert-by-EspnEventId the same way CfbSpreads' index backstops its upsert.
        entity.HasIndex(e => e.EspnEventId).IsUnique();
        entity.HasIndex(e => e.CfbSlateId);

        // CfbSlateId was previously an unenforced "soft FK" — see CfbSpreadsConfiguration for the
        // full rationale (frizat-896 schema audit). Restrict protects real final-score history from
        // being silently orphaned by a bulk slate delete.
        entity.HasOne<CfbSlates>()
            .WithMany()
            .HasForeignKey(e => e.CfbSlateId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
