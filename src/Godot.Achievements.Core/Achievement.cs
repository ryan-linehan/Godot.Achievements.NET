using System;

namespace Godot.Achievements.Core;

/// <summary>
/// Achievement definition resource that can be edited in the Godot editor
/// </summary>
[GlobalClass]
public partial class Achievement : Resource
{
    // Display info
    [Export] public string Id { get; set; } = string.Empty;
    [Export] public string DisplayName { get; set; } = string.Empty;
    [Export(PropertyHint.MultilineText)] public string Description { get; set; } = string.Empty;
    [Export] public Texture2D? Icon { get; set; }
    [Export] public bool Hidden { get; set; }

    // Platform ID mappings (built-in)
    [Export] public string SteamId { get; set; } = string.Empty;
    [Export] public string GameCenterId { get; set; } = string.Empty;
    [Export] public string GooglePlayId { get; set; } = string.Empty;

    // Custom platform metadata (for third-party providers)
    [Export] public Godot.Collections.Dictionary<string, string> CustomPlatformIds { get; set; } = new();

    // Runtime state (managed by LocalProvider, not exported)
    public bool IsUnlocked { get; set; }
    public DateTime? UnlockedAt { get; set; }
    public float Progress { get; set; } // 0.0 to 1.0

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
