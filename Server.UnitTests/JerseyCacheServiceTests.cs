using FourPlayWebApp.Server.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

namespace FourPlayWebApp.Server.UnitTests;

/// <summary>
/// Tests for JerseyCacheService — cache key isolation, hit/miss paths,
/// and team-abbreviation lookup, without making real HTTP calls.
/// </summary>
public class JerseyCacheServiceTests : IAsyncDisposable
{
    private readonly IMemoryCache _cache;
    private readonly JerseyCacheService _svc;

    // Cache key helper matches the private implementation
    private static string CacheKey(int season, int week) => $"jerseys_{season}_week_{week}";

    public JerseyCacheServiceTests()
    {
        _cache = new MemoryCache(new MemoryCacheOptions());
        // HttpClient pointing nowhere — tests that need RefreshAsync are skipped
        var httpClient = new HttpClient();
        _svc = new JerseyCacheService(httpClient, _cache, NullLogger<JerseyCacheService>.Instance);
    }

    // -----------------------------------------------------------------------
    // GetJerseys — cache miss
    // -----------------------------------------------------------------------

    [Fact]
    public void GetJerseys_WhenCacheMiss_ReturnsNull()
    {
        var result = _svc.GetJerseys(2025, 1);
        Assert.Null(result);
    }

    // -----------------------------------------------------------------------
    // GetJerseys — cache hit
    // -----------------------------------------------------------------------

    [Fact]
    public void GetJerseys_WhenCacheHit_ReturnsDictionary()
    {
        var jerseys = new Dictionary<string, string> { ["NE"] = "data:image/png;base64,abc" };
        _cache.Set(CacheKey(2025, 5), jerseys, TimeSpan.FromHours(1));

        var result = _svc.GetJerseys(2025, 5);

        Assert.NotNull(result);
        Assert.Equal("data:image/png;base64,abc", result["NE"]);
    }

    // -----------------------------------------------------------------------
    // GetJerseys — different season/week keys are isolated
    // -----------------------------------------------------------------------

    [Fact]
    public void GetJerseys_DifferentWeeks_DoNotShareCache()
    {
        var jerseys = new Dictionary<string, string> { ["BUF"] = "data:image/png;base64,xyz" };
        _cache.Set(CacheKey(2025, 3), jerseys, TimeSpan.FromHours(1));

        Assert.NotNull(_svc.GetJerseys(2025, 3));
        Assert.Null(_svc.GetJerseys(2025, 4));     // different week
        Assert.Null(_svc.GetJerseys(2024, 3));     // different season
    }

    // -----------------------------------------------------------------------
    // GetJerseyByTeam — returns correct data URI
    // -----------------------------------------------------------------------

    [Fact]
    public void GetJerseyByTeam_WhenTeamExists_ReturnsDataUri()
    {
        var jerseys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["KC"] = "data:image/png;base64,kc"
        };
        _cache.Set(CacheKey(2025, 7), jerseys, TimeSpan.FromHours(1));

        var result = _svc.GetJerseyByTeam("KC", 2025, 7);

        Assert.Equal("data:image/png;base64,kc", result);
    }

    // -----------------------------------------------------------------------
    // GetJerseyByTeam — case-insensitive lookup
    // -----------------------------------------------------------------------

    [Fact]
    public void GetJerseyByTeam_CaseInsensitive_FindsEntry()
    {
        var jerseys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["GB"] = "data:image/png;base64,gb"
        };
        _cache.Set(CacheKey(2025, 7), jerseys, TimeSpan.FromHours(1));

        Assert.Equal("data:image/png;base64,gb", _svc.GetJerseyByTeam("gb", 2025, 7));
        Assert.Equal("data:image/png;base64,gb", _svc.GetJerseyByTeam("Gb", 2025, 7));
    }

    // -----------------------------------------------------------------------
    // GetJerseyByTeam — missing team returns null
    // -----------------------------------------------------------------------

    [Fact]
    public void GetJerseyByTeam_WhenTeamMissing_ReturnsNull()
    {
        var jerseys = new Dictionary<string, string> { ["KC"] = "data:image/png;base64,kc" };
        _cache.Set(CacheKey(2025, 7), jerseys, TimeSpan.FromHours(1));

        Assert.Null(_svc.GetJerseyByTeam("BUF", 2025, 7));
    }

    // -----------------------------------------------------------------------
    // GetJerseyByTeam — null or empty teamAbbr returns null
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GetJerseyByTeam_NullOrWhitespaceAbbr_ReturnsNull(string? abbr)
    {
        Assert.Null(_svc.GetJerseyByTeam(abbr!, 2025, 7));
    }

    // -----------------------------------------------------------------------
    // GetJerseyByTeam — cache miss returns null
    // -----------------------------------------------------------------------

    [Fact]
    public void GetJerseyByTeam_WhenCacheMiss_ReturnsNull()
    {
        Assert.Null(_svc.GetJerseyByTeam("NE", 2025, 1));
    }

    public async ValueTask DisposeAsync()
    {
        _cache.Dispose();
        await _svc.DisposeAsync();
    }
}
