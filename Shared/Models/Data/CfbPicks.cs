using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace FourPlayWebApp.Shared.Models.Data;

[ExcludeFromCodeCoverage]
public class CfbPicks {
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public int LeagueId { get; set; }
    public int CfbSlateId { get; set; }
    public int EspnEventId { get; set; }
    public string Team { get; set; } = string.Empty;
    public string PickType { get; set; } = "Spread"; // "Spread" | "Over" | "Under"
    public int Season { get; set; }
    public DateTimeOffset DateCreated { get; set; } = DateTimeOffset.UtcNow;
}
