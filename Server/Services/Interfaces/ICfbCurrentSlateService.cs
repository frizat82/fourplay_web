namespace FourPlayWebApp.Server.Services.Interfaces;

public record CfbSlateInfo(int Id, int Season, int SlateNumber, string Label, string SlateType,
    DateOnly StartDate, DateOnly EndDate, DateTimeOffset? FirstGameUtc, DateTime SpreadLockDatetime);

public interface ICfbCurrentSlateService {
    Task<CfbSlateInfo?> GetCurrentSlateAsync();
}
