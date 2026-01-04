#if TOOLS
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Godot.Achievements.Core.Editor;

/// <summary>
/// Wrapper for JSON format: { "achievements": [...] }
/// </summary>
public class AchievementImportWrapper
{
    [JsonPropertyName("achievements")]
    public List<AchievementImportDto>? Achievements { get; set; }
}
#endif
