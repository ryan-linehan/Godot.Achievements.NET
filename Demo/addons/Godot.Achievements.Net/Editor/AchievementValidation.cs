#if TOOLS
using System.Collections.Generic;
using Godot;
using Godot.Achievements.Core;

namespace Godot.Achievements.Core.Editor;

/// <summary>
/// Represents validation warnings for a single achievement
/// </summary>
public class AchievementValidationResult
{
    public Achievement Achievement { get; }
    public List<string> Warnings { get; } = new();

    public AchievementValidationResult(Achievement achievement)
    {
        Achievement = achievement;
    }

    public bool HasWarnings => Warnings.Count > 0;

    public void AddWarning(string warning)
    {
        Warnings.Add(warning);
    }

    public string GetTooltipText()
    {
        if (!HasWarnings) return string.Empty;
        return string.Join("\n", Warnings);
    }
}

/// <summary>
/// Validates achievements against enabled platform integrations
/// </summary>
public static class AchievementValidator
{
    private const string STEAM_ENABLED_SETTING = "addons/achievements/platforms/steam_enabled";
    private const string GAMECENTER_ENABLED_SETTING = "addons/achievements/platforms/gamecenter_enabled";
    private const string GOOGLEPLAY_ENABLED_SETTING = "addons/achievements/platforms/googleplay_enabled";

    /// <summary>
    /// Validate a single achievement and return validation result with warnings
    /// </summary>
    public static AchievementValidationResult ValidateAchievement(Achievement achievement)
    {
        var result = new AchievementValidationResult(achievement);

        // Check for missing internal ID
        if (string.IsNullOrWhiteSpace(achievement.Id))
        {
            result.AddWarning("Missing internal ID");
        }

        // Check for missing display name
        if (string.IsNullOrWhiteSpace(achievement.DisplayName))
        {
            result.AddWarning("Missing display name");
        }

        // Check platform IDs based on enabled integrations
        if (GetPlatformEnabled(STEAM_ENABLED_SETTING) && string.IsNullOrWhiteSpace(achievement.SteamId))
        {
            result.AddWarning("Steam integration enabled but Steam ID is missing");
        }

        if (GetPlatformEnabled(GAMECENTER_ENABLED_SETTING) && string.IsNullOrWhiteSpace(achievement.GameCenterId))
        {
            result.AddWarning("Game Center integration enabled but Game Center ID is missing");
        }

        if (GetPlatformEnabled(GOOGLEPLAY_ENABLED_SETTING) && string.IsNullOrWhiteSpace(achievement.GooglePlayId))
        {
            result.AddWarning("Google Play integration enabled but Google Play ID is missing");
        }

        return result;
    }

    /// <summary>
    /// Validate all achievements in a database
    /// </summary>
    public static Dictionary<Achievement, AchievementValidationResult> ValidateDatabase(AchievementDatabase database)
    {
        var results = new Dictionary<Achievement, AchievementValidationResult>();

        if (database?.Achievements == null)
            return results;

        // First pass: run individual validations
        foreach (var achievement in database.Achievements)
        {
            var validationResult = ValidateAchievement(achievement);
            results[achievement] = validationResult;
        }

        // Second pass: check for duplicate platform IDs within each provider
        CheckDuplicatePlatformIds(database, results);

        return results;
    }

    /// <summary>
    /// Check for duplicate platform IDs within each provider and add warnings
    /// </summary>
    private static void CheckDuplicatePlatformIds(AchievementDatabase database, Dictionary<Achievement, AchievementValidationResult> results)
    {
        // Track platform IDs to detect duplicates (only check if platform is enabled)
        var steamIds = new Dictionary<string, List<Achievement>>();
        var gameCenterIds = new Dictionary<string, List<Achievement>>();
        var googlePlayIds = new Dictionary<string, List<Achievement>>();

        bool steamEnabled = GetPlatformEnabled(STEAM_ENABLED_SETTING);
        bool gameCenterEnabled = GetPlatformEnabled(GAMECENTER_ENABLED_SETTING);
        bool googlePlayEnabled = GetPlatformEnabled(GOOGLEPLAY_ENABLED_SETTING);

        // Collect all platform IDs
        foreach (var achievement in database.Achievements)
        {
            if (steamEnabled && !string.IsNullOrWhiteSpace(achievement.SteamId))
            {
                if (!steamIds.ContainsKey(achievement.SteamId))
                    steamIds[achievement.SteamId] = new List<Achievement>();
                steamIds[achievement.SteamId].Add(achievement);
            }

            if (gameCenterEnabled && !string.IsNullOrWhiteSpace(achievement.GameCenterId))
            {
                if (!gameCenterIds.ContainsKey(achievement.GameCenterId))
                    gameCenterIds[achievement.GameCenterId] = new List<Achievement>();
                gameCenterIds[achievement.GameCenterId].Add(achievement);
            }

            if (googlePlayEnabled && !string.IsNullOrWhiteSpace(achievement.GooglePlayId))
            {
                if (!googlePlayIds.ContainsKey(achievement.GooglePlayId))
                    googlePlayIds[achievement.GooglePlayId] = new List<Achievement>();
                googlePlayIds[achievement.GooglePlayId].Add(achievement);
            }
        }

        // Add warnings for duplicates
        foreach (var kvp in steamIds)
        {
            if (kvp.Value.Count > 1)
            {
                foreach (var achievement in kvp.Value)
                {
                    results[achievement].AddWarning($"Duplicate Steam ID '{kvp.Key}' (shared with {kvp.Value.Count - 1} other achievement(s))");
                }
            }
        }

        foreach (var kvp in gameCenterIds)
        {
            if (kvp.Value.Count > 1)
            {
                foreach (var achievement in kvp.Value)
                {
                    results[achievement].AddWarning($"Duplicate Game Center ID '{kvp.Key}' (shared with {kvp.Value.Count - 1} other achievement(s))");
                }
            }
        }

        foreach (var kvp in googlePlayIds)
        {
            if (kvp.Value.Count > 1)
            {
                foreach (var achievement in kvp.Value)
                {
                    results[achievement].AddWarning($"Duplicate Google Play ID '{kvp.Key}' (shared with {kvp.Value.Count - 1} other achievement(s))");
                }
            }
        }
    }

    private static bool GetPlatformEnabled(string settingKey)
    {
        if (ProjectSettings.HasSetting(settingKey))
        {
            return ProjectSettings.GetSetting(settingKey).AsBool();
        }
        return false;
    }
}
#endif
