using FourPlayWebApp.Server.Data;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Testcontainers.PostgreSql;
using Xunit;

namespace FourPlayWebApp.Server.UnitTests;

/// <summary>
/// A migrated Postgres container shared by every test in <see cref="PostgresCollection"/> — for
/// behaviour only Postgres can prove: advisory locks, and how Npgsql translates bulk
/// ExecuteUpdate/ExecuteDelete (CLAUDE.md: SQLite passing proves nothing there). Tests in the
/// collection run one at a time and each works on its own rows.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime {
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithCleanUp(true)
        .Build();

    public string ConnectionString { get; private set; } = "";

    public async Task InitializeAsync() {
        await _container.StartAsync();
        ConnectionString = _container.GetConnectionString();
        await using var db = OpenDb();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();

    public ApplicationDbContext OpenDb() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(ConnectionString)
            .Options);

    /// <summary>A fresh DbContext per call (EF contexts aren't thread-safe), all on this container.</summary>
    public IDbContextFactory<ApplicationDbContext> Factory() {
        var factory = Substitute.For<IDbContextFactory<ApplicationDbContext>>();
        factory.CreateDbContextAsync().Returns(_ => OpenDb());
        return factory;
    }
}

[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture> {
    public const string Name = "Postgres";
}
