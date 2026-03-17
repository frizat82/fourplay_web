using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Shared.Models.Enum;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FourPlayWebApp.Server.Data.Configurations;

public class LeagueInfoConfiguration : IEntityTypeConfiguration<LeagueInfo>
{
    public void Configure(EntityTypeBuilder<LeagueInfo> entity)
    {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.DateCreated)
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
        entity.Property(e => e.LeagueType)
            .HasDefaultValue(LeagueType.Nfl);
        entity.HasIndex(e => e.LeagueName).IsUnique();

        entity.HasMany(e => e.LeagueUsers)
            .WithOne(e => e.League)
            .HasForeignKey(e => e.LeagueId)
            .IsRequired();

        entity.HasMany(e => e.NflPicks)
            .WithOne(e => e.League)
            .HasForeignKey(e => e.LeagueId)
            .IsRequired();

        entity.HasMany(e => e.LeagueJuiceMappings)
            .WithOne(e => e.League)
            .HasForeignKey(e => e.LeagueId)
            .IsRequired();

        entity.HasOne(e => e.Owner)
            .WithMany()
            .HasForeignKey(e => e.OwnerUserId)
            .IsRequired();
    }
}
