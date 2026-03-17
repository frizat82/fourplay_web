namespace FourPlayWebApp.Shared.Models.Email;

public record ConfirmationRequest(string UserName, string Email, string ConfirmationLink);