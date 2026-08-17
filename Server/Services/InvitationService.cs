using FourPlayWebApp.Server.Data;
using FourPlayWebApp.Server.Models;
using FourPlayWebApp.Server.Services.Interfaces;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace FourPlayWebApp.Server.Services;

public class InvitationService(IDbContextFactory<ApplicationDbContext> dbContextFactory, IEmailSender emailSender) : IInvitationService {
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

    public async Task<Invitation> CreateInvitationAsync(string email, string invitedByUserId, int? leagueId = null, string? baseUrl = null)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();

        // frizat-9vm: Invitations is unique on (Email, LeagueId), not just Email — the same
        // address can be invited to several different leagues. This is an upsert against that
        // key rather than a blind insert, so re-inviting the same email to the same league
        // refreshes/resends instead of throwing on the duplicate-key violation.
        var existing = await dbContext.Invitations
            .FirstOrDefaultAsync(i => i.Email == email && i.LeagueId == leagueId);

        if (existing is not null && existing.IsUsed) {
            // Already registered via this invite — nothing to refresh or resend, just hand back
            // the existing record so the caller doesn't see this as a failure.
            Log.Information("Invitation for {Email} to league {LeagueId} already used by {UserId} — no-op", email, leagueId, existing.RegisteredUserId);
            return existing;
        }

        Invitation invitation;
        if (existing is not null) {
            invitation = existing;
            invitation.InvitedByUserId = invitedByUserId;
            invitation.ExpiresAt = DateTimeOffset.UtcNow.AddDays(7);
            Log.Information("Invitation for {Email} to league {LeagueId} refreshed by {UserId}", email, leagueId, invitedByUserId);
        } else {
            invitation = new Invitation {
                Email = email,
                InvitedByUserId = invitedByUserId,
                LeagueId = leagueId,
                CreatedAt = DateTimeOffset.UtcNow,
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(7)
            };
            dbContext.Invitations.Add(invitation);
            Log.Information("Invitation created for {Email} by {UserId}", email, invitedByUserId);
        }

        await dbContext.SaveChangesAsync();

        if (!string.IsNullOrWhiteSpace(baseUrl)) {
            try {
                await SendInvitationEmailAsync(invitation, baseUrl);
            } catch (Exception ex) {
                // Invitation was created/refreshed successfully; a failed email send must not undo it.
                Log.Error(ex, "Failed to send invitation email to {Email}", email);
            }
        }

        return invitation;
    }

    public async Task ResendInvitationEmailAsync(int invitationId, string baseUrl) {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var invitation = await dbContext.Invitations.FindAsync(invitationId);
        if (invitation == null) {
            Log.Warning("Cannot resend invitation {Id} - not found", invitationId);
            return;
        }
        await SendInvitationEmailAsync(invitation, baseUrl);
    }

    private Task SendInvitationEmailAsync(Invitation invitation, string baseUrl) {
        // Path must match InvitationsPage.tsx's getInviteUrl (the "copy link" button) — nothing
        // enforces the two staying in sync, so update both if this route ever changes.
        var registrationUrl = $"{baseUrl.TrimEnd('/')}/account/register?inviteCode={Uri.EscapeDataString(invitation.InvitationCode)}&returnUrl=%2F";
        var body = GoogleEmailSender.CreateTemplatedBody(
            "You're Invited to Join!",
            $"""
             <p>Hello,</p>
             <p>You've been invited to join IV League. Click the button below to create your account and get started.</p>
             <div style="text-align:center;margin:24px 0;">
               <a href="{registrationUrl}" style="display:inline-block;background-color:#4f46e5;color:#fff;text-decoration:none;padding:14px 30px;border-radius:6px;font-weight:bold;">
                 Create Your Account
               </a>
             </div>
             <p>If you didn't request this invite, you can safely ignore this email.</p>
             """);
        return emailSender.SendEmailAsync(invitation.Email, "IV League Invitation", body);
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

    public async Task<List<Invitation>> GetInvitationsByLeagueAsync(int leagueId)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();

        return await dbContext.Invitations
            .Where(i => i.LeagueId == leagueId)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync();
    }
}
