using System;
using Godot.Collections;
namespace Godot.Achievements.Core;

/// <summary>
/// Achievement definition resource that can be edited in the Godot editor
/// </summary>
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
    /// Platform-specific ID for Game Center (iOS/macOS)
    /// </summary>
    [Export]
    public string GameCenterId { get; set; } = string.Empty;
    /// <summary>
    /// Platform-specific ID for Google Play (Android)
    /// </summary>
    [Export]
    public string GooglePlayId { get; set; } = string.Empty;
    // Custom platform metadata (for third-party providers)
    [Export]
    public Dictionary<string, string> CustomPlatformIds { get; set; } = new();
    /// <summary>
    /// Extra properties for extensibility
    /// </summary>
    [Export]
    public Dictionary<string, Variant> ExtraProperties { get; set; } = new();

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

    /// <summary>
    /// Get a platform-specific ID from the CustomPlatformIds dictionary
    /// </summary>
    public string? GetPlatformId(string platform)
    {
        return CustomPlatformIds.TryGetValue(platform, out var id) ? id : null;
    }

    /// <summary>
    /// Set a platform-specific ID in the CustomPlatformIds dictionary
    /// </summary>
    public void SetPlatformId(string platform, string id)
    {
        CustomPlatformIds[platform] = id;
    }
}
