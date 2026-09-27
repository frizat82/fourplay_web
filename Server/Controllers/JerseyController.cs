using FourPlayWebApp.Server.Auth;
using FourPlayWebApp.Server.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FourPlayWebApp.Server.Controllers;

// Admin-only while the jersey feature is off (PicksPage, frizat-d2h). A cache miss scrapes
// gridiron-uniforms.com and image-processes every game, empty results aren't cached and
// concurrent misses aren't collapsed — so before opening this to users again, cache negative
// results and single-flight RefreshAsync, or anyone can make us hammer the upstream.
[Authorize(Roles = AppRoles.Administrator)]
[ApiController]
[Route("api/[controller]")]
public class JerseysController(IJerseyCacheService jerseyCacheService) : ControllerBase {

    [HttpGet("{season}/{week}")]
    [ProducesResponseType(typeof(Dictionary<string, string>), StatusCodes.Status200OK)]
    public async Task<ActionResult<Dictionary<string, string>?>> GetAll(int season, int week)
    {
        var dict = jerseyCacheService.GetJerseys(season, week);

        // If cache is empty, trigger refresh and return
        if (dict != null && dict.Count != 0)
            return Ok(dict);
        await jerseyCacheService.RefreshAsync(season, week);
        dict = jerseyCacheService.GetJerseys(season, week);

        return Ok(dict ?? new Dictionary<string, string>());
    }

    [HttpGet("{season}/{week}/{teamAbbr}")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<string?>> GetByTeam(int season, int week, string teamAbbr)
    {
        var img = jerseyCacheService.GetJerseyByTeam(teamAbbr, season, week);

        // If not found, try refreshing cache once
        if (img == null)
        {
            await jerseyCacheService.RefreshAsync(season, week);
            img = jerseyCacheService.GetJerseyByTeam(teamAbbr, season, week);
        }

        if (img == null) return NotFound();
        return Ok(img);
    }

    [HttpPost("{season}/{week}/refresh")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<ActionResult> RefreshCache(int season, int week)
    {
        // Trigger refresh asynchronously (fire and forget pattern)
        _ = Task.Run(() => jerseyCacheService.RefreshAsync(season, week));
        return Accepted();
    }
}
