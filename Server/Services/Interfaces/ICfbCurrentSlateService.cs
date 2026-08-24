namespace FourPlayWebApp.Server.Services.Interfaces;

public record CfbSlateInfo(int Id, int Season, int SlateNumber, string Label, string SlateType,
    DateOnly StartDate, DateOnly EndDate, DateTimeOffset? FirstGameUtc, DateTime SpreadLockDatetime);

public interface ICfbCurrentSlateService {
    Task<CfbSlateInfo?> GetCurrentSlateAsync();

    // Season-level (not slate-level) check: is a season actually happening right now, at all?
    // Callers that only need this yes/no answer (the ESPN cache poller) should use this instead
    // of re-deriving it externally — this service already owns the row-fetch + window-mapping.
    Task<bool> IsSeasonActiveAsync();
}
