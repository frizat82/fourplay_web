using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Models.Identity;
using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Server.Services.Repositories.Interfaces;
using FourPlayWebApp.Shared.Helpers;
using FourPlayWebApp.Shared.Models;
using FourPlayWebApp.Shared.Models.Data;
using FourPlayWebApp.Shared.Models.Data.Dtos;
using FourPlayWebApp.Shared.Models.Enum;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using System.Net.Mime;
using System.Security.Claims;

namespace FourPlayWebApp.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces(MediaTypeNames.Application.Json)]
public class LeagueController(
    IMemoryCache memoryCache,
    ILeagueRepository repo,
    ILogger<LeagueController> logger,
    UserManager<ApplicationUser> userManager,
    ISpreadCalculatorBuilder spreadCalculatorBuilder,
    IEspnCacheService espnCacheService) : ControllerBase {
    // ---------- League Info ----------
    [HttpGet("{leagueId:int}")]
    [ProducesResponseType(typeof(LeagueInfoDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<LeagueInfoDto>> GetLeagueInfo(int leagueId) {
        var info = await repo.GetLeagueInfoAsync(leagueId);
        var dtoInfos = new LeagueInfoDto {
            LeagueName = info.LeagueName,
            DateCreated = info.DateCreated,
            OwnerUserId = info.OwnerUserId
        };
        return Ok(dtoInfos);
    }

    [HttpGet("by-name/{leagueName}")]
    [ProducesResponseType(typeof(LeagueInfoDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<LeagueInfoDto?>> GetLeagueByName(string leagueName) {
        try {
            var info = await repo.GetLeagueByNameAsync(leagueName);
            if (info is null) return Ok(null);

            var dtoInfo = new LeagueInfoDto {
                Id = info.Id,
                LeagueName = info.LeagueName,
                DateCreated = info.DateCreated,
                OwnerUserId = info.OwnerUserId, LeagueType = info.LeagueType
            };
            return Ok(dtoInfo);
        }
        catch (Exception e) {
            logger.LogError(e, "Error getting league by name");
        }

        return Ok(null);
    }

    /*
    [HttpGet("names")]
    [ProducesResponseType(typeof(Dictionary<int, string>), StatusCodes.Status200OK)]
    public async Task<ActionResult<Dictionary<int, string>>> GetLeagueNames([FromQuery] int[] leagueIds)
    {
        var leagues = await repo.GetLeagueInfoByIdsAsync(leagueIds);
        var nameMap = leagues.ToDictionary(l => l.Id, l => l.LeagueName);
        return Ok(nameMap);
    }
    */

    // ---------- League Juice ----------
    [HttpGet("{leagueId:int}/juice")]
    [ProducesResponseType(typeof(List<LeagueJuiceMappingDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<LeagueJuiceMappingDto>>> GetLeagueJuice(int leagueId) {
        var mappings = await repo.GetLeagueJuiceMappingAsync(leagueId);
        var dtoMappings = mappings.Select(m => new LeagueJuiceMappingDto {
            LeagueId = m.LeagueId,
            LeagueName = m.League.LeagueName,
            Season = m.Season,
            Juice = m.Juice,
            JuiceDivisional = m.JuiceDivisional,
            JuiceConference = m.JuiceConference,
            WeeklyCost = m.WeeklyCost,
            DateCreated = m.DateCreated
        }).ToList();
        return Ok(dtoMappings);
    }

    [HttpGet("{leagueId:int}/juice/{season:int}")]
    [ProducesResponseType(typeof(LeagueJuiceMappingDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<LeagueJuiceMappingDto?>> GetLeagueJuiceForSeason(int leagueId, int season) {
        var mapping = await repo.GetLeagueJuiceMappingAsync(leagueId, season);
        if (mapping == null) return Ok(null);

        var dtoMapping = new LeagueJuiceMappingDto {
            LeagueId = mapping.LeagueId,
            LeagueName = mapping.League.LeagueName,
            Season = mapping.Season,
            Juice = mapping.Juice,
            JuiceDivisional = mapping.JuiceDivisional,
            JuiceConference = mapping.JuiceConference,
            WeeklyCost = mapping.WeeklyCost,
            DateCreated = mapping.DateCreated
        };
        return Ok(dtoMapping);
    }

    // ---------- League Users / Mappings ----------
    [HttpGet("{leagueId:int}/users")]
    [ProducesResponseType(typeof(List<LeagueUserMappingDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<LeagueUserMappingDto>>> GetLeagueUserMappings(int leagueId) {
        var mappings = await repo.GetLeagueUserMappingsAsync(leagueId);
        var dtoMappings = mappings.Select(m => new LeagueUserMappingDto {
            LeagueId = m.LeagueId,
            LeagueOwnerUserId = m.League.OwnerUserId,
            UserId = m.UserId,
            UserName = m.User.UserName,
            DateCreated = m.DateCreated
        }).ToList();
        return Ok(dtoMappings);
    }

    // The repo method takes IdentityUser; it only uses user.Id, so we can construct a stub.
    [HttpGet("user-mappings/by-user/{userId}")]
    [ProducesResponseType(typeof(List<LeagueUserMappingDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<LeagueUserMappingDto>>> GetLeagueUserMappingsForUser(string userId) {
        var mappings = await repo.GetLeagueUserMappingsAsync(new ApplicationUser() {Id = userId});
        var dtoMappings = mappings.Select(m => new LeagueUserMappingDto {
            Id = m.Id,
            LeagueId = m.LeagueId,
            LeagueOwnerUserId = m.League.OwnerUserId,
            UserId = m.UserId,
            UserName = m.User.UserName,
            DateCreated = m.DateCreated,
            LeagueName = m.League.LeagueName
        }).ToList();
        return Ok(dtoMappings);
    }

    [HttpGet("users")]
    [ProducesResponseType(typeof(List<UserSummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<UserSummaryDto>>> GetUsers() {

        var users = await repo.GetUsersAsync(); // returns List<IdentityUser>
        var usersOutput = new List<UserSummaryDto>();
        foreach (var u in users) {
            // Check if the user is in the Administrator role
            var roles = await userManager.GetRolesAsync(u);
            var isAdmin = roles.Contains("Administrator");

            usersOutput.Add(new UserSummaryDto(u.Id, u.UserName, u.Email, u.EmailConfirmed, isAdmin));

            //Console.WriteLine($"User: {u.Id}, {u.UserName}, {u.Email}, IsAdmin: {isAdmin}");
        }

        return Ok(usersOutput);
    }
    // ---------- NFL Weeks ----------
    [HttpGet("weeks/{season:int}")]
    [ProducesResponseType(typeof(List<NflWeeksDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<NflWeeksDto>>> GetScores(int season) {
        var weeks = await repo.GetNflWeeksAsync(season);
        return Ok(weeks.Select(x => new NflWeeksDto() {Id = x.Id, NflWeek = x.NflWeek, Season = x.Season, 
            StartDate = x.StartDate, EndDate = x.EndDate, DateCreated = x.DateCreated}));
    }
    // ---------- NFL Scores ----------
    [HttpGet("scores/{season:int}/{week:int}")]
    [ProducesResponseType(typeof(List<NflScores>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<NflScores>>> GetScores(int season, int week) {
        var scores = await repo.GetNflScoresAsync(season, week);
        return Ok(scores);
    }

    [HttpGet("scores/{season:int}")]
    [ProducesResponseType(typeof(List<NflScores>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<NflScores>>> GetScoresForSeason(int season) {
        var scores = await repo.GetAllNflScoresForSeasonAsync(season);
        return Ok(scores);
    }

    // Upsert scores
    [HttpPost("scores/upsert")]
    [Authorize(Roles = "Administrator")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> UpsertScores([FromBody] List<NflScores> scores) {

        await repo.UpsertNflScoresAsync(scores);
        return NoContent();
    }

    // Add (bulk) scores
    [HttpPost("scores")]
    [Authorize(Roles = "Administrator")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> AddScores([FromBody] IEnumerable<NflScores> scores) {
        await repo.AddNflScoresAsync(scores);
        return NoContent();
    }

    // Remove (bulk) scores
    [HttpDelete("scores")]
    [Authorize(Roles = "Administrator")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RemoveScores([FromBody] IEnumerable<NflScores> scores) {
        await repo.RemoveNflScoresAsync(scores);
        return NoContent();
    }

    // ---------- NFL Spreads ----------
    [HttpGet("spreads/{season:int}/{week:int}")]
    [ProducesResponseType(typeof(List<NflSpreads>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<NflSpreads>?>> GetSpreads(int season, int week) {
        var spreads = await repo.GetNflSpreadsAsync(season, week);
        if (spreads == null) return Ok(new List<NflSpreads>());

        return Ok(spreads);
    }

    [HttpGet("spreads/{season:int}")]
    [ProducesResponseType(typeof(List<NflSpreads>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<NflSpreads>>> GetSpreadsForSeason(int season) {
        var spreads = await repo.GetAllNflSpreadsForSeasonAsync(season);
        return Ok(spreads);
    }

    // Only-add-new spreads (no duplicates)
    [HttpPost("spreads/add-new")]
    [Authorize(Roles = "Administrator")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> AddNewSpreads([FromBody] List<NflSpreads> spreads) {
        await repo.AddNewNflSpreadsAsync(spreads);
        return NoContent();
    }

    // Add (bulk) spreads
    [HttpPost("spreads")]
    [Authorize(Roles = "Administrator")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> AddSpreads([FromBody] IEnumerable<NflSpreads> spreads) {
        await repo.AddNflSpreadsAsync(spreads);
        return NoContent();
    }

    // Remove (bulk) spreads
    [HttpDelete("spreads")]
    [Authorize(Roles = "Administrator")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RemoveSpreads([FromBody] IEnumerable<NflSpreads> spreads) {
        await repo.RemoveNflSpreadsAsync(spreads);
        return NoContent();
    }

    // ---------- NFL Picks ----------
    [HttpGet("{leagueId:int}/picks/{season:int}/{week:int}")]
    [ProducesResponseType(typeof(List<NflPickDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<NflPickDto>>> GetLeaguePicks(int leagueId, int season, int week) {
        var cacheKey = $"picks_{leagueId}_{season}_{week}";
        var response = await memoryCache.GetOrCreateAsync(cacheKey, async entry => {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
            var picks = await repo.GetNflPicksAsync(leagueId, season, week);
            return picks.Select(p => new NflPickDto {
                Id = p.Id,
                LeagueId = p.LeagueId,
                UserId = p.UserId,
                UserName = p.User.UserName,
                Team = p.Team,
                NflWeek = p.NflWeek,
                Season = p.Season,
                Pick = p.Pick,
                DateCreated = p.DateCreated
            }).ToList();
        });
        return Ok(response);
    }

    [HttpGet("{leagueId:int}/picks/{season:int}/{week:int}/user/{userId}")]
    [ProducesResponseType(typeof(List<NflPickDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<NflPickDto>>> GetUserPicks(string userId, int leagueId, int season, int week) {
        var picks = await repo.GetUserNflPicksAsync(userId, leagueId, season, week);
        var dtoPicks = picks.Select(p => new NflPickDto {
            Id = p.Id,
            LeagueId = p.LeagueId,
            UserId = p.UserId,
            UserName = p.User.UserName,
            Team = p.Team,
            NflWeek = p.NflWeek,
            Season = p.Season,
            Pick = p.Pick,
            DateCreated = p.DateCreated
        }).ToList();
        return Ok(dtoPicks);
    }

    [HttpPost("picks")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult<int>> AddPicks([FromBody] IEnumerable<NflPickDto> picksDto) {
        if (!picksDto.Any())
            return BadRequest("No picks provided");
        var authenticatedUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(authenticatedUserId))
            return Unauthorized();
        var weekId = await repo.GetNflWeeksAsync(picksDto.First().Season);
        var picksList = new List<NflPicks>();
        foreach (var pick in picksDto) {
            var week = weekId.FirstOrDefault(x => x.NflWeek == pick.NflWeek);
            if (week is null)
                return BadRequest("Nfl Week Does Not Exist for the given season");
            picksList.Add((NflPicks)(new NflPicks {
                LeagueId = pick.LeagueId,
                UserId = authenticatedUserId,
                Team = pick.Team,
                Pick = pick.Pick,
                NflWeek = pick.NflWeek,
                Season = pick.Season,
                DateCreated = pick.DateCreated,
                NflWeekId = week.Id
            }));
        }
        if (picksList.Select(x => x.NflWeek).Distinct().Count() > 1)
            return BadRequest("All picks must be for the same NFL week");
        if (picksList.Select(x => x.Season).Distinct().Count() > 1)
            return BadRequest("All picks must be for the same NFL Season");
        if (picksList.Select(x => x.LeagueId).Distinct().Count() > 1)
            return BadRequest("All picks must be for the same League");
        if (picksList.Select(x => x.UserId).Distinct().Count() > 1)
            return BadRequest("All picks must be for the same User");
        // Guard: reject picks for any game that has already kicked off
        var espnScores = await espnCacheService.GetScoresAsync();
        if (espnScores?.Events is not null)
        {
            var allCompetitions = espnScores.Events
                .SelectMany(e => e.Competitions)
                .ToList();
            var now = DateTimeOffset.UtcNow;
            foreach (var pick in picksList)
            {
                var competition = allCompetitions.FirstOrDefault(c =>
                    c.Competitors.Any(comp => string.Equals(comp.Team?.Abbreviation, pick.Team, StringComparison.OrdinalIgnoreCase)));
                if (competition is not null && competition.Date <= now)
                    return BadRequest($"Pick rejected: {pick.Team}'s game has already kicked off.");
            }
        }

        var existingPicks = await repo.GetUserNflPicksAsync(picksList.First().UserId, picksList.First().LeagueId, picksList.First().Season, picksList.First().NflWeek);
        var newPicks = picksList.Except(existingPicks);
        var requiredPicks = GameHelpers.GetRequiredPicks(picksList.First().NflWeek);
        if (newPicks.Count() + existingPicks.Count > requiredPicks)
            return BadRequest($"Too many picks. Maximum allowed for week {picksList.First().NflWeek} is {requiredPicks}");
        await repo.AddNflPicksAsync(newPicks);
        return Ok(newPicks.Count());
    }

    [HttpDelete("picks")]
    [Authorize(Roles = "Administrator")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RemovePicks([FromBody] IEnumerable<NflPickDto> picksDto) {
        var picks = picksDto.Select(dto => new NflPicks {
            Id = dto.Id,
            LeagueId = dto.LeagueId,
            UserId = dto.UserId,
            Team = dto.Team,
            Pick = dto.Pick,
            NflWeek = dto.NflWeek,
            Season = dto.Season,
            DateCreated = dto.DateCreated
        }).ToList();
        await repo.RemoveNflPicksAsync(picks);
        return NoContent();
    }
    
    // ---------- Odds Calculations ----------
    [HttpGet("{leagueId:int}/odds/{season:int}/{week:int}/didUserWin")]
    [ProducesResponseType(typeof(SpreadCalculationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SpreadCalculationResponse>> DidUserWin(
        int leagueId, int season, int week,
        [FromQuery] string team,
        [FromQuery] int pickTeamScore,
        [FromQuery] int otherTeamScore) {
        var calculator = await spreadCalculatorBuilder
            .WithLeagueId(leagueId)
            .WithWeek(week)
            .WithSeason(season)
            .BuildAsync();

        if (!calculator.DoOddsExist())
            return NotFound("No odds available");
        
        return Ok(new SpreadCalculationResponse {
            Team = team,
            IsWinner = calculator.DidUserWinPick(team, pickTeamScore, otherTeamScore),
            Spread = calculator.GetSpread(team),
            Over = calculator.GetOverUnder(team, PickType.Over),
            IsOverWinner = calculator.DidUserWinPick(team, pickTeamScore, otherTeamScore, PickType.Over),
            Under = calculator.GetOverUnder(team, PickType.Under),
            IsUnderWinner = calculator.DidUserWinPick(team, pickTeamScore, otherTeamScore, PickType.Under),
        });
    }

    [HttpGet("{leagueId:int}/odds/{season:int}/{week:int}/team/{team}")]
    [ProducesResponseType(typeof(double?), StatusCodes.Status200OK)]
    public async Task<ActionResult<double?>> GetSpreadForTeam(int leagueId, int season, int week, string team) {
        var calculator = await spreadCalculatorBuilder
            .WithLeagueId(leagueId)
            .WithWeek(week)
            .WithSeason(season)
            .BuildAsync();

        if (!calculator.DoOddsExist())
            return NotFound("No odds available");

        var spread = calculator.GetSpread(team);
        return Ok(spread);
    }
    [HttpGet("{leagueId:int}/odds/{season:int}/{week:int}/team/{team}/overunder")]
    [ProducesResponseType(typeof(double?), StatusCodes.Status200OK)]
    public async Task<ActionResult<double?>> GetOverUnder(int leagueId, int season, int week, string team,
        [FromQuery] string pickType = "Spread") {
        var calculator = await spreadCalculatorBuilder
            .WithLeagueId(leagueId)
            .WithWeek(week)
            .WithSeason(season)
            .BuildAsync();

        if (!calculator.DoOddsExist())
            return NotFound("No odds available");

        var overUnder = calculator.GetOverUnder(team,
            Enum.TryParse<PickType>(pickType, out var type) ? type : PickType.Spread);
        return Ok(overUnder);
    }

    [HttpGet("{leagueId:int}/odds/{season:int}/{week:int}/exists")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    public async Task<ActionResult<bool>> DoOddsExist(int leagueId, int season, int week) {
        var calculator = await spreadCalculatorBuilder
            .WithLeagueId(leagueId)
            .WithWeek(week)
            .WithSeason(season)
            .BuildAsync();

        return Ok(calculator.DoOddsExist());
    }

    [HttpGet("{leagueId:int}/odds/{season:int}/{week:int}/{teamAbbr}")]
    [ProducesResponseType(typeof(double), StatusCodes.Status200OK)]
    public async Task<ActionResult<double>> GetSpread(int leagueId, int season, int week, string teamAbbr) {
        var calculator = await spreadCalculatorBuilder
            .WithLeagueId(leagueId)
            .WithWeek(week)
            .WithSeason(season)
            .BuildAsync();

        return Ok(calculator.GetSpread(teamAbbr));
    }

    [HttpPost("{leagueId:int}/odds/{season:int}/{week:int}")]
    [ProducesResponseType(typeof(BatchSpreadResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BatchSpreadResponse>> GetSpreadBatch(
        int leagueId, int season, int week,
        [FromBody] BatchSpreadRequest request) {
        var calculator = await spreadCalculatorBuilder
            .WithLeagueId(leagueId)
            .WithWeek(week)
            .WithSeason(season)
            .BuildAsync();

        if (!calculator.DoOddsExist())
            return NotFound("No odds available");

        var response = new BatchSpreadResponse();
        foreach (var calc in request.Requests) {
                var key = $"{calc.Team}";

                response.Responses[key] = new SpreadResponse {
                    Team = calc.Team,
                    Spread = calculator.GetSpread(calc.Team),
                    Over = calculator.GetOverUnder(calc.Team, PickType.Over),
                    Under = calculator.GetOverUnder(calc.Team, PickType.Under),
                };
        }

        return Ok(response);
    }

    [HttpPost("{leagueId:int}/odds/{season:int}/{week:int}/calculate-batch")]
    [ProducesResponseType(typeof(BatchSpreadCalculationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BatchSpreadCalculationResponse>> CalculateSpreadBatch(
        int leagueId, int season, int week,
        [FromBody] BatchSpreadCalculationRequest request) {
        var calculator = await spreadCalculatorBuilder
            .WithLeagueId(leagueId)
            .WithWeek(week)
            .WithSeason(season)
            .BuildAsync();

        if (!calculator.DoOddsExist())
            return NotFound("No odds available");

        var response = new BatchSpreadCalculationResponse();

        foreach (var calc in request.Calculations) {
            var key = $"{calc.Team}";

                response.Results[key] = new SpreadCalculationResponse {
                    Team = calc.Team,
                    IsWinner = calculator.DidUserWinPick(calc.Team, calc.PickTeamScore, calc.OtherTeamScore),
                    Spread = calculator.GetSpread(calc.Team),
                    IsOverWinner = calculator.DidUserWinPick(calc.Team, calc.PickTeamScore, calc.OtherTeamScore, PickType.Over),
                    Over = calculator.GetOverUnder(calc.Team, PickType.Over),
                    IsUnderWinner = calculator.DidUserWinPick(calc.Team, calc.PickTeamScore, calc.OtherTeamScore, PickType.Under),
                    Under = calculator.GetOverUnder(calc.Team, PickType.Under)
                };
        }

        return Ok(response);
        //});

        //return response;
    }

    // ---------- Utilities ----------
    [HttpGet("exists/league/{leagueName}/{season:int}")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    public async Task<ActionResult<bool>> LeagueExists(string leagueName, int season)
        => Ok(await repo.LeagueExistsAsync(leagueName, season));

    [HttpGet("exists/league/{leagueName}")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    public async Task<ActionResult<bool>> LeagueExists(string leagueName)
        => Ok(await repo.LeagueExistsAsync(leagueName));

    [HttpGet("exists/user-in-league/{userId}/{leagueId:int}")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    public async Task<ActionResult<bool>> UserExistsInLeague(string userId, int leagueId)
        => Ok(await repo.UserExistsInLeagueAsync(userId, leagueId));

    // ---------- Adds for core entities ----------
    [HttpPost("league-user")]
    [Authorize(Roles = "Administrator")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> AddLeagueUser([FromBody] LeagueUsersDto leagueUserDto) {
        var leagueUser = new LeagueUsers {
            Email = leagueUserDto.Email
        };
        await repo.AddLeagueUserAsync(leagueUser);
        return NoContent();
    }

    [HttpPost("league-user-mapping")]
    [Authorize(Roles = "Administrator")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> AddLeagueUserMapping([FromBody] LeagueUserMappingDto mappingDto) {
        var mapping = new LeagueUserMapping {
            LeagueId = mappingDto.LeagueId,
            UserId = mappingDto.UserId,
            DateCreated = mappingDto.DateCreated, 
        };
        await repo.AddLeagueUserMappingAsync(mapping);
        return NoContent();
    }

    [HttpPost("league-info")]
    [Authorize(Roles = "Administrator")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> AddLeagueInfo([FromBody] LeagueInfoDto leagueInfoDto) {
        var leagueInfo = new LeagueInfo {
            LeagueName = leagueInfoDto.LeagueName,
            DateCreated = leagueInfoDto.DateCreated,
            OwnerUserId = leagueInfoDto.OwnerUserId
        };
        await repo.AddLeagueInfoAsync(leagueInfo);
        return NoContent();
    }

    [HttpPost("league-juice-mapping")]
    [Authorize(Roles = "Administrator")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> AddLeagueJuiceMapping([FromBody] LeagueJuiceMappingDto mappingDto) {
        var mapping = new LeagueJuiceMapping {
            LeagueId = mappingDto.LeagueId,
            Season = mappingDto.Season,
            Juice = mappingDto.Juice,
            JuiceDivisional = mappingDto.JuiceDivisional,
            JuiceConference = mappingDto.JuiceConference,
            WeeklyCost = mappingDto.WeeklyCost,
            DateCreated = mappingDto.DateCreated
        };
        await repo.AddLeagueJuiceMappingAsync(mapping);
        return NoContent();
    }

}