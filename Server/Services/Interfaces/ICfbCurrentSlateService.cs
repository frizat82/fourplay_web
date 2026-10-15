using FourPlayWebApp.Shared.Models.Data.Dtos;

namespace FourPlayWebApp.Server.Services.Interfaces;

public interface ICfbCurrentSlateService {
    Task<CfbSlateInfo?> GetCurrentSlateAsync();

    // Season-level (not slate-level) check: is a season actually happening right now, at all?
    // Callers that only need this yes/no answer (the ESPN cache poller) should use this instead
    // of re-deriving it externally — this service already owns the row-fetch + window-mapping.
    Task<bool> IsSeasonActiveAsync();
}
