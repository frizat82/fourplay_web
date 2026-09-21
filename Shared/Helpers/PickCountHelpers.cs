using FourPlayWebApp.Shared.Models.Data.Dtos;

namespace FourPlayWebApp.Shared.Helpers;

/// <summary>Shared by LeagueController (NFL) and CfbPicksController (CFB) — one count, not a copy per sport (frizat-xbq).</summary>
public static class PickCountHelpers
{
    /// <summary>Submitted picks per user. A user with no picks has no entry (callers default to 0).</summary>
    public static List<MemberPickCountDto> CountByUser(IEnumerable<string> userIds) =>
        userIds
            .GroupBy(id => id)
            .Select(g => new MemberPickCountDto { UserId = g.Key, PickCount = g.Count() })
            .ToList();
}
