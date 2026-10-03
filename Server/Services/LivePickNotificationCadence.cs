namespace FourPlayWebApp.Server.Services;

/// <summary>
/// At the instant a game goes live (0-0), one side of a spread pick is already mathematically
/// bloody or covering purely from the spread's sign — a trivial artifact, not a real event. A pick
/// is left alone until QuietWindow past its game's kickoff; the first cover-state captured at or
/// after that point is a silent baseline (no push), and only a transition after that notifies.
/// </summary>
public static class LivePickNotificationCadence
{
    public static readonly TimeSpan QuietWindow = TimeSpan.FromMinutes(20);
}
