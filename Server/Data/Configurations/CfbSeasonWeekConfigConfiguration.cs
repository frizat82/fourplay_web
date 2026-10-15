using FourPlayWebApp.Server.Models.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FourPlayWebApp.Server.Data.Configurations;

public class CfbSeasonWeekConfigConfiguration : IEntityTypeConfiguration<CfbSeasonWeekConfig>
{
    public void Configure(EntityTypeBuilder<CfbSeasonWeekConfig> entity)
    {
        entity.HasKey(e => e.Id);
        // frizat-b4g: shared enums, stored by name (same values as NflSeasonWeekConfigs).
        entity.Property(e => e.WeekType).HasConversion<string>();
        entity.Property(e => e.ScoringFormat).HasConversion<string>();

        // One row per week number per season — mirrors NflSeasonWeekConfigs' (Season, WeekId). A
        // skipped week has no number, so it's left out.
        entity.HasIndex(e => new { e.Season, e.IvLeagueWeekNumber })
            .IsUnique()
            .HasFilter("\"IvLeagueWeekNumber\" IS NOT NULL");

        entity.ToTable(t => {
            // InScopeIvLeague is the one skip flag: an in-scope week has a number, a skipped one doesn't.
            t.HasCheckConstraint("CK_CfbSeasonWeekConfigs_InScope", "\"InScopeIvLeague\" = (\"IvLeagueWeekNumber\" IS NOT NULL)");
            // frizat-d47: a real round asks for 1-4 picks; only a skipped week carries 0. A typo'd or
            // forgotten count fails the insert rather than silently capping a slate at 0.
            t.HasCheckConstraint("CK_CfbSeasonWeekConfigs_RequiredPicks",
                "(\"InScopeIvLeague\" AND \"RequiredPicks\" BETWEEN 1 AND 4) OR (NOT \"InScopeIvLeague\" AND \"RequiredPicks\" = 0)");
            t.HasCheckConstraint("CK_CfbSeasonWeekConfigs_ScoringFormat", WeekRuleChecks.ScoringFormatMatchesRequiredPicks);
            // Every screen and push names the round by this — never blank (frizat-d47).
            t.HasCheckConstraint("CK_CfbSeasonWeekConfigs_WeekLabel", "\"WeekLabel\" <> ''");
        });
    }
}
