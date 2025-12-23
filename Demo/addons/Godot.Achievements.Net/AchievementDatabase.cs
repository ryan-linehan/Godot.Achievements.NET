using System;
using System.Linq;

namespace Godot.Achievements.Core;

/// <summary>
/// Database of achievements - configured in the editor and saved as a resource file
/// </summary>
[Tool]
[GlobalClass]
public partial class AchievementDatabase : Resource
{
    [Export] public Godot.Collections.Array<Achievement> Achievements { get; set; } = new();

    /// <summary>
    /// Get an achievement by its ID
    /// </summary>
    public Achievement? GetById(string id)
    {
        return Achievements.FirstOrDefault(a => a.Id == id);
    }

    /// <summary>
    /// Add a new achievement to the database
    /// </summary>
    public void AddAchievement(Achievement achievement)
    {
        if (GetById(achievement.Id) != null)
        {
            GD.PushWarning($"Achievement with ID '{achievement.Id}' already exists");
            return;
        }

        Achievements.Add(achievement);
    }

    /// <summary>
    /// Remove an achievement from the database
    /// </summary>
    public bool RemoveAchievement(string id)
    {
        var achievement = GetById(id);
        if (achievement == null)
            return false;

        Achievements.Remove(achievement);
        return true;
    }

    /// <summary>
    /// Validate the database for duplicate IDs and missing required fields
    /// </summary>
    public string[] Validate()
    {
        var errors = new System.Collections.Generic.List<string>();

        // Check for duplicate IDs
        var duplicateIds = Achievements
            .GroupBy(a => a.Id)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key);

        foreach (var id in duplicateIds)
        {
            errors.Add($"Duplicate achievement ID: '{id}'");
        }

        // Check for missing required fields
        for (int i = 0; i < Achievements.Count; i++)
        {
            var achievement = Achievements[i];

            if (string.IsNullOrWhiteSpace(achievement.Id))
                errors.Add($"Achievement at index {i} has no ID");

            if (string.IsNullOrWhiteSpace(achievement.DisplayName))
                errors.Add($"Achievement '{achievement.Id}' has no display name");

            if (string.IsNullOrWhiteSpace(achievement.Description))
                errors.Add($"Achievement '{achievement.Id}' has no description");
        }

        return errors.ToArray();
    }
}
