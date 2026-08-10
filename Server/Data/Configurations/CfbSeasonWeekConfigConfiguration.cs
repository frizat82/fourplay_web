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

        // IvLeagueWeekNumber is the canonical week number used everywhere outside ESPN-fetch
        // context (CfbSlates.SlateNumber, trigger Identity strings, HasData lookups) — mirrors
        // NflSeasonWeekConfigs' (Season, WeekId) unique index. Filtered because 99 is a sentinel
        // for "excluded from the league" and multiple excluded ESPN weeks legitimately share it.
        entity.HasIndex(e => new { e.Season, e.IvLeagueWeekNumber })
            .IsUnique()
            .HasFilter("\"IvLeagueWeekNumber\" <> 99");
    }
}
