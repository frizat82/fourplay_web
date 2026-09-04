using FourPlayWebApp.Server.Models.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FourPlayWebApp.Server.Data.Configurations;

public class CfbRankingConfiguration : IEntityTypeConfiguration<CfbRanking>
{
    public void Configure(EntityTypeBuilder<CfbRanking> entity)
    {
        entity.HasKey(e => e.Id);
        entity.HasIndex(e => new { e.Season, e.EspnWeekNumber, e.TeamAbbreviation }).IsUnique();
    }
}
