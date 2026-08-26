using FourPlayWebApp.Server.Models.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FourPlayWebApp.Server.Data.Configurations;

public class LeagueJuiceReminderSentConfiguration : IEntityTypeConfiguration<LeagueJuiceReminderSent>
{
    public void Configure(EntityTypeBuilder<LeagueJuiceReminderSent> entity)
    {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.SentAt)
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        entity.HasIndex(e => new { e.LeagueId, e.Season }).IsUnique();
    }
}
