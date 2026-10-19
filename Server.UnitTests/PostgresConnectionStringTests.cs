using FourPlayWebApp.Server.Infrastructure;
using Npgsql;

namespace FourPlayWebApp.Server.UnitTests;

public class PostgresConnectionStringTests {
    private static NpgsqlConnectionStringBuilder Built(string raw) => new(PostgresConnectionString.Normalize(raw));

    // Kerberos is never used (see PostgresConnectionString.Normalize) — both input forms opt out.
    [Theory]
    [InlineData("postgres://user:pw@ep-x.neon.tech/neondb?sslmode=require")]
    [InlineData("Host=localhost;Port=5432;Username=fourplay;Password=pw;Database=fourplay_dev")]
    public void Normalize_DisablesGssEncryption(string raw) {
        Assert.Equal(GssEncryptionMode.Disable, Built(raw).GssEncryptionMode);
    }

    // ...unless a connection string explicitly asks for something other than the default.
    [Fact]
    public void Normalize_KeepsAnExplicitGssEncryptionMode() {
        Assert.Equal(GssEncryptionMode.Require,
            Built("Host=localhost;Username=u;Password=pw;Database=db;GSS Encryption Mode=Require").GssEncryptionMode);
    }

    [Fact]
    public void Normalize_ConvertsAPostgresUrl_ToKeyValueForm() {
        var built = Built("postgres://user:p%40ss@ep-x.neon.tech:5433/neondb?sslmode=require");

        Assert.Equal(("ep-x.neon.tech", 5433, "user", "p@ss", "neondb", SslMode.Require),
            (built.Host, built.Port, built.Username, built.Password, built.Database, built.SslMode));
    }

    // URL-encoded credentials may hold characters that delimit a key=value string (';', '=', '@').
    [Fact]
    public void Normalize_DecodesUrlCredentials_AndKeepsDelimitersIntact() {
        var built = Built("postgresql://my%40user:p%3Bw%3Dd@ep-x.neon.tech/neondb?sslmode=require");

        Assert.Equal(("my@user", "p;w=d", "neondb"), (built.Username, built.Password, built.Database));
    }

    // Neon's sslmode → Npgsql's SSL Mode (a missing one means Prefer).
    [Theory]
    [InlineData("?sslmode=require", SslMode.Require)]
    [InlineData("?sslmode=verify-ca", SslMode.VerifyCA)]
    [InlineData("?sslmode=verify-full", SslMode.VerifyFull)]
    [InlineData("?sslmode=disable", SslMode.Disable)]
    [InlineData("", SslMode.Prefer)]
    public void Normalize_MapsUrlSslMode(string query, SslMode mode) {
        Assert.Equal(mode, Built("postgres://u:pw@ep-x.neon.tech/neondb" + query).SslMode);
    }

    [Fact]
    public void Normalize_KeepsAKeyValueStringsOwnSettings() {
        var built = Built("Host=localhost;Port=5433;Username=fourplay;Password=\"a;b\";Database=fourplay_dev;Pooling=false;SSL Mode=Require");

        Assert.Equal(("localhost", 5433, "a;b", "fourplay_dev", false, SslMode.Require),
            (built.Host, built.Port, built.Password, built.Database, built.Pooling, built.SslMode));
    }
}
