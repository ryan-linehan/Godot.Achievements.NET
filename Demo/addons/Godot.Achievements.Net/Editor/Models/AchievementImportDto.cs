#if TOOLS
using System.Text.Json.Serialization;

namespace Godot.Achievements.Core.Editor;

/// <summary>
/// DTO for importing achievements from JSON
/// </summary>
public class AchievementImportDto
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("steamId")]
    public string? SteamId { get; set; }

    [JsonPropertyName("gameCenterId")]
    public string? GameCenterId { get; set; }

    [JsonPropertyName("googlePlayId")]
    public string? GooglePlayId { get; set; }

    [JsonPropertyName("isIncremental")]
    public bool? IsIncremental { get; set; }

    [JsonPropertyName("maxProgress")]
    public int? MaxProgress { get; set; }

    [JsonPropertyName("iconPath")]
    public string? IconPath { get; set; }
}
#endif
