using FourPlayWebApp.Server.Data;
using FourPlayWebApp.Server.Models;
using FourPlayWebApp.Server.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace FourPlayWebApp.Server.Services;

public class InvitationService(IDbContextFactory<ApplicationDbContext> dbContextFactory) : IInvitationService {
    public async Task DeleteInvitationAsync(int id) {

        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var invitation = await dbContext.Invitations.FindAsync(id);
        if (invitation == null) {
            Log.Warning("Invitation with ID {Id} not found", id);
            return;
        }

        dbContext.Invitations.Remove(invitation);
        await dbContext.SaveChangesAsync();
        Log.Information("Invitation with ID {Id} deleted", id);
    }

    public async Task<Invitation> CreateInvitationAsync(string email, string invitedByUserId, int? leagueId = null)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();

        var invitation = new Invitation
        {
            Email = email,
            InvitedByUserId = invitedByUserId,
            LeagueId = leagueId,
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7)
        };

        dbContext.Invitations.Add(invitation);
        await dbContext.SaveChangesAsync();

        Log.Information("Invitation created for {Email} by {UserId}", email, invitedByUserId);

        return invitation;
    }

    public async Task<Invitation?> ValidateInvitationAsync(string code)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();

        var invitation = await dbContext.Invitations
            .Include(i => i.League)
            .FirstOrDefaultAsync(i => i.InvitationCode == code);

        if (invitation == null)
        {
            Log.Information("Invitation with code {Code} not found", code);
            return null;
        }

        if (invitation.IsUsed)
        {
            Log.Information("Invitation with code {Code} has already been used", code);
            return null;
        }

        if (invitation.ExpiresAt >= DateTimeOffset.UtcNow) return invitation;
        Log.Information("Invitation with code {Code} has expired", code);
        return null;

    }

    public async Task<bool> MarkInvitationAsUsedAsync(string code, string registeredUserId)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();

        var invitation = await dbContext.Invitations
            .FirstOrDefaultAsync(i => i.InvitationCode == code);

        if (invitation == null || invitation.IsUsed || invitation.ExpiresAt < DateTimeOffset.UtcNow)
        {
            Log.Warning("Cannot mark invitation {Code} as used - invalid state", code);
            return false;
        }

        invitation.IsUsed = true;
        invitation.UsedAt = DateTimeOffset.UtcNow;
        invitation.RegisteredUserId = registeredUserId;

        await dbContext.SaveChangesAsync();
        Log.Information("Invitation {Code} marked as used by user {UserId}", code, registeredUserId);

        return true;
    }

    public async Task<List<Invitation>> GetAllInvitationsAsync()
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();

        return await dbContext.Invitations
            .Include(i => i.InvitedByUser)
            .Include(i => i.RegisteredUser)
            .Include(i => i.League)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<Invitation>> GetInvitationsByUserAsync(string userId)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();

        return await dbContext.Invitations
            .Where(i => i.RegisteredUserId == userId)
            .Include(i => i.RegisteredUser)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync();
    }
}
