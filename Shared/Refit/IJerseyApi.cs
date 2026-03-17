using System.Collections.Generic;
using Refit;

namespace FourPlayWebApp.Shared.Refit;

public interface IJerseyApi
{
    // Returns a dictionary mapping team abbreviation -> data-uri image for a specific season/week
    [Get("/api/jerseys/{season}/{week}")]
    Task<Dictionary<string, string>?> GetAllJerseys(int season, int week);

    // Returns the data-uri for a single team (by abbreviation) for a specific season/week
    [Get("/api/jerseys/{season}/{week}/{teamAbbr}")]
    Task<string?> GetJerseyByTeam(int season, int week, string teamAbbr);
}
