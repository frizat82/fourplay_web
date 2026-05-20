using FourPlayWebApp.Server.Services.Repositories.Interfaces;
using FourPlayWebApp.Shared.Models.Data;
using FourPlayWebApp.Shared.Models.Data.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FourPlayWebApp.Server.Controllers;

[ApiController]
[Route("api/cfb")]
[Authorize]
public class CfbPicksController(ICfbPicksRepository repo, ICfbRepository cfbRepo) : ControllerBase {
    [HttpGet("slates/{season}")]
    public async Task<IActionResult> GetSlates(int season) =>
        Ok(await cfbRepo.GetSlatesForSeasonAsync(season));

    [HttpGet("spreads/{cfbSlateId}")]
    public async Task<IActionResult> GetSpreads(int cfbSlateId) =>
        Ok(await cfbRepo.GetSpreadsForSlateAsync(cfbSlateId));

    [HttpGet("scores/{cfbSlateId}")]
    public async Task<IActionResult> GetScores(int cfbSlateId) {
        var scores = await cfbRepo.GetScoresForSlateAsync(cfbSlateId);
        var dtos = scores.Select(s => new CfbScoreDto {
            Id                  = s.Id,
            CfbSlateId          = s.CfbSlateId,
            EspnEventId         = s.EspnEventId,
            HomeTeam            = s.HomeTeam,
            AwayTeam            = s.AwayTeam,
            HomeTeamScore       = s.HomeTeamScore,
            AwayTeamScore       = s.AwayTeamScore,
            GameStatus          = s.GameStatus,
            GameTime            = s.GameTime.ToString("O"),
            WeatherDisplayValue = s.WeatherDisplayValue,
            WeatherConditionId  = s.WeatherConditionId,
            WeatherTemperatureF = s.WeatherTemperatureF,
        });
        return Ok(dtos);
    }

    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    [HttpGet("picks/{leagueId}/{cfbSlateId}")]
    public async Task<IActionResult> GetAllPicks(int leagueId, int cfbSlateId) {
        var picks = await repo.GetAllPicksForSlateAsync(leagueId, cfbSlateId);
        return Ok(picks);
    }

    [HttpGet("picks/{leagueId}/{cfbSlateId}/user")]
    public async Task<IActionResult> GetUserPicks(int leagueId, int cfbSlateId) {
        var picks = await repo.GetUserPicksAsync(leagueId, cfbSlateId, CurrentUserId);
        return Ok(picks);
    }

    [HttpPost("picks")]
    public async Task<IActionResult> AddPicks([FromBody] AddCfbPicksRequest request) {
        var userId = CurrentUserId;
        var existing = (await repo.GetUserPicksAsync(request.LeagueId, request.CfbSlateId, userId))
            .Select(p => $"{p.EspnEventId}|{p.Team}|{p.PickType}")
            .ToHashSet();

        var newPicks = request.Picks
            .Where(p => !existing.Contains($"{p.EspnEventId}|{p.Team}|{p.PickType}"))
            .Select(p => new CfbPicks {
                UserId      = userId,
                LeagueId    = request.LeagueId,
                CfbSlateId  = request.CfbSlateId,
                EspnEventId = p.EspnEventId,
                Team        = p.Team,
                PickType    = p.PickType,
                Season      = request.Season,
            })
            .ToList();

        if (newPicks.Count > 0)
            await repo.AddPicksAsync(newPicks);

        return Ok(new { added = newPicks.Count });
    }

    [HttpDelete("picks/{leagueId}/{cfbSlateId}")]
    public async Task<IActionResult> DeletePicks(int leagueId, int cfbSlateId) {
        await repo.DeletePicksAsync(leagueId, cfbSlateId, CurrentUserId);
        return Ok();
    }
}

public record AddCfbPicksRequest {
    public int LeagueId    { get; init; }
    public int CfbSlateId  { get; init; }
    public int Season      { get; init; }
    public List<CfbPickItem> Picks { get; init; } = [];
}

public record CfbPickItem {
    public int    EspnEventId { get; init; }
    public string Team        { get; init; } = string.Empty;
    public string PickType    { get; init; } = "Spread";
}
