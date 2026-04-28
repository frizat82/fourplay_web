namespace FourPlayWebApp.Shared.Models.Email;

public record PasswordResetCodeRequest(string UserName, string Email, string ResetCode);
