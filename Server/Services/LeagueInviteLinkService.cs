using FourPlayWebApp.Server.Data;
using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FourPlayWebApp.Server.Services;

public class LeagueInviteLinkService(IDbContextFactory<ApplicationDbContext> dbContextFactory)
    : ILeagueInviteLinkService
{
    public async Task<LeagueInviteLink> GenerateAsync(int leagueId, string createdByUserId)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();

        await db.LeagueInviteLinks
            .Where(l => l.LeagueId == leagueId && !l.IsRevoked)
            .ExecuteUpdateAsync(s => s.SetProperty(l => l.IsRevoked, true));

        var link = new LeagueInviteLink
        {
            LeagueId = leagueId,
            CreatedByUserId = createdByUserId,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(24),
        };
        db.LeagueInviteLinks.Add(link);
        await db.SaveChangesAsync();

        return link;
    }

    public async Task<LeagueInviteLink?> ValidateAsync(string token)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        return await db.LeagueInviteLinks
            .Include(l => l.League)
            .Where(l => !l.IsRevoked && l.ExpiresAt > DateTimeOffset.UtcNow)
            .FirstOrDefaultAsync(l => l.Token == token);
    }
}
