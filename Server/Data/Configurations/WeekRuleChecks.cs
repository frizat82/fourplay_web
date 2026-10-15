using FourPlayWebApp.Server.Models.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FourPlayWebApp.Server.Data.Configurations;

// frizat-b4g: the rules both control tables share, configured once. The enums are stored by
// name; a week's ScoringFormat always matches its pick count, and its postseason flag always
// matches its WeekType, so none of them can disagree.
internal static class WeekRuleChecks {
    public static void Configure<T>(EntityTypeBuilder<T> entity, string table) where T : class, IControlTableWeek {
        entity.Property(e => e.WeekType).HasConversion<string>();
        entity.Property(e => e.ScoringFormat).HasConversion<string>();
        entity.ToTable(t => {
            t.HasCheckConstraint($"CK_{table}_ScoringFormat",
                "(\"ScoringFormat\" = 'Standard' AND \"RequiredPicks\" = 4) OR " +
                "(\"ScoringFormat\" = 'Divisional' AND \"RequiredPicks\" = 3) OR " +
                "(\"ScoringFormat\" = 'Conference' AND \"RequiredPicks\" = 2) OR " +
                "(\"ScoringFormat\" = 'Final' AND \"RequiredPicks\" = 1) OR " +
                "(\"ScoringFormat\" = 'None' AND \"RequiredPicks\" = 0)");
            // The app's postseason (O/U offered, the Postseason selector) is conference
            // championships and the playoffs — for CFB, from Conf. Championships on.
            t.HasCheckConstraint($"CK_{table}_IsPostSeason", IsPostSeasonMatchesWeekType);
        });
    }

    public const string IsPostSeasonMatchesWeekType =
        "\"IsPostSeason\" = (\"WeekType\" IN ('ConferenceChampionships', 'Playoff'))";
}
