using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Shared.Models.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace FourPlayWebApp.Server.Controllers;
[Authorize]
[ApiController]
[Route("api/[controller]/{leagueId:int}")]
public class LeaderboardController(
    ILeaderboardService leaderboardService, IMemoryCache memoryCache)
    : ControllerBase {
    [HttpGet("leaderboard/{seasonYear:long}")]
    public async Task<ActionResult<List<LeaderboardDto>>> GetLeaderboard(int leagueId, long seasonYear) {
        var scoreboard = await memoryCache.GetOrCreateAsync($"{leagueId}-{seasonYear}", async entry => {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
            return  await leaderboardService.BuildLeaderboard(leagueId, seasonYear);
        });
        if (scoreboard == null) return NotFound();
        var scoreboardDto = scoreboard.Select(m => new LeaderboardDto {
            UserId = m.User.Id,
            UserName = m.User.UserName ?? m.User.NormalizedUserName ?? string.Empty,
            Total = m.Total,
            WeekResults = m.WeekResults,
            Rank = m.Rank
        }).ToList();
        return Ok(scoreboardDto);
    }
}
