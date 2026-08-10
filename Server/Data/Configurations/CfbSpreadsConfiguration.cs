using FourPlayWebApp.Shared.Models.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FourPlayWebApp.Server.Data.Configurations;

public class CfbSpreadsConfiguration : IEntityTypeConfiguration<CfbSpreads>
{
    public void Configure(EntityTypeBuilder<CfbSpreads> entity)
    {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.DateCreated)
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        entity.HasIndex(e => e.EspnEventId).IsUnique();
        entity.HasIndex(e => e.CfbSlateId);
    }
}
