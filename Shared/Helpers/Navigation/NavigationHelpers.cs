using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components;
using Serilog;

namespace FourPlayWebApp.Shared.Helpers.Navigation;

public static class NavigationHelpers {
    public static async Task HandleFirstRenderAsync(int leagueId, NavigationManager navigation, Action setInitialized) {
        if (leagueId == 0) {
            navigation.NavigateTo("/leagues");
        }
        else {
            setInitialized();
            // Add any additional logic that needs to be executed after initialization
            await Task.CompletedTask;
        }
    }
    public static async Task<int> HandleLeagueCookie(NavigationManager navigationManager,
        ILocalStorageService localStorage, bool redirectToHome = true)
    {
        try {
            var leagueId = await localStorage.GetItemAsync<int>("leagueId");
            Log.Information("LeagueId: {LeagueId}", leagueId);
            if (leagueId != 0) return leagueId;
            Log.Information("LeagueId is 0, removing from local storage");
            await localStorage.RemoveItemAsync("leagueId");
            if (!redirectToHome) return leagueId;
            Log.Warning("Redirecting to home page due to missing leagueId");
            navigationManager.RedirectTo("/");

            return leagueId;
        }
        catch (Exception ex) {
            Log.Error(ex, "Error getting leagueId");
            if (redirectToHome) {
                Log.Warning("Redirecting to home page due to missing leagueId");
                navigationManager.RedirectTo("/");
            }
        }

        return 0;
    }

    public static void RedirectTo(this NavigationManager navigationManager, string? uri) {
        uri ??= "/";

        // Prevent open redirects.
        if (!Uri.IsWellFormedUriString(uri, UriKind.Relative)) {
            uri = navigationManager.ToBaseRelativePath(uri);
        }

        navigationManager.NavigateTo(uri);
    }

    public static void RedirectTo(this NavigationManager navigationManager, string uri, Dictionary<string, object?> queryParameters)
    {
        var uriWithoutQuery = navigationManager.ToAbsoluteUri(uri).GetLeftPart(UriPartial.Path);
        var newUri = navigationManager.GetUriWithQueryParameters(uriWithoutQuery, queryParameters);
        navigationManager.RedirectTo(newUri);
    }
}
