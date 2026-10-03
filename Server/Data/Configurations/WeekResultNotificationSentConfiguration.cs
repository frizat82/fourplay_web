using FourPlayWebApp.Server.Models.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FourPlayWebApp.Server.Data.Configurations;

public class WeekResultNotificationSentConfiguration : IEntityTypeConfiguration<WeekResultNotificationSent>
{
    public void Configure(EntityTypeBuilder<WeekResultNotificationSent> entity)
    {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.SentAt)
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        entity.HasIndex(e => new { e.UserId, e.LeagueId, e.Season, e.Week }).IsUnique();
    }
}
