namespace FourPlayWebApp.Shared.Models.Email;

public record PasswordResetLinkRequest(string UserName, string Email, string ResetLink);