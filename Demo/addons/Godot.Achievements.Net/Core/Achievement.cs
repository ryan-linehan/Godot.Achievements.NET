using System;
using Godot.Collections;
namespace Godot.Achievements.Core;

/// <summary>
/// Achievement definition resource that can be edited in the Godot editor
/// </summary>
[Tool]
[GlobalClass]
public partial class Achievement : Resource
{
    /// <summary>
    /// Unique identifier for the achievement
    /// </summary>
    [Export]
    public string Id { get; set; } = string.Empty;
    /// <summary>
    /// Display name of the achievement
    /// </summary>
    [Export]
    public string DisplayName { get; set; } = string.Empty;
    /// <summary>
    /// Description of the achievement
    /// </summary>
    [Export(PropertyHint.MultilineText)]
    public string Description { get; set; } = string.Empty;
    /// <summary>
    /// Icon representing the achievement
    /// </summary>
    [Export] public Texture2D? Icon { get; set; }
    /// <summary>
    /// Platform-specific ID for steam
    /// </summary>
    [Export]
    public string SteamId { get; set; } = string.Empty;
    /// <summary>
    /// Steam stat key for tracking progress on incremental achievements.
    /// In Steam, stats and achievements have separate keys.
    /// Leave empty to use SteamId as the stat key (only works if configured identically in Steamworks).
    /// </summary>
    [Export]
    public string SteamStatId { get; set; } = string.Empty;
    /// <summary>
    /// Platform-specific ID for Game Center (iOS/macOS)
    /// </summary>
    [Export]
    public string GameCenterId { get; set; } = string.Empty;
    /// <summary>
    /// Platform-specific ID for Google Play (Android)
    /// </summary>
    [Export]
    public string GooglePlayId { get; set; } = string.Empty;
    /// <summary>
    /// Extra properties for extensibility
    /// </summary>
    [Export]
    public Dictionary<string, Variant> ExtraProperties { get; set; } = new();
    /// <summary>
    /// Whether the achievement is incremental (a counting achievement)
    /// </summary>
    [Export]
    public bool IsIncremental { get; set; } = false;
    /// <summary>
    /// Maximum progress value for incremental achievements
    /// </summary>
    [Export] public int MaxProgress { get; set; } = 1;

    /// <summary>
    /// Whether the achievement is unlocked (managed at runtime thus not exported)
    /// </summary>
    public bool IsUnlocked { get; set; }
    /// <summary>
    /// Timestamp when the achievement was unlocked (managed at runtime thus not exported)
    /// </summary>
    public DateTime? UnlockedAt { get; set; }
    /// <summary>
    /// Current progress value for incremental achievements (managed at runtime thus not exported)
    /// </summary>
    public int CurrentProgress { get; set; }
    /// <summary>
    /// Get progress as a percentage (0.0 to 1.0)
    /// </summary>
    public float ProgressPercentage => MaxProgress > 0 ? (float)CurrentProgress / MaxProgress : 0f;

}
