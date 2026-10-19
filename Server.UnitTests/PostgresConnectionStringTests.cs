using FourPlayWebApp.Server.Infrastructure;
using Npgsql;

namespace FourPlayWebApp.Server.UnitTests;

public class PostgresConnectionStringTests {
    // Neon and local Docker Postgres never use Kerberos. Left at Npgsql's default (Prefer), the first
    // connection tries to load libgssapi_krb5, which the .NET 10 (Ubuntu) runtime image doesn't
    // ship — an error-level line on every boot for a feature we can't use. Both forms opt out.
    [Theory]
    [InlineData("postgres://user:pw@ep-x.neon.tech/neondb?sslmode=require")]
    [InlineData("Host=localhost;Port=5432;Username=fourplay;Password=pw;Database=fourplay_dev")]
    public void Normalize_DisablesGssEncryption(string raw) {
        var built = new NpgsqlConnectionStringBuilder(PostgresConnectionString.Normalize(raw));

        Assert.Equal(GssEncryptionMode.Disable, built.GssEncryptionMode);
    }

    [Fact]
    public void Normalize_ConvertsAPostgresUrl_ToKeyValueForm() {
        var built = new NpgsqlConnectionStringBuilder(
            PostgresConnectionString.Normalize("postgres://user:p%40ss@ep-x.neon.tech:5433/neondb?sslmode=require"));

        Assert.Equal(("ep-x.neon.tech", 5433, "user", "p@ss", "neondb", SslMode.Require),
            (built.Host, built.Port, built.Username, built.Password, built.Database, built.SslMode));
    }

    [Fact]
    public void Normalize_KeepsAKeyValueStringsOwnSettings() {
        var built = new NpgsqlConnectionStringBuilder(
            PostgresConnectionString.Normalize("Host=localhost;Port=5432;Username=fourplay;Password=pw;Database=fourplay_dev;Pooling=false"));

        Assert.Equal(("localhost", "fourplay_dev", false), (built.Host, built.Database, built.Pooling));
    }
}
