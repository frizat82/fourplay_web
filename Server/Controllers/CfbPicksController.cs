using FourPlayWebApp.Server.Auth;
using FourPlayWebApp.Server.Jobs;
using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Server.Services.Repositories.Interfaces;
using FourPlayWebApp.Shared.Helpers;
using FourPlayWebApp.Shared.Models.Data;
using FourPlayWebApp.Shared.Models.Data.Dtos;
using FourPlayWebApp.Shared.Models.Enum;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text.Json.Serialization;
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

    // Rules page: the full current season's spread-lock schedule, every in-scope week — mirrors
    // LeagueController.GetSpreadLockSchedule's role for NFL. Resolves "current season" via the
    // same service GetCurrentSlate uses, so the frontend doesn't need a separate round-trip just
    // to learn which season to ask for.
    [HttpGet("spread-lock-schedule")]
    public async Task<IActionResult> GetSpreadLockSchedule([FromServices] ICfbCurrentSlateService currentSlateService) {
        var current = await currentSlateService.GetCurrentSlateAsync();
        if (current is null) return Ok(Array.Empty<SpreadLockWeekDto>());

        var configs = await cfbRepo.GetWeekConfigsForSeasonAsync(current.Season);
        var schedule = configs
            .Where(c => c.InScopeIvLeague)
            .OrderBy(c => c.IvLeagueWeekNumber)
            .Select(c => new SpreadLockWeekDto(CfbWeekLabelHelper.LabelFromConfig(c), c.SpreadLockDatetime));
        return Ok(schedule);
    }

    [HttpGet("slates/{season}")]
    public async Task<IActionResult> GetSlates(int season) =>
        Ok(await cfbRepo.GetSlatesForSeasonAsync(season));

    // frizat-9m0: the full FBS slate is persisted for audit (see CfbSpreadJob), but only
    // league-eligible games (ranked-either-side or CFP, excluding MAC Tue/Wed) are ever served —
    // the filter moved here, from ingestion, so the historical data underneath stays complete.
    [HttpGet("spreads/{cfbSlateId}")]
    public async Task<IActionResult> GetSpreads(int cfbSlateId) {
        var spreads = await cfbRepo.GetSpreadsForSlateAsync(cfbSlateId);
        return Ok(spreads.WhereLeagueEligible());
    }

    [HttpGet("scores/{cfbSlateId}")]
    public async Task<IActionResult> GetScores(int cfbSlateId) {
        var spreadsTask = cfbRepo.GetSpreadsForSlateAsync(cfbSlateId);
        var scoresTask = cfbRepo.GetScoresForSlateAsync(cfbSlateId);
        await Task.WhenAll(spreadsTask, scoresTask);

        // Fail-open, same philosophy as AddPicks's ineligibility guard: exclude only events we
        // KNOW are ineligible (a matching CfbSpreads row exists and says so). A completed game
        // with no matching spread row at all (e.g. CfbSpreadJob's one-shot-per-week fetch missed
        // a late schedule change) still shows rather than silently vanishing from the scores page.
        // Matched by HomeTeam (unique within a slate) rather than an ESPN id — CfbScores.HomeTeam
        // and CfbSpreads.HomeTeam are populated from the same real-world game by design.
        var ineligibleHomeTeams = spreadsTask.Result.WhereLeagueIneligible().Select(s => s.HomeTeam).ToHashSet();
        var scores = scoresTask.Result.Where(s => !ineligibleHomeTeams.Contains(s.HomeTeam));
        var dtos = scores.Select(s => new CfbScoreDto {
            Id                  = s.Id,
            CfbSlateId          = s.CfbSlateId,
            HomeTeam            = s.HomeTeam,
            AwayTeam            = s.AwayTeam,
            HomeTeamScore       = s.HomeTeamScore,
            AwayTeamScore       = s.AwayTeamScore,
            GameStatus          = s.GameStatus.ToString(),
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
    // Returns both home and away team names for started games — a pick can be on either side.
    private static HashSet<string> StartedTeams(IEnumerable<CfbSpreads> spreads, DateTimeOffset now) =>
        spreads.Where(s => s.GameTime <= now).SelectMany(s => new[] { s.HomeTeam, s.AwayTeam }).ToHashSet();

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
            var startedTeams = StartedTeams(spreadsTask.Result, DateTimeOffset.UtcNow);
            allPicks = allPicks
                .Where(p => p.UserId == callerId || startedTeams.Contains(p.Team))
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

        // Guard: reject picks for any game that has already kicked off. Matched by team name
        // (either side) rather than an ESPN id — a team plays at most one game per slate, so
        // Team alone unambiguously identifies which CfbSpreads row a pick belongs to.
        var startedTeams = StartedTeams(spreadsTask.Result, DateTimeOffset.UtcNow);

        // Guard: reject picks for a team we KNOW is excluded from the league (MAC Tue/Wed,
        // unranked, etc. — frizat-9m0). Distinct from "no matching spread at all", which stays
        // fail-open below (e.g. an ESPN cache gap) — this only rejects positive knowledge of
        // ineligibility, not absence of data.
        var ineligibleTeams = spreadsTask.Result.WhereLeagueIneligible()
            .SelectMany(s => new[] { s.HomeTeam, s.AwayTeam }).ToHashSet();

        foreach (var pick in request.Picks) {
            if (startedTeams.Contains(pick.Team))
                return BadRequest($"Pick rejected: {pick.Team}'s game has already kicked off.");
            if (ineligibleTeams.Contains(pick.Team))
                return BadRequest($"Pick rejected: {pick.Team}'s game is not part of this league's slate.");
        }

        var existingPicks = existingPicksTask.Result.ToList();
        var existingKeys = existingPicks
            .Select(p => $"{p.Team}|{p.PickType}")
            .ToHashSet();
        var newPicks = request.Picks
            .Where(p => !existingKeys.Contains($"{p.Team}|{p.PickType}"))
            .Select(p => new CfbPicks {
                UserId      = userId,
                LeagueId    = request.LeagueId,
                CfbSlateId  = request.CfbSlateId,
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

        return Ok(new AddCfbPicksResponseDto(newPicks.Count));
    }

    [HttpDelete("picks/{leagueId}/{cfbSlateId}")]
    [Authorize(Roles = AppRoles.Administrator)]
    public async Task<IActionResult> DeletePicks(int leagueId, int cfbSlateId, [FromQuery] string userId) {
        await repo.DeletePicksAsync(leagueId, cfbSlateId, userId);
        return Ok();
    }

    [HttpGet("live-stream")]
    public Task LiveStream([FromServices] ICfbCacheService cfbCacheService, CancellationToken ct) =>
        SseHelper.StreamAsync(Response,
            h => cfbCacheService.ScoresChanged += h,
            h => cfbCacheService.ScoresChanged -= h,
            ct);

}

public record AddCfbPicksRequest {
    public int LeagueId    { get; init; }
    public int CfbSlateId  { get; init; }
    public int Season      { get; init; }
    public List<CfbPickItem> Picks { get; init; } = [];
}

public record CfbPickItem {
    public string Team { get; init; } = string.Empty;
    [property: JsonConverter(typeof(JsonStringEnumConverter))]
    public PickType PickType { get; init; } = PickType.Spread;
}
