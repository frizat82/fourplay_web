namespace FourPlayWebApp.Shared.Helpers;

public static class WebEncoding {
    public static byte[] Base64UrlDecode(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return [];

        var base64 = input
            .Replace('-', '+')
            .Replace('_', '/');

        // Add padding if missing
        switch (base64.Length % 4)
        {
            case 2: base64 += "=="; break;
            case 3: base64 += "="; break;
        }

        return Convert.FromBase64String(base64);
    }
}