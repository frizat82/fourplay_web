namespace FourPlayWebApp.Shared.Models.Email;

public record EmailRequest(string ToEmail, string Subject, string HtmlBody);
