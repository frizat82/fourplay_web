using FourPlayWebApp.Server.Auth;
using FourPlayWebApp.Server.Controllers;
using Microsoft.AspNetCore.Authorization;
using System.Reflection;

namespace FourPlayWebApp.Server.UnitTests;

/// <summary>
/// Regression tests for Issue #2: bulk-data endpoints must require Administrator role.
/// These tests fail before the fix (only [Authorize] present) and pass after
/// [Authorize(Roles = "Administrator")] is applied.
/// </summary>
public class AuthorizationTests
{
    // Every destructive or bulk-write endpoint on LeagueController that any
    // authenticated user could previously invoke.
    public static TheoryData<string> BulkEndpoints =>
    [
        nameof(LeagueController.UpsertScores),
        nameof(LeagueController.AddScores),
        nameof(LeagueController.RemoveScores),
        nameof(LeagueController.AddSpreads),
        nameof(LeagueController.AddNewSpreads),
        nameof(LeagueController.RemoveSpreads),
        nameof(LeagueController.RemovePicks),
        // frizat-8n3: these were missing [Authorize(Roles = "Administrator")]
        nameof(LeagueController.AddLeagueUserMapping),
        nameof(LeagueController.AddLeagueInfo),
        nameof(LeagueController.AddLeagueJuiceMapping),
        // security review: email addresses exposed to any authenticated user
        nameof(LeagueController.GetUsers),
        // My Leagues consolidation: cross-league visibility is admin-only
        nameof(LeagueController.GetAllLeagues),
    ];

    [Theory]
    [MemberData(nameof(BulkEndpoints))]
    public void BulkEndpoint_HasAuthorizeAttribute_WithAdministratorRole(string methodName)
    {
        var method = typeof(LeagueController).GetMethod(methodName);
        Assert.NotNull(method);

        // The method itself must declare [Authorize(Roles = "Administrator")].
        // Controller-level [Authorize] without Roles is NOT sufficient.
        var attr = method.GetCustomAttributes<AuthorizeAttribute>()
                         .FirstOrDefault(a => a.Roles is not null);

        Assert.NotNull(attr);  // fails before fix — no per-method Authorize present
        Assert.Equal("Administrator", attr.Roles);
    }

    /// <summary>
    /// Guard: the class-level [Authorize] must still be present so non-bulk
    /// endpoints remain authenticated-only.
    /// </summary>
    [Fact]
    public void LeagueController_HasClassLevel_AuthorizeAttribute()
    {
        var attr = typeof(LeagueController).GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(attr);
    }
}

/// <summary>
/// The jersey endpoints scrape an outside site and image-process every game on a cache miss, with
/// no negative caching or single-flight — anonymous access let anyone make the server do that on
/// demand (and risk the upstream blocking us). The feature is switched off in the UI pending a
/// copyright review, so the controller is admin-only until it's re-enabled. Supersedes frizat-uvi,
/// which locked only RefreshCache and kept the GETs anonymous — but a GET miss runs the same scrape.
/// </summary>
public class JerseysControllerAuthorizationTests
{
    [Fact]
    public void JerseysController_IsAdminOnly_AndNoActionAllowsAnonymous()
    {
        var attr = typeof(JerseysController).GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(attr);
        Assert.Equal(AppRoles.Administrator, attr.Roles);

        Assert.Null(typeof(JerseysController).GetCustomAttribute<AllowAnonymousAttribute>());
        foreach (var method in typeof(JerseysController).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            Assert.Null(method.GetCustomAttribute<AllowAnonymousAttribute>());
    }
}
