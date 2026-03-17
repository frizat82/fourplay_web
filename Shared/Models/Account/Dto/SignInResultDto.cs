namespace FourPlayWebApp.Shared.Models.Account.Dto;

public record SignInResultDto
{
    /// <summary>True if login succeeded</summary>
    public bool Succeeded { get; set; }

    /// <summary>True if the account is locked out</summary>
    public bool IsLockedOut { get; set; }

    /// <summary>True if two-factor authentication is required</summary>
    public bool RequiresTwoFactor { get; set; }

    /// <summary>True if login is not allowed (e.g., email not confirmed)</summary>
    public bool IsNotAllowed { get; set; }

    /// <summary>Optional: Number of remaining failed attempts before lockout</summary>
    public int? AccessFailedCount { get; set; }

    /// <summary>Optional: Lockout end date (UTC) if locked out</summary>
    public DateTimeOffset? LockoutEnd { get; set; }

    /// <summary>Optional message for client display</summary>
    public string? Message { get; set; }
}