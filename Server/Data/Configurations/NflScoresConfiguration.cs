using FourPlayWebApp.Shared.Models.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FourPlayWebApp.Server.Data.Configurations;

public class NflScoresConfiguration : IEntityTypeConfiguration<NflScores>
{
    public void Configure(EntityTypeBuilder<NflScores> entity)
    {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.DateCreated)
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        entity.HasIndex(x => new { x.Season, NFLWeek = x.NflWeek, x.HomeTeam }).IsUnique();
    }
}
