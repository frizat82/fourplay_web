using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Shared.Helpers;
using Microsoft.Extensions.Caching.Memory;
using HtmlAgilityPack;
using Serilog;
using System.Collections.Concurrent;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace FourPlayWebApp.Server.Services;

public class JerseyCacheService(HttpClient httpClient, IMemoryCache cache, ILogger<JerseyCacheService> logger) : IJerseyCacheService, IAsyncDisposable {

    // Generate cache key based on season and week
    private static string GetCacheKey(int season, int week) => $"jerseys_{season}_week_{week}";

    // -------------------------
    // PUBLIC METHODS
    // -------------------------

    public Dictionary<string, string>? GetJerseys(int season, int week)
        => cache.Get<Dictionary<string, string>?>(GetCacheKey(season, week));

    public string? GetJerseyByTeam(string teamAbbr, int season, int week) {
        if (string.IsNullOrWhiteSpace(teamAbbr)) return null;
        var dict = cache.Get<Dictionary<string, string>?>(GetCacheKey(season, week));
        if (dict == null) return null;

        var key = teamAbbr.Trim().ToUpperInvariant();
        return dict.TryGetValue(key, out var uri) ? uri : null;
    }

    public async Task RefreshAsync(int season, int week)
    {
        var cacheKey = GetCacheKey(season, week);

        try
        {
            logger.LogInformation("Refreshing jersey cache for season {Season}, week {Week}", season, week);

            // Step 1: Build the URL with season and week
            var weeklyUrl = $"controller/controller.php?action=weekly&year={season}&week={week}&league=NFL";
            var html = await httpClient.GetStringAsync(weeklyUrl);

            // Step 2: Parse the page to find jersey images and team associations
            var jerseyMap = await ParseWeeklyPageAndDownloadImagesAsync(html);

            // Step 3: Cache the results for 1 hour
            if (jerseyMap?.Count > 0)
            {
                cache.Set(cacheKey, jerseyMap.ToDictionary(kvp => kvp.Key, kvp => kvp.Value), TimeSpan.FromHours(3));
                logger.LogDebug("✅ Cached {Count} jerseys for season {Season}, week {Week}", jerseyMap.Count, season, week);
            }
            else
            {
                logger.LogWarning("⚠️ No jerseys found for season {Season}, week {Week}", season, week);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "⚠️ Jersey cache refresh failed for season {Season}, week {Week}", season, week);
        }
    }

    // -------------------------
    // PARSING HELPERS
    // -------------------------

    private async Task<ConcurrentDictionary<string, string>?> ParseWeeklyPageAndDownloadImagesAsync(string html)
    {
        if (string.IsNullOrWhiteSpace(html)) return null;

        // Step 1: Load the HTML into HtmlAgilityPack for parsing
        var htmlDoc = new HtmlDocument();
        htmlDoc.LoadHtml(html);

        // Step 2: Find all game links (the game pages we need to scrape images from)
        var links = (htmlDoc.DocumentNode
            .SelectNodes("//a[contains(@href, 'action=single-weekly&game_id')]")
            ?.Select(a => a.GetAttributeValue("href", string.Empty).Replace("../", ""))
            .Distinct().ToList());

        var results = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var tasks = links?.Select(url => ParseGameAndDownloadImagesAsync(url, results)).ToList();
        if (tasks != null) await Task.WhenAll(tasks);

        return results.Count > 0 ? results : null;
    }

    private async Task ParseGameAndDownloadImagesAsync(string url, ConcurrentDictionary<string, string> results)
    {
        try {
            var html = await httpClient.GetStringAsync(url);
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(html);

            // Step 2: Find image tags (team jersey images) on the game page
            var imageParent = htmlDoc.DocumentNode
                .SelectSingleNode("//*[@id=\"single-weekly-unis\"]");


            var imgNodes = imageParent
                .SelectNodes("./img");
            foreach (var imgNode in imgNodes) {
                var src = imgNode.GetAttributeValue("src", string.Empty).Replace("../","");
                if (string.IsNullOrWhiteSpace(src)) continue;

                // Step 3: Extract the team abbreviation using Path methods
                // Expected format: 2025_MIN_H1.png or 2025_GB_A2.png
                var fileName = Path.GetFileNameWithoutExtension(src);
                var parts = fileName.Split('_');

                // Validate we have the expected format: [YEAR]_[TEAM]_[HOME/AWAY][NUMBER]
                if (parts.Length < 3)
                    continue;
                var teamAbbr = parts[1].ToUpperInvariant();

                // Validate it looks like a team abbreviation (2-4 letters)
                if (teamAbbr.Length is < 2 or > 4 || !teamAbbr.All(char.IsLetter))
                    continue;
                var imgUrl = src;

                foreach (var map in NflTeamMappingHelpers.NflTeamAbbrMapping.Where(map => map.Key == teamAbbr))
                {
                    teamAbbr = map.Value;
                }
                // Step 4: Download image and convert it to Base64 with transparent background
                if (results.ContainsKey(teamAbbr))
                    continue;
                try {
                    var bytes = await httpClient.GetByteArrayAsync(imgUrl);
                    logger.LogDebug("Downloaded jersey for {Team} from {Url}, size: {Size} bytes", teamAbbr, imgUrl, bytes.Length);
                    if (await BackgroundRemover.IsTbaImage(bytes))
                        continue;
                    // Convert white background to transparent
                    var transparentBytes = await BackgroundRemover.FloodFillTransparent(bytes, 0, 0);
                    logger.LogDebug("Converted {Team} jersey to transparent, new size: {Size} bytes", teamAbbr, transparentBytes.Length);
                    var base64 = Convert.ToBase64String(transparentBytes);
                    var dataUri = $"data:image/png;base64,{base64}";
                    results.TryAdd(teamAbbr, dataUri);
                }
                catch (Exception ex) {
                    logger.LogError(ex, "Failed to process jersey image for {Team} from {Url}", teamAbbr, imgUrl);
                }
            }
        }
        catch (Exception ex) {
            logger.LogError(ex, "Failed to parse game page {Url}", url);
        }
    }

    // -------------------------
    // CLEANUP
    // -------------------------

    public async ValueTask DisposeAsync()
    {
        // Dispose resources if needed
        await Task.CompletedTask;
    }
}
