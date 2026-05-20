using FourPlayWebApp.Server.Models;
using FourPlayWebApp.Server.Models.Data;
using FourPlayWebApp.Server.Models.Identity;
using FourPlayWebApp.Shared.Models.Data;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace FourPlayWebApp.Server.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options) {
    public DbSet<LeagueUsers> LeagueUsers { get; set; }
    public DbSet<LeagueJuiceMapping> LeagueJuiceMapping { get; set; }
    public DbSet<LeagueUserMapping> LeagueUserMapping { get; set; }
    public DbSet<NflPicks> NflPicks { get; set; }
    public DbSet<NflWeeks> NflWeeks { get; set; }
    public DbSet<Invitation> Invitations { get; set; }
    public DbSet<NflSpreads> NflSpreads { get; set; }
    public DbSet<NflScores> NflScores { get; set; }
    public DbSet<LeagueInfo> LeagueInfo { get; set; }
    public DbSet<RefreshToken> RefreshTokens { get; set; }
    public DbSet<CfbSlates> CfbSlates { get; set; }
    public DbSet<CfbSpreads> CfbSpreads { get; set; }
    public DbSet<CfbScores> CfbScores { get; set; }
    public DbSet<CfbPicks> CfbPicks { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder) {
        base.OnModelCreating(modelBuilder);

        // Apply all IEntityTypeConfiguration<T> classes in this assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }

}
