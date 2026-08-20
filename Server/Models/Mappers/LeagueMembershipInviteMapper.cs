using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Shared.Models.Data.Dtos;

namespace FourPlayWebApp.Server.Models.Mappers;

public static class LeagueMembershipInviteMapper
{
    public static PendingMembershipInviteDto ToPendingDto(this LeagueMembershipInvite invite)
    {
        return new PendingMembershipInviteDto(
            invite.Id,
            invite.LeagueId,
            invite.League?.LeagueName ?? string.Empty,
            invite.InvitedByUser?.UserName,
            invite.CreatedAt
        );
    }

    public static List<PendingMembershipInviteDto> ToPendingDtoList(this IEnumerable<LeagueMembershipInvite> invites)
    {
        return invites.Select(i => i.ToPendingDto()).ToList();
    }

    public static MembershipInviteStatusDto ToStatusDto(this LeagueMembershipInvite invite)
    {
        return new MembershipInviteStatusDto(
            invite.Id,
            invite.LeagueId,
            invite.InvitedUser?.Email ?? string.Empty,
            invite.InvitedUser?.UserName,
            invite.Status,
            invite.CreatedAt,
            invite.RespondedAt
        );
    }

    public static List<MembershipInviteStatusDto> ToStatusDtoList(this IEnumerable<LeagueMembershipInvite> invites)
    {
        return invites.Select(i => i.ToStatusDto()).ToList();
    }
}
