using FourPlayWebApp.Server.Models.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FourPlayWebApp.Server.Data.Configurations;

public class SpreadsPostedNotificationSentConfiguration : IEntityTypeConfiguration<SpreadsPostedNotificationSent>
{
    public void Configure(EntityTypeBuilder<SpreadsPostedNotificationSent> entity)
    {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.SentAt)
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        entity.HasIndex(e => new { e.Sport, e.Season, e.Week }).IsUnique();
    }
}
