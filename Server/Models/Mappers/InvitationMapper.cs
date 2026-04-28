using FourPlayWebApp.Shared.Models.Data.Dtos;

namespace FourPlayWebApp.Server.Models.Mappers;

public static class InvitationMapper
{
    public static InvitationDto ToDto(this Invitation invitation)
    {
        return new InvitationDto
        {
            Id = invitation.Id,
            InvitationCode = invitation.InvitationCode,
            Email = invitation.Email,
            InvitedByUserId = invitation.InvitedByUserId,
            InvitedByUserName = invitation.InvitedByUser?.UserName, // from ApplicationUser
            CreatedAt = invitation.CreatedAt,
            ExpiresAt = invitation.ExpiresAt,
            IsUsed = invitation.IsUsed,
            UsedAt = invitation.UsedAt,
            RegisteredUserId = invitation.RegisteredUserId,
            RegisteredUserName = invitation.RegisteredUser?.UserName, // from ApplicationUser
            IsExpired = invitation.IsExpired,
            IsValid = invitation.IsValid,
            LeagueId = invitation.LeagueId,
            LeagueName = invitation.League?.LeagueName
        };
    }

    public static List<InvitationDto> ToDtoList(this IEnumerable<Invitation> invitations)
    {
        return invitations.Select(i => i.ToDto()).ToList();
    }
}
