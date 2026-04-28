namespace FourPlayWebApp.Shared.Models.Account
{
    public class ChangePassword
    {
        public string CurrentPassword { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}
