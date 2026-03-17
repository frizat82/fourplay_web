namespace FourPlayWebApp.Shared.Models.Account;

public class CreateUserRequest {
    public string Username { get; set; }
    public string Password { get; set; }
    public string Email { get; set; }
    public string Code { get; set; }
}