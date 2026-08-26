namespace FourPlayWebApp.Server.Services.Repositories.Interfaces;

// Shared write/read contract for both sports' spread tables (frizat CLAUDE.md: NFL/CFB are
// siblings). Each sport keeps its own entity type and natural key, but both are held to the same
// contract — insert if new on UpsertAsync, never a blind duplicate insert, and expose which
// season/week keys already have data for the catch-up scheduler (TimedTriggerScheduler) to read.
//
// NOTE on UpsertAsync's "update in place" half: the two implementations intentionally differ on
// what happens when a row already exists with a non-blank spread. CfbRepository always refreshes
// every field (a re-fetch always wins). LeagueRepository (NFL) only overwrites if the existing
// spread is still 0/0 — a deliberate pre-existing guard against a bad automated re-fetch
// clobbering a real line. This interface intentionally does not force one policy; implementers
// choose it, but must document it here if it changes.
public interface ISpreadRepository<TSpread> {
    Task UpsertAsync(IEnumerable<TSpread> spreads);
    Task<HashSet<(int Season, int Week)>> GetWeeksWithSpreadDataAsync();
}
