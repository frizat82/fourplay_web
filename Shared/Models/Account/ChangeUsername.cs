namespace FourPlayWebApp.Shared.Models.Account
{
    public class ChangeUsername
    {
        public string CurrentPassword { get; set; } = string.Empty;
        public string NewUsername { get; set; } = string.Empty;
    }
}
