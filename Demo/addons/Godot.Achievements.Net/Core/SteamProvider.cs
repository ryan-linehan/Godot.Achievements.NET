namespace Godot.Achievements.Core;

/// <summary>
/// Specifies which Steam library to use for achievement integration.
/// </summary>
public enum SteamProvider
{
    /// <summary>
    /// Use Godot.Steamworks.NET (default) - requires the Godot.Steamworks.NET addon.
    /// https://github.com/ryan-linehan/Godot.Steamworks.NET
    /// </summary>
    GodotSteamworksNet = 0,

    /// <summary>
    /// Use GodotSteam with C# bindings - requires GodotSteam GDExtension and C# bindings.
    /// https://godotsteam.com/
    /// https://github.com/LauraWebdev/GodotSteam_CSharpBindings
    /// </summary>
    GodotSteam = 1
}
