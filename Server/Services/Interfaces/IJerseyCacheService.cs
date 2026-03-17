using System.Collections.Generic;
using System.Threading.Tasks;

namespace FourPlayWebApp.Server.Services.Interfaces;

public interface IJerseyCacheService
{
    // Returns a dictionary mapping team abbreviation -> data URI image for a specific season/week
    Dictionary<string, string>? GetJerseys(int season, int week);

    // Return single team's image data URI (or null if not found) for a specific season/week
    string? GetJerseyByTeam(string teamAbbr, int season, int week);

    // Force an immediate refresh of the cached jerseys for a specific season/week
    Task RefreshAsync(int season, int week);
}
