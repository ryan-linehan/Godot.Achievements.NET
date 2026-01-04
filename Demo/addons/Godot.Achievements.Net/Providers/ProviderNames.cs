namespace Godot.Achievements.Providers;

/// <summary>
/// Constants for built-in provider names.
/// Use these instead of magic strings when referencing providers by name.
/// </summary>
public static class ProviderNames
{
    /// <summary>
    /// Local provider that persists to user://achievements.json
    /// </summary>
    public const string Local = "Local";

    /// <summary>
    /// Steam (Steamworks) provider for PC/Desktop platforms
    /// </summary>
    public const string Steam = "Steam";

    /// <summary>
    /// Apple Game Center provider for iOS/macOS
    /// </summary>
    public const string GameCenter = "Game Center";

    /// <summary>
    /// Google Play Games provider for Android
    /// </summary>
    public const string GooglePlay = "Google Play Games";
}
