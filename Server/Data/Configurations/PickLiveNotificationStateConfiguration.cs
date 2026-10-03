using FourPlayWebApp.Server.Models.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FourPlayWebApp.Server.Data.Configurations;

public class PickLiveNotificationStateConfiguration : IEntityTypeConfiguration<PickLiveNotificationState>
{
    public void Configure(EntityTypeBuilder<PickLiveNotificationState> entity)
    {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.LastNotifiedAt).HasColumnType("timestamptz");
        entity.Property(e => e.FinalNotifiedAt).HasColumnType("timestamptz");
        entity.Property(e => e.UpdatedAt)
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        entity.HasIndex(e => new { e.Sport, e.PickId }).IsUnique();
        entity.HasIndex(e => e.LeagueId);
    }
}
