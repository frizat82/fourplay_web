using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Shared.Models.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FourPlayWebApp.Server.Data.Configurations;

public class CfbSpreadsConfiguration : IEntityTypeConfiguration<CfbSpreads>
{
    public void Configure(EntityTypeBuilder<CfbSpreads> entity)
    {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.DateCreated)
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Natural key mirroring NflSpreadsConfiguration's (Season, NflWeek, HomeTeam) — a team
        // plays at most one game per slate, so (CfbSlateId, HomeTeam) uniquely identifies a game
        // without depending on an ESPN-specific id. CfbSlateId already encodes season via
        // CfbSlates.Season, so a separate Season column isn't needed in this index.
        entity.HasIndex(e => new { e.CfbSlateId, e.HomeTeam }).IsUnique();

        // CfbSlateId was previously an unenforced "soft FK" (indexed, joined against in queries,
        // but no DB-level constraint) — frizat-896 schema audit. Restrict (not Cascade): CfbSlates
        // rows are deleted in bulk by CfbSlateSeederJob's stale-slate cleanup with no check for
        // whether real spread data already exists for them; Restrict turns a would-be silent
        // orphan into a loud failure instead of quietly detaching real odds data from its slate.
        entity.HasOne<CfbSlates>()
            .WithMany()
            .HasForeignKey(e => e.CfbSlateId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
