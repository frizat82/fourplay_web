namespace FourPlayWebApp.Shared.Models.Account;

public class ForgotPasswordRequest
{
    public string Email { get; set; } = string.Empty;
    public string ResetUrl { get; set; } = string.Empty; // Base URL for reset page
}
