using FourPlayWebApp.Server.Infrastructure;

namespace FourPlayWebApp.Server.UnitTests;

public class CorsStartupValidationTests
{
    [Fact]
    public void ParseAndValidateCorsOrigins_ThrowsInProduction_WhenEmpty()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => StartupValidation.ParseAndValidateCorsOrigins(null, isDevelopment: false));
        Assert.Contains("ALLOWED_ORIGINS", ex.Message);
    }

    [Fact]
    public void ParseAndValidateCorsOrigins_ThrowsInProduction_WhenWhitespace()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => StartupValidation.ParseAndValidateCorsOrigins("  ", isDevelopment: false));
        Assert.Contains("ALLOWED_ORIGINS", ex.Message);
    }

    [Fact]
    public void ParseAndValidateCorsOrigins_SucceedsInDevelopment_WhenEmpty()
    {
        var result = StartupValidation.ParseAndValidateCorsOrigins(null, isDevelopment: true);
        Assert.Empty(result);
    }

    [Fact]
    public void ParseAndValidateCorsOrigins_ParsesOrigins_InProduction()
    {
        var result = StartupValidation.ParseAndValidateCorsOrigins(
            "https://ivleague.com, https://cfb.ivleague.com", isDevelopment: false);
        Assert.Equal(2, result.Length);
        Assert.Contains("https://ivleague.com", result);
        Assert.Contains("https://cfb.ivleague.com", result);
    }

    [Fact]
    public void ParseAndValidateCorsOrigins_ParsesOrigins_InDevelopment()
    {
        var result = StartupValidation.ParseAndValidateCorsOrigins(
            "http://localhost:5173", isDevelopment: true);
        Assert.Single(result);
        Assert.Equal("http://localhost:5173", result[0]);
    }
}
