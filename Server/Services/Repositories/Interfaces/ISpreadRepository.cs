namespace FourPlayWebApp.Server.Services.Repositories.Interfaces;

// Shared write contract for both sports' spread tables (frizat CLAUDE.md: NFL/CFB are siblings).
// Each sport keeps its own entity type and natural key, but both are held to the same upsert
// contract — insert if new, update in place if it already exists, never a blind duplicate insert.
public interface ISpreadRepository<TSpread> {
    Task UpsertAsync(IEnumerable<TSpread> spreads);
}
