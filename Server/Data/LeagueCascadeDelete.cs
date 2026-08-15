using Microsoft.EntityFrameworkCore;

namespace FourPlayWebApp.Server.Data;

/// <summary>
/// Shared by LeagueRepository.DeleteLeagueAsync (single league, owner/admin-initiated) and
/// DemoDataSeeder.PurgeUnknownLeaguesAsync (bulk, startup hygiene) — one place for the ordered
/// multi-table cascade so a future FK added off LeagueInfo only needs updating here, not in two
/// independently-drifting copies. Explicit RemoveRange rather than relying solely on the DB's own
/// Cascade FKs, so this stays correct against providers that don't enforce them (e.g. EF Core's
/// UseInMemoryDatabase in tests).
/// </summary>
internal static class LeagueCascadeDelete {
    public static void RemoveLeaguesAndDependents(ApplicationDbContext db, IReadOnlyCollection<int> leagueIds) {
        if (leagueIds.Count == 0) return;
        db.NflPicks.RemoveRange(db.NflPicks.Where(p => leagueIds.Contains(p.LeagueId)));
        db.CfbPicks.RemoveRange(db.CfbPicks.Where(p => leagueIds.Contains(p.LeagueId)));
        db.Invitations.RemoveRange(db.Invitations.Where(i => i.LeagueId != null && leagueIds.Contains(i.LeagueId.Value)));
        db.LeagueJuiceMapping.RemoveRange(db.LeagueJuiceMapping.Where(m => leagueIds.Contains(m.LeagueId)));
        db.LeagueUserMapping.RemoveRange(db.LeagueUserMapping.Where(m => leagueIds.Contains(m.LeagueId)));
        db.LeagueInfo.RemoveRange(db.LeagueInfo.Where(l => leagueIds.Contains(l.Id)));
    }
}
