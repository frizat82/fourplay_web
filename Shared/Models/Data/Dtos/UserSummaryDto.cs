namespace FourPlayWebApp.Shared.Models.Data.Dtos;

// Match the safe DTO defined in the controller; ideally move to Shared
public sealed record UserSummaryDto(string Id, string? UserName, string? Email, bool EmailConfirmed, bool IsAdmin);