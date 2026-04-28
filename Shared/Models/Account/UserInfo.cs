using FourPlayWebApp.Shared.Models.Account.Dto;

namespace FourPlayWebApp.Shared.Models.Account;

public record UserInfo(string UserId, string? Name, IEnumerable<ClaimDto> Claims);
