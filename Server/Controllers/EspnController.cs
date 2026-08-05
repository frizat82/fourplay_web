using FourPlayWebApp.Server.Infrastructure;
using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Server.Services.Repositories.Interfaces;
using FourPlayWebApp.Shared.Models;
using FourPlayWebApp.Shared.Models.Data.Dtos;
using FourPlayWebApp.Shared.Models.Enum;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FourPlayWebApp.Server.Controllers;
[Authorize]
[ApiController]
[Route("api/[controller]")]
public class EspnController(
    IEspnCacheService espnCacheService,
    ICfbCacheService cfbCacheService,
    ICfbLiveScoreFetcher cfbFetcher,
    ICfbRepository cfbRepo)
    : ControllerBase {
    [HttpGet("scores/week/{week:int}/{year:int}")]
    [ProducesResponseType(typeof(EspnScores), StatusCodes.Status200OK)]
    public async Task<ActionResult<EspnScores?>> GetWeekScores(int week, int year, [FromQuery] bool postSeason = false)
    {
        var scores = await espnCacheService.GetWeekScoresAsync(week, year, postSeason);
        return Ok(scores ?? new EspnScores());
    }

    [HttpGet("scores")]
    [ProducesResponseType(typeof(EspnScores), StatusCodes.Status200OK)]
    public async Task<ActionResult<EspnScores?>> GetScores()
    {
        var scores = await espnCacheService.GetScoresAsync();
        return Ok(scores ?? new EspnScores());
    }

    /// <summary>
    /// Live CFB scores for the CURRENT slate — cached, same role as GetScores() for NFL. One poll
    /// serves every concurrent viewer instead of each triggering its own ESPN call.
    /// </summary>
    [HttpGet("cfb/scores")]
    [ProducesResponseType(typeof(EspnScores), StatusCodes.Status200OK)]
    public async Task<ActionResult<EspnScores?>> GetCfbScores()
    {
        var scores = await cfbCacheService.GetScoresAsync();
        return Ok(scores ?? new EspnScores());
    }

    /// <summary>
    /// Live CFB scores for a SPECIFIC (typically non-current) slate — direct/uncached, same role
    /// as GetWeekScores() for NFL. Used when browsing a past or future slate, which isn't repeatedly
    /// polled the way the current slate is.
    /// </summary>
    [HttpGet("cfb/scores/slate/{slateId:int}")]
    [ProducesResponseType(typeof(EspnScores), StatusCodes.Status200OK)]
    public async Task<ActionResult<EspnScores?>> GetCfbScoresForSlate(int slateId)
    {
        var slate = await cfbRepo.GetSlateByIdAsync(slateId);
        if (slate is null) return Ok(new EspnScores());

        var scores = await cfbFetcher.FetchForSlateAsync(slate);
        return Ok(scores ?? new EspnScores());
    }

    [HttpGet("livegames")]
    [ProducesResponseType(typeof(List<LiveGameDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<LiveGameDto>>> GetLiveGames() =>
        Ok(BuildLiveGames(await espnCacheService.GetScoresAsync()));

    // CFB's own live-games endpoint — a prior version of this reused the NFL one above, so CFB
    // situation/field-position data was always read from the NFL cache and could never match a
    // real CFB event (silently always empty).
    [HttpGet("cfb/livegames")]
    [ProducesResponseType(typeof(List<LiveGameDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<LiveGameDto>>> GetCfbLiveGames() =>
        Ok(BuildLiveGames(await cfbCacheService.GetScoresAsync()));

    private static List<LiveGameDto> BuildLiveGames(EspnScores? scores) =>
        scores?.Events is null
            ? []
            : scores.Events.SelectMany(e => e.Competitions).Select(LiveGameDto.FromCompetition).ToList();

    [HttpGet("live-stream")]
    public Task LiveStream(CancellationToken ct) =>
        SseHelper.StreamAsync(Response,
            h => espnCacheService.ScoresChanged += h,
            h => espnCacheService.ScoresChanged -= h,
            ct);

/*
    [HttpGet("odds/events/{eventId:int}")]
    [ProducesResponseType(typeof(ESPNCoreOddsApiResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ESPNCoreOddsApiResponse?>> GetEventsWithOdds(int eventId)
    {
        var odds = await espnCoreOddsService.GetEventsWithOddsAsync(eventId);
        return Ok(odds);
    }

    [HttpGet("odds/events/{eventId:int}/provider/{provider}")]
    [ProducesResponseType(typeof(ESPNCoreOddsApiResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ESPNCoreOddsApiResponse?>> GetEventsWithOdds(int eventId, EspnOddsProviders provider)
    {
        var odds = await espnCoreOddsService.GetEventsWithOddsAsync(eventId, (int)provider);
        return Ok(odds);
    }
    */
}
