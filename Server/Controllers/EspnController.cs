using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Shared.Models;
using FourPlayWebApp.Shared.Models.Data.Dtos;
using FourPlayWebApp.Shared.Models.Enum;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FourPlayWebApp.Server.Controllers;
[Authorize]
[ApiController]
[Route("api/[controller]")]
public class EspnController(IEspnApiService espnApiService, IEspnCacheService espnCacheService)
    : ControllerBase {
    [HttpGet("scores/week/{week:int}/{year:int}")]
    [ProducesResponseType(typeof(EspnScores), StatusCodes.Status200OK)]
    public async Task<ActionResult<EspnScores?>> GetWeekScores(int week, int year, [FromQuery] bool postSeason = false)
    {
        var scores = await espnApiService.GetWeekScores(week, year, postSeason);
        return Ok(scores ?? new EspnScores());
    }

    [HttpGet("scores")]
    [ProducesResponseType(typeof(EspnScores), StatusCodes.Status200OK)]
    public async Task<ActionResult<EspnScores?>> GetScores()
    {
        var scores = await espnCacheService.GetScoresAsync();
        return Ok(scores ?? new EspnScores());
    }

    [HttpGet("livegames")]
    [ProducesResponseType(typeof(List<LiveGameDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<LiveGameDto>>> GetLiveGames()
    {
        var scores = await espnCacheService.GetScoresAsync();
        if (scores?.Events is null)
            return Ok(new List<LiveGameDto>());

        var games = scores.Events
            .SelectMany(e => e.Competitions)
            .Select(LiveGameDto.FromCompetition)
            .ToList();

        return Ok(games);
    }
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
