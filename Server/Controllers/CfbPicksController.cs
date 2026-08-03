using FourPlayWebApp.Server.Auth;
using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Server.Services.Repositories.Interfaces;
using FourPlayWebApp.Shared.Helpers;
using FourPlayWebApp.Shared.Models.Data;
using FourPlayWebApp.Shared.Models.Data.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using FourPlayWebApp.Server.Infrastructure;

namespace FourPlayWebApp.Server.Controllers;

[ApiController]
[Route("api/cfb")]
[Authorize]
public class CfbPicksController(ICfbPicksRepository repo, ICfbRepository cfbRepo, ILeagueRepository leagueRepo) : ControllerBase {
    [HttpGet("current-slate")]
    public async Task<IActionResult> GetCurrentSlate([FromServices] ICfbCurrentSlateService svc) {
        var slate = await svc.GetCurrentSlateAsync();
        return slate is null ? NotFound() : Ok(slate);
    }

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

    // CFB has no live ESPN status feed the way NFL does (see cfbAdapter.ts) — CfbSpreads.GameTime
    // is the source of truth for kickoff time, same as the frontend's own gameIsLocked check.
    private static HashSet<int> StartedEventIds(IEnumerable<CfbSpreads> spreads, DateTimeOffset now) =>
        spreads.Where(s => s.GameTime <= now).Select(s => s.EspnEventId).ToHashSet();

    [HttpGet("picks/{leagueId}/{cfbSlateId}")]
    public async Task<IActionResult> GetAllPicks(int leagueId, int cfbSlateId) {
        var callerId = CurrentUserId;
        var isAdmin = User.IsInRole(AppRoles.Administrator);
        if (!isAdmin && !await leagueRepo.UserExistsInLeagueAsync(callerId, leagueId))
            return Forbid();

        var allPicksTask = repo.GetAllPicksForSlateAsync(leagueId, cfbSlateId);
        var spreadsTask = isAdmin
            ? Task.FromResult<IEnumerable<CfbSpreads>>([])
            : cfbRepo.GetSpreadsForSlateAsync(cfbSlateId);
        await Task.WhenAll(allPicksTask, spreadsTask);
        var allPicks = allPicksTask.Result.ToList();

        // Hide other users' picks for games that haven't kicked off yet — same rule as NFL's
        // GetLeaguePicks. Admins always see all picks, same as NFL.
        if (!isAdmin) {
            var startedEventIds = StartedEventIds(spreadsTask.Result, DateTimeOffset.UtcNow);
            allPicks = allPicks
                .Where(p => p.UserId == callerId || startedEventIds.Contains(p.EspnEventId))
                .ToList();
        }

        return Ok(allPicks);
    }

    [HttpGet("picks/{leagueId}/{cfbSlateId}/user")]
    public async Task<IActionResult> GetUserPicks(int leagueId, int cfbSlateId) {
        var picks = await repo.GetUserPicksAsync(leagueId, cfbSlateId, CurrentUserId);
        return Ok(picks);
    }

    [HttpPost("picks")]
    public async Task<IActionResult> AddPicks([FromBody] AddCfbPicksRequest request) {
        var userId = CurrentUserId;

        if (!await leagueRepo.UserExistsInLeagueAsync(userId, request.LeagueId))
            return Forbid();

        var spreadsTask = cfbRepo.GetSpreadsForSlateAsync(request.CfbSlateId);
        var existingPicksTask = repo.GetUserPicksAsync(request.LeagueId, request.CfbSlateId, userId);
        var slateTask = cfbRepo.GetSlateByIdAsync(request.CfbSlateId);
        await Task.WhenAll(spreadsTask, existingPicksTask, slateTask);

        var slate = slateTask.Result;
        if (slate is null)
            return BadRequest("Cfb Slate Does Not Exist");

        // Guard: reject picks for any game that has already kicked off.
        var startedEventIds = StartedEventIds(spreadsTask.Result, DateTimeOffset.UtcNow);
        foreach (var pick in request.Picks) {
            if (startedEventIds.Contains(pick.EspnEventId))
                return BadRequest($"Pick rejected: {pick.Team}'s game has already kicked off.");
        }

        var existingPicks = existingPicksTask.Result.ToList();
        var existingKeys = existingPicks
            .Select(p => $"{p.EspnEventId}|{p.Team}|{p.PickType}")
            .ToHashSet();
        var newPicks = request.Picks
            .Where(p => !existingKeys.Contains($"{p.EspnEventId}|{p.Team}|{p.PickType}"))
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

        var requiredPicks = GameHelpers.GetCfbRequiredPicks(slate.SlateNumber);
        if (newPicks.Count + existingPicks.Count > requiredPicks)
            return BadRequest($"Too many picks. Maximum allowed for this slate is {requiredPicks}");

        if (newPicks.Count > 0)
            await repo.AddPicksAsync(newPicks);

        return Ok(new { added = newPicks.Count });
    }

    [HttpDelete("picks/{leagueId}/{cfbSlateId}")]
    [Authorize(Roles = AppRoles.Administrator)]
    public async Task<IActionResult> DeletePicks(int leagueId, int cfbSlateId) {
        await repo.DeletePicksAsync(leagueId, cfbSlateId, CurrentUserId);
        return Ok();
    }

    [HttpGet("live-stream")]
    public Task LiveStream([FromServices] ICfbCacheService cfbCacheService, CancellationToken ct) =>
        SseHelper.StreamAsync(Response,
            h => cfbCacheService.ScoresChanged += h,
            h => cfbCacheService.ScoresChanged -= h,
            ct);

    [HttpGet("week-configs/{season:int}")]
    [Authorize(Roles = AppRoles.Administrator)]
    public async Task<ActionResult<List<CfbSeasonWeekConfigDto>>> GetWeekConfigs(int season) {
        var configs = await cfbRepo.GetWeekConfigsForSeasonAsync(season);
        var dtos = configs.Select(c => new CfbSeasonWeekConfigDto(
            c.EspnWeekNumber,
            c.IvLeagueWeekNumber,
            c.WeekType,
            c.ScoringFormat,
            c.InScopeIvLeague,
            c.WeekStartDate,
            c.WeekEndDate,
            c.Notes));
        return Ok(dtos);
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
