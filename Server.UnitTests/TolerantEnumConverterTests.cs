using FourPlayWebApp.Server.Data.Configurations;
using FourPlayWebApp.Shared.Models.Enum;
using Xunit;

namespace FourPlayWebApp.Server.UnitTests;

// /code-review: HasConversion<string>() alone throws on read for any stored value that doesn't
// exactly match an enum member name — a single malformed row would 500 the whole query instead
// of degrading gracefully. TolerantEnumConverter is the fallback-instead-of-throw version used by
// CfbPicksConfiguration/CfbScoresConfiguration.
public class TolerantEnumConverterTests
{
    [Fact]
    public void Parse_ReturnsMatchingEnumMember_ForExactName()
    {
        var result = TolerantEnumConverter.Parse("Spread", PickType.Over);
        Assert.Equal(PickType.Spread, result);
    }

    [Fact]
    public void Parse_FallsBackToDefault_ForUnrecognizedText()
    {
        var result = TolerantEnumConverter.Parse("banana", PickType.Spread);
        Assert.Equal(PickType.Spread, result);
    }

    [Fact]
    public void Parse_FallsBackToDefault_ForNullOrEmpty()
    {
        Assert.Equal(PickType.Spread, TolerantEnumConverter.Parse(null, PickType.Spread));
        Assert.Equal(PickType.Spread, TolerantEnumConverter.Parse("", PickType.Spread));
    }
}
