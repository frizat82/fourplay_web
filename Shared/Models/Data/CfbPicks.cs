using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
using FourPlayWebApp.Shared.Models.Enum;

namespace FourPlayWebApp.Shared.Models.Data;

[ExcludeFromCodeCoverage]
public class CfbPicks {
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public int LeagueId { get; set; }
    public int CfbSlateId { get; set; }
    public string Team { get; set; } = string.Empty;
    // Reuses NflPicks' PickType enum — same set of values, same concept, no reason for a
    // separate CFB-only type.
    public PickType PickType { get; set; } = PickType.Spread;
    public int Season { get; set; }
    public DateTimeOffset DateCreated { get; set; } = DateTimeOffset.UtcNow;
}
