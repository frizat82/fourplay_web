using FourPlayWebApp.Shared.Models.Data.Dtos;

namespace FourPlayWebApp.Server.Services.Interfaces;

public interface ICfbCurrentSlateService {
    Task<CfbSlateInfo?> GetCurrentSlateAsync();

    // Mirrors INflCurrentWeekService.GetFirstUnfinishedWeekAsync: the first slate of `season` that
    // isn't over yet, or null. A new league's default Start Week.
    Task<CfbSlateInfo?> GetFirstUnfinishedSlateAsync(int season);

    // Season-level (not slate-level) check: is a season actually happening right now, at all?
    // Callers that only need this yes/no answer (the ESPN cache poller) should use this instead
    // of re-deriving it externally — this service already owns the row-fetch + window-mapping.
    Task<bool> IsSeasonActiveAsync();
}
