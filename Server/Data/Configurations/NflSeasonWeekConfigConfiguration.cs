using FourPlayWebApp.Server.Models.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FourPlayWebApp.Server.Data.Configurations;

public class NflSeasonWeekConfigConfiguration : IEntityTypeConfiguration<NflSeasonWeekConfig>
{
    public void Configure(EntityTypeBuilder<NflSeasonWeekConfig> entity)
    {
        // frizat-b4g: shared enums, stored by name (same values as CfbSeasonWeekConfigs).
        entity.Property(e => e.WeekType).HasConversion<string>();
        entity.Property(e => e.ScoringFormat).HasConversion<string>();
        entity.Property(e => e.InScopeIvLeague).HasDefaultValue(true);

        entity.ToTable(t => {
            // frizat-d47: a hand-written season row with a typo'd or forgotten pick count fails the
            // insert instead of silently changing a week's cap. Every NFL round asks for 1-4 picks —
            // 0 would cap every submission at nothing and score every member a win.
            t.HasCheckConstraint("CK_NflSeasonWeekConfigs_RequiredPicks", "\"RequiredPicks\" BETWEEN 1 AND 4");
            t.HasCheckConstraint("CK_NflSeasonWeekConfigs_ScoringFormat", WeekRuleChecks.ScoringFormatMatchesRequiredPicks);
        });
    }
}
