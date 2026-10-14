using FourPlayWebApp.Server.Models.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FourPlayWebApp.Server.Data.Configurations;

public class NflSeasonWeekConfigConfiguration : IEntityTypeConfiguration<NflSeasonWeekConfig>
{
    // frizat-d47: a hand-written season row with a typo'd or forgotten pick count fails the insert
    // instead of silently changing a week's cap. Every NFL round asks for 1-4 picks — 0 would cap
    // every submission at nothing and score every member a win.
    public void Configure(EntityTypeBuilder<NflSeasonWeekConfig> entity) =>
        entity.ToTable(t => t.HasCheckConstraint("CK_NflSeasonWeekConfigs_RequiredPicks", "\"RequiredPicks\" BETWEEN 1 AND 4"));
}
