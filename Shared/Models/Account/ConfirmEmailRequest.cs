namespace FourPlayWebApp.Shared.Models.Account;

public class ConfirmEmailRequest
{
    public string UserId { get; set; } = "";
    public string Token { get; set; } = "";
}
