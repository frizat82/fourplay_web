using FourPlayWebApp.Server.Models.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FourPlayWebApp.Server.Data.Configurations;

public class LeagueJuiceMappingConfiguration : IEntityTypeConfiguration<LeagueJuiceMapping>
{
    public void Configure(EntityTypeBuilder<LeagueJuiceMapping> entity)
    {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.DateCreated)
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // SQL defaults match C# property defaults in LeagueJuiceMapping
        entity.Property(e => e.WeeklyCost).HasDefaultValue(5);
        entity.Property(e => e.Juice).HasDefaultValue(13);
        entity.Property(e => e.JuiceConference).HasDefaultValue(6);
        entity.Property(e => e.JuiceDivisional).HasDefaultValue(10);

        entity.HasOne(e => e.League)
            .WithMany(l => l.LeagueJuiceMappings)
            .HasForeignKey(e => e.LeagueId)
            .IsRequired();

        entity.HasIndex(x => new { x.LeagueId, x.Season }).IsUnique();
    }
}
