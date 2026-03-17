using AppAny.Quartz.EntityFrameworkCore.Migrations;
using AppAny.Quartz.EntityFrameworkCore.Migrations.PostgreSQL;
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

    protected override void OnModelCreating(ModelBuilder modelBuilder) {
        base.OnModelCreating(modelBuilder);

        // Quartz.NET
        modelBuilder.AddQuartz(builder => builder.UsePostgreSql());

        // Apply all IEntityTypeConfiguration<T> classes in this assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }

}
