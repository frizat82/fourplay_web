using FourPlayWebApp.Server.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FourPlayWebApp.Server.UnitTests;

// Every table keys games on our own natural key — (season, week, team) — never an ESPN id (#218).
// CfbRankings.EspnEventId was the last one left behind; this keeps it from coming back anywhere.
public class NoEspnIdsInSchemaTests
{
    [Fact]
    public void NoTableStoresAnEspnColumn()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=unused").Options);

        var espnColumns = db.Model.GetEntityTypes()
            .SelectMany(t => t.GetProperties().Select(p => $"{t.GetTableName() ?? t.Name}.{p.GetColumnName()}"))
            .Where(c => c.Contains("espn", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.Empty(espnColumns);
    }
}
