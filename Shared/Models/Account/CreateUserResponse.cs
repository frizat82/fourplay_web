namespace FourPlayWebApp.Shared.Models.Account;

public class CreateUserResponse {
    public string UserId { get; set; }
    public bool IsSuccess { get; set; }
    public List<string> Errors { get; set; } = [];
}