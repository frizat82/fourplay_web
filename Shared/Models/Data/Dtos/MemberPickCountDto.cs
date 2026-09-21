namespace FourPlayWebApp.Shared.Models.Data.Dtos;

/// <summary>
/// How many picks one member has SUBMITTED for a week/slate — a count and nothing else.
/// Deliberately carries no team or pick type: this backs the commissioner's "who still needs to
/// pick" view, and a league owner is also a player, so they must never learn which teams a member
/// picked before kickoff (the picks endpoints hide that until the game starts). The count queries
/// select only UserId, so a team is never even loaded on this path. Don't add fields that identify
/// a pick — MemberPickCountDto_CarriesCountsOnly guards it.
/// </summary>
public class MemberPickCountDto
{
    public string UserId { get; set; } = string.Empty;
    public int PickCount { get; set; }
}
