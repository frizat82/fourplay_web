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

        // Revoke any existing active links for this league
        var existing = await db.LeagueInviteLinks
            .Where(l => l.LeagueId == leagueId && !l.IsRevoked)
            .ToListAsync();
        foreach (var old in existing)
            old.IsRevoked = true;

        var link = new LeagueInviteLink
        {
            LeagueId = leagueId,
            CreatedByUserId = createdByUserId,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(24),
        };
        db.LeagueInviteLinks.Add(link);
        await db.SaveChangesAsync();

        // Reload with navigation property
        return await db.LeagueInviteLinks
            .Include(l => l.League)
            .FirstAsync(l => l.Id == link.Id);
    }

    public async Task<LeagueInviteLink?> ValidateAsync(string token)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        var link = await db.LeagueInviteLinks
            .Include(l => l.League)
            .FirstOrDefaultAsync(l => l.Token == token);

        return link is { IsValid: true } ? link : null;
    }
}
