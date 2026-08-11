using FourPlayWebApp.Server.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FourPlayWebApp.Server.Data;

// dotnet ef has no lightweight way to get a DbContext for a top-level-statement Program.cs — by
// default it boots the entire app pipeline (JWT config, email config, CORS validation, the
// startup migration check) just to construct one. That's fragile for CI/migration-only runs
// (needs a pile of unrelated dummy env vars, and would even trip the startup fail-fast check
// this same PR adds). This factory is the standard EF Core answer: dotnet ef design-time tooling
// prefers this over booting Program.cs when present, and it only needs a connection string.
public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var raw = Environment.GetEnvironmentVariable("ConnectionStrings__POSTGRES_CONNECTION_STRING")
            ?? throw new InvalidOperationException(
                "ConnectionStrings__POSTGRES_CONNECTION_STRING must be set to run EF Core design-time commands (migrations, database update).");

        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseNpgsql(PostgresConnectionString.Normalize(raw));
        return new ApplicationDbContext(optionsBuilder.Options);
    }
}
