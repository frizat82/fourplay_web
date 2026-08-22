using FourPlayWebApp.Server.Data;
using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Services.Interfaces;
using FourPlayWebApp.Shared.Models.Enum;
using Microsoft.EntityFrameworkCore;

namespace FourPlayWebApp.Server.Services;

public class LeagueMembershipInviteService(IDbContextFactory<ApplicationDbContext> dbContextFactory)
    : ILeagueMembershipInviteService
{
    public async Task CreateOrReopenAsync(int leagueId, string invitedUserId, string invitedByUserId)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();

        // Unique on (LeagueId, InvitedUserId) — mirrors InvitationService.CreateInvitationAsync's
        // upsert: re-inviting someone who previously declined resets them back to Pending instead
        // of throwing on the duplicate-key violation.
        var existing = await db.LeagueMembershipInvites
            .FirstOrDefaultAsync(i => i.LeagueId == leagueId && i.InvitedUserId == invitedUserId);

        if (existing is not null) {
            existing.Status = MembershipInviteStatus.Pending;
            existing.InvitedByUserId = invitedByUserId;
            existing.CreatedAt = DateTimeOffset.UtcNow;
            existing.RespondedAt = null;
            await db.SaveChangesAsync();
            return;
        }

        db.LeagueMembershipInvites.Add(new LeagueMembershipInvite {
            LeagueId = leagueId,
            InvitedUserId = invitedUserId,
            InvitedByUserId = invitedByUserId,
        });
        try {
            await db.SaveChangesAsync();
        } catch (DbUpdateException) {
            // TOCTOU: a concurrent request already inserted this exact (LeagueId, InvitedUserId)
            // pair between our read and write — same race TryAddUserToLeagueAsync (LeagueController)
            // guards against. Their row lands as Pending by default (the entity's default value),
            // which is exactly the state we wanted too, so there's nothing left to do.
        }
    }

    // No includes — every caller (accept/decline ownership + status checks) only reads scalar
    // columns (InvitedUserId, LeagueId, Status). Callers that need League or InvitedByUser fetch
    // them separately (see CancelMembershipInvite's repo.GetLeagueInfoAsync call).
    public async Task<LeagueMembershipInvite?> GetByIdAsync(int id)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        return await db.LeagueMembershipInvites.FirstOrDefaultAsync(i => i.Id == id);
    }

    public async Task<List<LeagueMembershipInvite>> GetPendingForUserAsync(string userId)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        return await db.LeagueMembershipInvites
            .Where(i => i.InvitedUserId == userId && i.Status == MembershipInviteStatus.Pending)
            .Include(i => i.League)
            .Include(i => i.InvitedByUser)
            // Order by Id, not CreatedAt — SQLite (used in tests) can't translate ORDER BY on
            // DateTimeOffset; Id increases monotonically with insertion order anyway. See
            // LeagueInviteLinkService.GetCurrentAsync for the same workaround.
            .OrderByDescending(i => i.Id)
            .ToListAsync();
    }

    // Only InvitedUser is included — the sole caller (ToStatusDtoList, for the commissioner-side
    // status table) reads InvitedUser.Email/UserName and never touches League or InvitedByUser.
    public async Task<List<LeagueMembershipInvite>> GetByLeagueAsync(int leagueId)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        return await db.LeagueMembershipInvites
            .Where(i => i.LeagueId == leagueId)
            .Include(i => i.InvitedUser)
            .OrderByDescending(i => i.Id)
            .ToListAsync();
    }

    public Task MarkAcceptedAsync(int id) => SetStatusAsync(id, MembershipInviteStatus.Accepted);

    public Task MarkDeclinedAsync(int id) => SetStatusAsync(id, MembershipInviteStatus.Declined);

    private async Task SetStatusAsync(int id, MembershipInviteStatus status)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        var invite = await db.LeagueMembershipInvites.FindAsync(id);
        if (invite is null) return;
        invite.Status = status;
        invite.RespondedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync();
        var invite = await db.LeagueMembershipInvites.FindAsync(id);
        if (invite is null) return;
        db.LeagueMembershipInvites.Remove(invite);
        await db.SaveChangesAsync();
    }
}
