namespace FourPlayWebApp.Shared.Models.Account;

public class AssignRoleRequest
{
    public string UserId { get; set; } = "";
    public string Role { get; set; } = "Administrator";
}