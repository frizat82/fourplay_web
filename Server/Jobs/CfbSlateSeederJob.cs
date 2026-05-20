using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Services.Repositories.Interfaces;
using Quartz;
using Serilog;

namespace FourPlayWebApp.Server.Jobs;

[DisallowConcurrentExecution]
public class CfbSlateSeederJob(ICfbRepository repo) : IJob {
    private const int Season = 2025;
    private const int ExpectedSlateCount = 19;

    // Full 2025 CFB season: weeks 1-14 (regular), conf championships, 4 CFP rounds
    // SlateNumber mirrors week number; SlateType drives which ESPN endpoint the jobs use.
    private static readonly CfbSlates[] Slates2025 =
    [
        // ── Regular season weeks ──────────────────────────────────────────────
        new() { Season = Season, SlateNumber =  1, Label = "Week 1",  SlateType = "RegularSeason",          StartDate = new DateOnly(2025,  8, 23), EndDate = new DateOnly(2025,  8, 30) },
        new() { Season = Season, SlateNumber =  2, Label = "Week 2",  SlateType = "RegularSeason",          StartDate = new DateOnly(2025,  8, 30), EndDate = new DateOnly(2025,  9,  6) },
        new() { Season = Season, SlateNumber =  3, Label = "Week 3",  SlateType = "RegularSeason",          StartDate = new DateOnly(2025,  9,  6), EndDate = new DateOnly(2025,  9, 13) },
        new() { Season = Season, SlateNumber =  4, Label = "Week 4",  SlateType = "RegularSeason",          StartDate = new DateOnly(2025,  9, 13), EndDate = new DateOnly(2025,  9, 20) },
        new() { Season = Season, SlateNumber =  5, Label = "Week 5",  SlateType = "RegularSeason",          StartDate = new DateOnly(2025,  9, 20), EndDate = new DateOnly(2025,  9, 27) },
        new() { Season = Season, SlateNumber =  6, Label = "Week 6",  SlateType = "RegularSeason",          StartDate = new DateOnly(2025,  9, 27), EndDate = new DateOnly(2025, 10,  4) },
        new() { Season = Season, SlateNumber =  7, Label = "Week 7",  SlateType = "RegularSeason",          StartDate = new DateOnly(2025, 10,  4), EndDate = new DateOnly(2025, 10, 11) },
        new() { Season = Season, SlateNumber =  8, Label = "Week 8",  SlateType = "RegularSeason",          StartDate = new DateOnly(2025, 10, 11), EndDate = new DateOnly(2025, 10, 18) },
        new() { Season = Season, SlateNumber =  9, Label = "Week 9",  SlateType = "RegularSeason",          StartDate = new DateOnly(2025, 10, 18), EndDate = new DateOnly(2025, 10, 25) },
        new() { Season = Season, SlateNumber = 10, Label = "Week 10", SlateType = "RegularSeason",          StartDate = new DateOnly(2025, 10, 25), EndDate = new DateOnly(2025, 11,  1) },
        new() { Season = Season, SlateNumber = 11, Label = "Week 11", SlateType = "RegularSeason",          StartDate = new DateOnly(2025, 11,  1), EndDate = new DateOnly(2025, 11,  8) },
        new() { Season = Season, SlateNumber = 12, Label = "Week 12", SlateType = "RegularSeason",          StartDate = new DateOnly(2025, 11,  8), EndDate = new DateOnly(2025, 11, 15) },
        new() { Season = Season, SlateNumber = 13, Label = "Week 13", SlateType = "RegularSeason",          StartDate = new DateOnly(2025, 11, 15), EndDate = new DateOnly(2025, 11, 22) },
        new() { Season = Season, SlateNumber = 14, Label = "Week 14", SlateType = "RegularSeason",          StartDate = new DateOnly(2025, 11, 22), EndDate = new DateOnly(2025, 11, 29) },
        // ── Conference Championship Week ──────────────────────────────────────
        new() { Season = Season, SlateNumber = 15, Label = "Conf. Championships", SlateType = "ConferenceChampionship", StartDate = new DateOnly(2025, 12, 5), EndDate = new DateOnly(2025, 12, 7) },
        // ── CFP Postseason ────────────────────────────────────────────────────
        new() { Season = Season, SlateNumber = 16, Label = "CFP First Round",          SlateType = "FirstRound",   StartDate = new DateOnly(2025, 12, 19), EndDate = new DateOnly(2025, 12, 20) },
        new() { Season = Season, SlateNumber = 17, Label = "CFP Quarterfinals",        SlateType = "Quarterfinal", StartDate = new DateOnly(2025, 12, 31), EndDate = new DateOnly(2026,  1,  1) },
        new() { Season = Season, SlateNumber = 18, Label = "CFP Semifinals",           SlateType = "Semifinal",    StartDate = new DateOnly(2026,  1,  8), EndDate = new DateOnly(2026,  1,  9) },
        new() { Season = Season, SlateNumber = 19, Label = "CFP National Championship",SlateType = "Championship", StartDate = new DateOnly(2026,  1, 19), EndDate = new DateOnly(2026,  1, 19) },
    ];

    public async Task Execute(IJobExecutionContext context) {
        Log.Information("CfbSlateSeederJob: checking season {Season}", Season);

        var existing = (await repo.GetSlatesForSeasonAsync(Season)).ToList();
        if (existing.Count >= ExpectedSlateCount) {
            Log.Information("CfbSlateSeederJob: {Count} slates already seeded for {Season}, skipping", existing.Count, Season);
            return;
        }

        // Remove stale partial seed (e.g. old 4-slate CFP-only structure) before re-seeding
        if (existing.Count > 0) {
            await repo.DeleteSlatesAsync(existing);
            Log.Information("CfbSlateSeederJob: removed {Count} stale slates for {Season}", existing.Count, Season);
        }

        await repo.AddSlatesAsync(Slates2025);
        Log.Information("CfbSlateSeederJob: seeded {Count} slates for {Season}", Slates2025.Length, Season);
    }
}
