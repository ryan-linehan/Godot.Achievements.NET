#if TOOLS
using System.Collections.Generic;
using Godot.Achievements.Core;

namespace Godot.Achievements.Core.Editor;

/// <summary>
/// Represents validation warnings for a single achievement
/// </summary>
public class AchievementValidationResult
{
    public Achievement Achievement { get; }
    public List<string> Warnings { get; } = new();

    /// <summary>
    /// Field-specific warnings keyed by ValidationFields constants
    /// </summary>
    public Dictionary<string, ValidationWarningType> FieldWarnings { get; } = new();

    public AchievementValidationResult(Achievement achievement)
    {
        Achievement = achievement;
    }

    public bool HasWarnings => Warnings.Count > 0;

    public void AddWarning(string warning)
    {
        Warnings.Add(warning);
    }

    /// <summary>
    /// Add a warning associated with a specific field
    /// </summary>
    public void AddFieldWarning(string fieldKey, ValidationWarningType warningType, string message)
    {
        FieldWarnings[fieldKey] = warningType;
        Warnings.Add(message);
    }

    public string GetTooltipText()
    {
        if (!HasWarnings) return string.Empty;
        return string.Join("\n", Warnings);
    }
}
#endif
