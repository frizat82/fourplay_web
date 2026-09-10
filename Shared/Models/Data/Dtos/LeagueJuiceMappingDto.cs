namespace FourPlayWebApp.Shared.Models.Data.Dtos;

public class LeagueJuiceMappingDto
{
    public int Id { get; set; }
    public int LeagueId { get; set; }
    public string LeagueName { get; set; } = string.Empty;
    public int Season { get; set; }
    public int Juice { get; set; }
    public int JuiceDivisional { get; set; }
    public int JuiceConference { get; set; }
    public int WeeklyCost { get; set; }
    // Which week/slate this league's season effectively starts at (default 1 = no exclusion,
    // today's behavior for every existing league) — frizat-o3x. Shares TeaseLocked below: it
    // locks at the same season-start boundary as Juice/JuiceDivisional/JuiceConference.
    public int StartWeek { get; set; } = 1;
    public DateTimeOffset DateCreated { get; set; } = DateTimeOffset.UtcNow;
    // Tease points (Juice/JuiceDivisional/JuiceConference) lock once the season's first week/slate
    // has started; WeeklyCost locks separately, once the season's LAST week/slate (Super Bowl /
    // Championship) has started — see LeagueController.UpdateLeagueJuice and
    // LeagueJuiceScheduleSource.GetSeasonStartLockTimeUtc/GetSeasonEndLockTimeUtc.
    public bool TeaseLocked { get; set; }
    public bool WeeklyCostLocked { get; set; }
}
