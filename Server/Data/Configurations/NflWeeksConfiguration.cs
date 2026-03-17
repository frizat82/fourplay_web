using FourPlayWebApp.Server.Models.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FourPlayWebApp.Server.Data.Configurations;

public class NflWeeksConfiguration : IEntityTypeConfiguration<NflWeeks>
{
    public void Configure(EntityTypeBuilder<NflWeeks> entity)
    {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.DateCreated)
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        entity.HasIndex(w => new { w.Season, w.NflWeek }).IsUnique();

        entity.HasMany(e => e.NflPicks)
            .WithOne(l => l.NflWeekInfo)
            .HasForeignKey(g => g.NflWeekId)
            .IsRequired();
    }
}
