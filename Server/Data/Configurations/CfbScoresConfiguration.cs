using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Shared.Models;
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

        // Store as the enum's string name (e.g. "StatusFinal"), not its int ordinal — this is the
        // exact literal value CfbScoresJob's status.ToString() was already writing to this
        // previously-string column, so existing rows keep parsing correctly with zero data
        // migration needed. Tolerant (not HasConversion<string>() directly) so a malformed/legacy
        // row logs a warning and falls back instead of throwing and failing the whole query.
        entity.Property(e => e.GameStatus).HasConversion(TolerantEnumConverter.Create(TypeName.StatusScheduled));

        // Natural key mirroring NflScoresConfiguration's (Season, NflWeek, HomeTeam) — see
        // CfbSpreadsConfiguration's comment for the full rationale. Backstops
        // UpsertCfbScoresAsync's app-level upsert-by-(CfbSlateId, HomeTeam).
        entity.HasIndex(e => new { e.CfbSlateId, e.HomeTeam }).IsUnique();

        // CfbSlateId was previously an unenforced "soft FK" — see CfbSpreadsConfiguration for the
        // full rationale (frizat-896 schema audit). Restrict protects real final-score history from
        // being silently orphaned by a bulk slate delete.
        entity.HasOne<CfbSlates>()
            .WithMany()
            .HasForeignKey(e => e.CfbSlateId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
