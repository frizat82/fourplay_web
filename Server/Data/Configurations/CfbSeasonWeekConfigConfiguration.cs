using FourPlayWebApp.Server.Models.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FourPlayWebApp.Server.Data.Configurations;

public class CfbSeasonWeekConfigConfiguration : IEntityTypeConfiguration<CfbSeasonWeekConfig>
{
    public void Configure(EntityTypeBuilder<CfbSeasonWeekConfig> entity)
    {
        entity.HasKey(e => e.Id);
        entity.HasIndex(e => new { e.Season, e.EspnWeekNumber }).IsUnique();
    }
}
