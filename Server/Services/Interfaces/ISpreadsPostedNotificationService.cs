using FourPlayWebApp.Shared.Models.Enum;

namespace FourPlayWebApp.Server.Services.Interfaces;

// One shared implementation for both sports — called by NflSpreadJob/CfbSpreadJob right after a
// week's spreads are saved.
public interface ISpreadsPostedNotificationService
{
    /// <summary>
    /// Pushes "spreads are up" for this sport's week to every user who opted in, once per week.
    /// Never throws: the spreads are already saved, and a failed push must not fail the spread job.
    /// </summary>
    /// <returns>How many users were notified.</returns>
    Task<int> NotifyAsync(LeagueType sport, int season, int week, string weekLabel, DateTime spreadLockUtc);
}
