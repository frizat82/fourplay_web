using FourPlayWebApp.Server.Models.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FourPlayWebApp.Server.Data.Configurations;

public class PushSubscriptionConfiguration : IEntityTypeConfiguration<PushSubscription>
{
    public void Configure(EntityTypeBuilder<PushSubscription> entity)
    {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.CreatedAt)
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
        entity.Property(e => e.LastSeenAt)
            .HasColumnType("timestamptz");

        entity.HasIndex(e => e.Endpoint).IsUnique();
        entity.HasIndex(e => e.UserId);
    }
}
