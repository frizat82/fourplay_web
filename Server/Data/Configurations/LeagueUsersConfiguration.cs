using FourPlayWebApp.Server.Models.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FourPlayWebApp.Server.Data.Configurations;

public class LeagueUsersConfiguration : IEntityTypeConfiguration<LeagueUsers>
{
    public void Configure(EntityTypeBuilder<LeagueUsers> entity)
    {
        entity.HasKey(e => e.Id);
        entity.HasIndex(e => e.Email).IsUnique();
    }
}
