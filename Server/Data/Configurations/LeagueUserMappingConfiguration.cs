using FourPlayWebApp.Server.Models.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FourPlayWebApp.Server.Data.Configurations;

public class LeagueUserMappingConfiguration : IEntityTypeConfiguration<LeagueUserMapping>
{
    public void Configure(EntityTypeBuilder<LeagueUserMapping> entity)
    {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.DateCreated)
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        entity.HasOne(e => e.League)
            .WithMany(l => l.LeagueUsers)
            .HasForeignKey(e => e.LeagueId)
            .IsRequired();

        entity.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .IsRequired();

        entity.HasIndex(x => new { x.LeagueId, x.UserId }).IsUnique();
    }
}
