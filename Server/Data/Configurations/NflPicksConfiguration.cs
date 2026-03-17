using FourPlayWebApp.Server.Models.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FourPlayWebApp.Server.Data.Configurations;

public class NflPicksConfiguration : IEntityTypeConfiguration<NflPicks>
{
    public void Configure(EntityTypeBuilder<NflPicks> entity)
    {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.DateCreated)
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        entity.HasOne(e => e.League)
            .WithMany(l => l.NflPicks)
            .HasForeignKey(e => e.LeagueId)
            .IsRequired();

        entity.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .IsRequired();

        entity.HasOne(e => e.NflWeekInfo)
            .WithMany(l => l.NflPicks)
            .HasForeignKey(e => e.NflWeekId)
            .IsRequired();

        entity.HasIndex(x => new { x.UserId, x.LeagueId, x.NflWeek, x.Season, x.Team, x.Pick }).IsUnique();
    }
}
