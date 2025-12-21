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
    [Export(PropertyHint.MultilineText)] public string CustomPlatformIds { get; set; } = string.Empty;

    // Runtime state (managed by LocalProvider, not exported)
    public bool IsUnlocked { get; set; }
    public DateTime? UnlockedAt { get; set; }
    public float Progress { get; set; } // 0.0 to 1.0

    /// <summary>
    /// Get a platform-specific ID from the CustomPlatformIds JSON
    /// </summary>
    public string? GetPlatformId(string platform)
    {
        if (string.IsNullOrEmpty(CustomPlatformIds))
            return null;

        var json = new Json();
        var error = json.Parse(CustomPlatformIds);
        if (error != Error.Ok)
            return null;

        var dict = json.Data.AsGodotDictionary<string, string>();
        return dict.TryGetValue(platform, out var id) ? id : null;
    }

    /// <summary>
    /// Set a platform-specific ID in the CustomPlatformIds JSON
    /// </summary>
    public void SetPlatformId(string platform, string id)
    {
        Godot.Collections.Dictionary<string, string> dict;

        if (string.IsNullOrEmpty(CustomPlatformIds))
        {
            dict = new Godot.Collections.Dictionary<string, string>();
        }
        else
        {
            var json = new Json();
            var error = json.Parse(CustomPlatformIds);
            if (error != Error.Ok)
                dict = new Godot.Collections.Dictionary<string, string>();
            else
                dict = json.Data.AsGodotDictionary<string, string>();
        }

        dict[platform] = id;
        CustomPlatformIds = Json.Stringify(dict);
    }
}
