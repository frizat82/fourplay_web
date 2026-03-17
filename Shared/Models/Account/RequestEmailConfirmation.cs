namespace FourPlayWebApp.Shared.Models.Account;

public class RequestEmailConfirmation
{
    public string Email { get; set; } = string.Empty;
    public string ConfirmationUrl { get; set; } = string.Empty; // Base URL the user will be sent to
}