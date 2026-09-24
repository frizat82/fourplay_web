using FourPlayWebApp.Server.Data;
using FourPlayWebApp.Server.Models.Identity;
using FourPlayWebApp.Server.Services;
using Microsoft.EntityFrameworkCore;

namespace FourPlayWebApp.Server.UnitTests;

/// <summary>
/// Regression tests for Issue #11: RefreshTokenService injects ApplicationDbContext
/// directly. After fix it uses IDbContextFactory. These tests verify existing
/// behaviour is preserved through the refactor.
/// </summary>
public class RefreshTokenServiceTests
{
    private static DbContextFactoryStub BuildFactory(string? dbName = null) =>
        new(dbName ?? "RefreshTokenTest_" + Guid.NewGuid());

    private static ApplicationUser BuildUser(string id = "user-1") =>
        new() { Id = id, UserName = "testuser", Email = "test@example.com" };

    // ── IssueTokenAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task IssueTokenAsync_ReturnsToken_AndPersistsToDb()
    {
        var factory  = BuildFactory();
        var service  = new RefreshTokenService(factory);
        var user     = BuildUser();

        var token = await service.IssueTokenAsync(user, TimeSpan.FromDays(14));

        Assert.NotNull(token.Token);
        Assert.NotEmpty(token.Token);
        Assert.Equal(user.Id, token.UserId);
        Assert.True(token.Expires > DateTime.UtcNow);
        Assert.Equal(1, factory.CreateDbContext().RefreshTokens.Count());
    }

    [Fact]
    public async Task IssueTokenAsync_GeneratesUniqueTokensEachCall()
    {
        var factory = BuildFactory();
        var service = new RefreshTokenService(factory);
        var user    = BuildUser();

        var t1 = await service.IssueTokenAsync(user, TimeSpan.FromDays(14));
        var t2 = await service.IssueTokenAsync(user, TimeSpan.FromDays(14));

        Assert.NotEqual(t1.Token, t2.Token);
    }

    // ── ValidateTokenAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task ValidateTokenAsync_ValidToken_ReturnsRefreshToken()
    {
        var factory = BuildFactory();
        var service = new RefreshTokenService(factory);
        var user    = BuildUser();
        var db      = factory.CreateDbContext();
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var issued = await service.IssueTokenAsync(user, TimeSpan.FromDays(14));

        var validated = await service.ValidateTokenAsync(issued.Token);

        Assert.NotNull(validated);
        Assert.Equal(issued.Token, validated.Token);
    }

    [Fact]
    public async Task ValidateTokenAsync_ExpiredToken_ReturnsNull()
    {
        var factory = BuildFactory();
        var service = new RefreshTokenService(factory);
        var user    = BuildUser();

        // Issue with negative lifetime → already expired
        var issued = await service.IssueTokenAsync(user, TimeSpan.FromSeconds(-1));

        var result = await service.ValidateTokenAsync(issued.Token);

        Assert.Null(result);
    }

    [Fact]
    public async Task ValidateTokenAsync_RevokedToken_ReturnsNull()
    {
        var factory = BuildFactory();
        var service = new RefreshTokenService(factory);
        var user    = BuildUser();
        var db      = factory.CreateDbContext();
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var issued = await service.IssueTokenAsync(user, TimeSpan.FromDays(14));
        await service.RevokeTokenAsync(issued.Token);

        var result = await service.ValidateTokenAsync(issued.Token);

        Assert.Null(result);
    }

    [Fact]
    public async Task ValidateTokenAsync_UnknownToken_ReturnsNull()
    {
        var factory = BuildFactory();
        var service = new RefreshTokenService(factory);

        var result = await service.ValidateTokenAsync("not-a-real-token");

        Assert.Null(result);
    }

    // ── RotateTokenAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task RotateTokenAsync_ValidToken_RevokesOldAndReturnsNew()
    {
        var factory = BuildFactory();
        var service = new RefreshTokenService(factory);
        var user    = BuildUser();
        var db      = factory.CreateDbContext();
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var original = await service.IssueTokenAsync(user, TimeSpan.FromDays(14));
        var rotated  = await service.RotateTokenAsync(original.Token, user, TimeSpan.FromDays(14));

        Assert.NotNull(rotated);
        Assert.NotEqual(original.Token, rotated.Token);

        // Old token must now be revoked
        var oldInDb = factory.CreateDbContext().RefreshTokens.First(rt => rt.Token == original.Token);
        Assert.NotNull(oldInDb.Revoked);
    }

    [Fact]
    public async Task RotateTokenAsync_AlreadyRevoked_ReturnsNull()
    {
        var factory = BuildFactory();
        var service = new RefreshTokenService(factory);
        var user    = BuildUser();
        var db      = factory.CreateDbContext();
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var original = await service.IssueTokenAsync(user, TimeSpan.FromDays(14));
        await service.RevokeTokenAsync(original.Token);

        var result = await service.RotateTokenAsync(original.Token, user, TimeSpan.FromDays(14));

        Assert.Null(result);
    }

    // ── RevokeTokenAsync / RevokeAllUserTokensAsync ───────────────────────────

    [Fact]
    public async Task RevokeTokenAsync_SetsRevokedTimestamp()
    {
        var factory = BuildFactory();
        var service = new RefreshTokenService(factory);
        var user    = BuildUser();

        var issued = await service.IssueTokenAsync(user, TimeSpan.FromDays(14));
        await service.RevokeTokenAsync(issued.Token);

        var inDb = factory.CreateDbContext().RefreshTokens.First(rt => rt.Token == issued.Token);
        Assert.NotNull(inDb.Revoked);
    }

    [Fact]
    public async Task RevokeAllUserTokensAsync_RevokesAllActiveTokensForUser()
    {
        var factory = BuildFactory();
        var service = new RefreshTokenService(factory);
        var user    = BuildUser("user-multi");

        await service.IssueTokenAsync(user, TimeSpan.FromDays(14));
        await service.IssueTokenAsync(user, TimeSpan.FromDays(14));
        await service.IssueTokenAsync(user, TimeSpan.FromDays(14));

        await service.RevokeAllUserTokensAsync(user.Id);

        var allRevoked = factory.CreateDbContext().RefreshTokens
            .Where(rt => rt.UserId == user.Id)
            .All(rt => rt.Revoked != null);

        Assert.True(allRevoked);
    }
}
