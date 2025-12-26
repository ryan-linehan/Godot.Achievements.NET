#if TOOLS
using System;
using System.Text.Json;

namespace Godot.Achievements.Core.Editor;

/// <summary>
/// Result of an import operation
/// </summary>
public readonly struct ImportResult
{
    public bool Success { get; }
    public string? ErrorMessage { get; }
    public int ImportedCount { get; }
    public int UpdatedCount { get; }
    public int SkippedCount { get; }

    private ImportResult(bool success, string? errorMessage, int imported, int updated, int skipped)
    {
        Success = success;
        ErrorMessage = errorMessage;
        ImportedCount = imported;
        UpdatedCount = updated;
        SkippedCount = skipped;
    }

    public static ImportResult SuccessResult(int imported, int updated, int skipped)
        => new(true, null, imported, updated, skipped);

    public static ImportResult FailureResult(string errorMessage)
        => new(false, errorMessage, 0, 0, 0);

    public string GetSummary()
        => $"New: {ImportedCount}, Updated: {UpdatedCount}, Skipped: {SkippedCount}";
}

/// <summary>
/// Result of an export operation
/// </summary>
public readonly struct ExportResult
{
    public bool Success { get; }
    public string? ErrorMessage { get; }
    public int ExportedCount { get; }

    private ExportResult(bool success, string? errorMessage, int exported)
    {
        Success = success;
        ErrorMessage = errorMessage;
        ExportedCount = exported;
    }

    public static ExportResult SuccessResult(int exported)
        => new(true, null, exported);

    public static ExportResult FailureResult(string errorMessage)
        => new(false, errorMessage, 0);
}

/// <summary>
/// Static utility class for importing and exporting achievements to/from CSV and JSON formats
/// </summary>
public static class AchievementImportExport
{
    #region CSV Operations

    /// <summary>
    /// Import achievements from a CSV file into the database
    /// </summary>
    public static ImportResult ImportFromCSV(AchievementDatabase database, string path)
    {
        try
        {
            var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
            if (file == null)
                return ImportResult.FailureResult($"Failed to open file: {FileAccess.GetOpenError()}");

            // Use Godot's built-in CSV parsing
            var header = file.GetCsvLine();
            if (header.Length == 0)
            {
                file.Close();
                return ImportResult.FailureResult("CSV file is empty.");
            }

            // Build column index map from header
            var columnMap = new System.Collections.Generic.Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < header.Length; i++)
            {
                columnMap[header[i].Trim()] = i;
            }

            // Validate required columns
            if (!columnMap.ContainsKey("Id"))
            {
                file.Close();
                return ImportResult.FailureResult("CSV must contain an 'Id' column.");
            }

            int importedCount = 0;
            int updatedCount = 0;
            int skippedCount = 0;

            // Process data rows using Godot's GetCsvLine()
            while (!file.EofReached())
            {
                var values = file.GetCsvLine();
                if (values.Length == 0 || (values.Length == 1 && string.IsNullOrWhiteSpace(values[0])))
                    continue;

                var id = GetCSVValue(values, columnMap, "Id");
                if (string.IsNullOrWhiteSpace(id))
                    continue;

                // Check if achievement already exists
                var existing = database.GetById(id);
                bool isNew = existing == null;

                var achievement = existing ?? new Achievement { Id = id };
                bool hasChanges = false;

                // Update fields from CSV, tracking if any changes occur
                if (columnMap.ContainsKey("DisplayName"))
                {
                    var newValue = GetCSVValue(values, columnMap, "DisplayName") ?? achievement.DisplayName;
                    if (achievement.DisplayName != newValue)
                    {
                        achievement.DisplayName = newValue;
                        hasChanges = true;
                    }
                }
                if (columnMap.ContainsKey("Description"))
                {
                    var newValue = GetCSVValue(values, columnMap, "Description") ?? achievement.Description;
                    if (achievement.Description != newValue)
                    {
                        achievement.Description = newValue;
                        hasChanges = true;
                    }
                }
                if (columnMap.ContainsKey("SteamId"))
                {
                    var newValue = GetCSVValue(values, columnMap, "SteamId") ?? achievement.SteamId;
                    if (achievement.SteamId != newValue)
                    {
                        achievement.SteamId = newValue;
                        hasChanges = true;
                    }
                }
                if (columnMap.ContainsKey("GameCenterId"))
                {
                    var newValue = GetCSVValue(values, columnMap, "GameCenterId") ?? achievement.GameCenterId;
                    if (achievement.GameCenterId != newValue)
                    {
                        achievement.GameCenterId = newValue;
                        hasChanges = true;
                    }
                }
                if (columnMap.ContainsKey("GooglePlayId"))
                {
                    var newValue = GetCSVValue(values, columnMap, "GooglePlayId") ?? achievement.GooglePlayId;
                    if (achievement.GooglePlayId != newValue)
                    {
                        achievement.GooglePlayId = newValue;
                        hasChanges = true;
                    }
                }
                if (columnMap.ContainsKey("IsIncremental"))
                {
                    var newValue = GetCSVValue(values, columnMap, "IsIncremental")?.ToLower() == "true";
                    if (achievement.IsIncremental != newValue)
                    {
                        achievement.IsIncremental = newValue;
                        hasChanges = true;
                    }
                }
                if (columnMap.ContainsKey("MaxProgress"))
                {
                    if (int.TryParse(GetCSVValue(values, columnMap, "MaxProgress"), out int maxProgress))
                    {
                        if (achievement.MaxProgress != maxProgress)
                        {
                            achievement.MaxProgress = maxProgress;
                            hasChanges = true;
                        }
                    }
                }

                if (isNew)
                {
                    achievement.ExtraProperties = new Godot.Collections.Dictionary<string, Variant>();
                    database.AddAchievement(achievement);
                    importedCount++;
                }
                else if (hasChanges)
                {
                    updatedCount++;
                }
                else
                {
                    skippedCount++;
                }
            }

            file.Close();

            AchievementLogger.Log(AchievementLogger.Areas.Editor, $"Imported CSV from {path}: {importedCount} new, {updatedCount} updated, {skippedCount} skipped");
            return ImportResult.SuccessResult(importedCount, updatedCount, skippedCount);
        }
        catch (Exception ex)
        {
            AchievementLogger.Error(AchievementLogger.Areas.Editor, $"CSV import error: {ex}");
            return ImportResult.FailureResult($"Failed to import CSV: {ex.Message}");
        }
    }

    /// <summary>
    /// Export achievements from the database to a CSV file
    /// </summary>
    public static ExportResult ExportToCSV(AchievementDatabase database, string path)
    {
        if (database.Achievements.Count == 0)
            return ExportResult.FailureResult("No achievements to export.");

        try
        {
            var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
            if (file == null)
                return ExportResult.FailureResult($"Failed to create file: {FileAccess.GetOpenError()}");

            // Write header using Godot's StoreCsvLine
            file.StoreCsvLine(new string[] { "Id", "DisplayName", "Description", "SteamId", "GameCenterId", "GooglePlayId", "IsIncremental", "MaxProgress" });

            // Write achievement rows using Godot's StoreCsvLine (handles escaping automatically)
            foreach (var achievement in database.Achievements)
            {
                file.StoreCsvLine(new string[]
                {
                    achievement.Id ?? "",
                    achievement.DisplayName ?? "",
                    achievement.Description ?? "",
                    achievement.SteamId ?? "",
                    achievement.GameCenterId ?? "",
                    achievement.GooglePlayId ?? "",
                    achievement.IsIncremental.ToString().ToLower(),
                    achievement.MaxProgress.ToString()
                });
            }

            file.Close();

            AchievementLogger.Log(AchievementLogger.Areas.Editor, $"Exported {database.Achievements.Count} achievements to {path}");
            return ExportResult.SuccessResult(database.Achievements.Count);
        }
        catch (Exception ex)
        {
            AchievementLogger.Error(AchievementLogger.Areas.Editor, $"CSV export error: {ex}");
            return ExportResult.FailureResult($"Failed to export CSV: {ex.Message}");
        }
    }

    #endregion

    #region JSON Operations

    /// <summary>
    /// Import achievements from a JSON file into the database
    /// </summary>
    public static ImportResult ImportFromJSON(AchievementDatabase database, string path)
    {
        try
        {
            var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
            if (file == null)
                return ImportResult.FailureResult($"Failed to open file: {FileAccess.GetOpenError()}");

            var jsonContent = file.GetAsText();
            file.Close();

            if (string.IsNullOrWhiteSpace(jsonContent))
                return ImportResult.FailureResult("JSON file is empty.");

            // Parse JSON
            JsonDocument? doc;
            try
            {
                doc = JsonDocument.Parse(jsonContent);
            }
            catch (JsonException ex)
            {
                return ImportResult.FailureResult($"Invalid JSON format: {ex.Message}");
            }

            var root = doc.RootElement;
            JsonElement achievementsArray;

            // Support both { "achievements": [...] } and direct array [...]
            if (root.ValueKind == JsonValueKind.Array)
            {
                achievementsArray = root;
            }
            else if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("achievements", out var achProp))
            {
                achievementsArray = achProp;
            }
            else
            {
                doc.Dispose();
                return ImportResult.FailureResult("JSON must be an array of achievements or an object with an 'achievements' array.");
            }

            int importedCount = 0;
            int updatedCount = 0;
            int skippedCount = 0;

            foreach (var item in achievementsArray.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                    continue;

                // Get Id (required)
                if (!item.TryGetProperty("Id", out var idProp) && !item.TryGetProperty("id", out idProp))
                    continue;

                var id = idProp.GetString();
                if (string.IsNullOrWhiteSpace(id))
                    continue;

                // Check if achievement already exists
                var existing = database.GetById(id);
                bool isNew = existing == null;

                var achievement = existing ?? new Achievement { Id = id };
                bool hasChanges = false;

                // Update fields from JSON, tracking if any changes occur
                if (TryGetJsonString(item, "DisplayName", out var displayName) || TryGetJsonString(item, "displayName", out displayName))
                {
                    if (achievement.DisplayName != displayName)
                    {
                        achievement.DisplayName = displayName;
                        hasChanges = true;
                    }
                }

                if (TryGetJsonString(item, "Description", out var description) || TryGetJsonString(item, "description", out description))
                {
                    if (achievement.Description != description)
                    {
                        achievement.Description = description;
                        hasChanges = true;
                    }
                }

                if (TryGetJsonString(item, "SteamId", out var steamId) || TryGetJsonString(item, "steamId", out steamId))
                {
                    if (achievement.SteamId != steamId)
                    {
                        achievement.SteamId = steamId;
                        hasChanges = true;
                    }
                }

                if (TryGetJsonString(item, "GameCenterId", out var gameCenterId) || TryGetJsonString(item, "gameCenterId", out gameCenterId))
                {
                    if (achievement.GameCenterId != gameCenterId)
                    {
                        achievement.GameCenterId = gameCenterId;
                        hasChanges = true;
                    }
                }

                if (TryGetJsonString(item, "GooglePlayId", out var googlePlayId) || TryGetJsonString(item, "googlePlayId", out googlePlayId))
                {
                    if (achievement.GooglePlayId != googlePlayId)
                    {
                        achievement.GooglePlayId = googlePlayId;
                        hasChanges = true;
                    }
                }

                if (TryGetJsonBool(item, "IsIncremental", out var isIncremental) || TryGetJsonBool(item, "isIncremental", out isIncremental))
                {
                    if (achievement.IsIncremental != isIncremental)
                    {
                        achievement.IsIncremental = isIncremental;
                        hasChanges = true;
                    }
                }

                if (TryGetJsonInt(item, "MaxProgress", out var maxProgress) || TryGetJsonInt(item, "maxProgress", out maxProgress))
                {
                    if (achievement.MaxProgress != maxProgress)
                    {
                        achievement.MaxProgress = maxProgress;
                        hasChanges = true;
                    }
                }

                if (isNew)
                {
                    achievement.ExtraProperties = new Godot.Collections.Dictionary<string, Variant>();
                    database.AddAchievement(achievement);
                    importedCount++;
                }
                else if (hasChanges)
                {
                    updatedCount++;
                }
                else
                {
                    skippedCount++;
                }
            }

            doc.Dispose();

            AchievementLogger.Log(AchievementLogger.Areas.Editor, $"Imported JSON from {path}: {importedCount} new, {updatedCount} updated, {skippedCount} skipped");
            return ImportResult.SuccessResult(importedCount, updatedCount, skippedCount);
        }
        catch (Exception ex)
        {
            AchievementLogger.Error(AchievementLogger.Areas.Editor, $"JSON import error: {ex}");
            return ImportResult.FailureResult($"Failed to import JSON: {ex.Message}");
        }
    }

    /// <summary>
    /// Export achievements from the database to a JSON file
    /// </summary>
    public static ExportResult ExportToJSON(AchievementDatabase database, string path)
    {
        if (database.Achievements.Count == 0)
            return ExportResult.FailureResult("No achievements to export.");

        try
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = null // Keep PascalCase for compatibility with Godot conventions
            };

            // Build achievements list for JSON
            var achievementsList = new System.Collections.Generic.List<object>();
            foreach (var achievement in database.Achievements)
            {
                achievementsList.Add(new
                {
                    Id = achievement.Id ?? "",
                    DisplayName = achievement.DisplayName ?? "",
                    Description = achievement.Description ?? "",
                    SteamId = achievement.SteamId ?? "",
                    GameCenterId = achievement.GameCenterId ?? "",
                    GooglePlayId = achievement.GooglePlayId ?? "",
                    IsIncremental = achievement.IsIncremental,
                    MaxProgress = achievement.MaxProgress
                });
            }

            var exportObject = new { achievements = achievementsList };
            var jsonContent = JsonSerializer.Serialize(exportObject, options);

            var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
            if (file == null)
                return ExportResult.FailureResult($"Failed to create file: {FileAccess.GetOpenError()}");

            file.StoreString(jsonContent);
            file.Close();

            AchievementLogger.Log(AchievementLogger.Areas.Editor, $"Exported {database.Achievements.Count} achievements to JSON: {path}");
            return ExportResult.SuccessResult(database.Achievements.Count);
        }
        catch (Exception ex)
        {
            AchievementLogger.Error(AchievementLogger.Areas.Editor, $"JSON export error: {ex}");
            return ExportResult.FailureResult($"Failed to export JSON: {ex.Message}");
        }
    }

    #endregion

    #region Helper Methods

    private static string? GetCSVValue(string[] values, System.Collections.Generic.Dictionary<string, int> columnMap, string columnName)
    {
        if (!columnMap.TryGetValue(columnName, out int index) || index >= values.Length)
            return null;
        return values[index].Trim();
    }

    private static bool TryGetJsonString(JsonElement element, string propertyName, out string value)
    {
        value = string.Empty;
        if (element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String)
        {
            value = prop.GetString() ?? string.Empty;
            return true;
        }
        return false;
    }

    private static bool TryGetJsonBool(JsonElement element, string propertyName, out bool value)
    {
        value = false;
        if (element.TryGetProperty(propertyName, out var prop))
        {
            if (prop.ValueKind == JsonValueKind.True)
            {
                value = true;
                return true;
            }
            if (prop.ValueKind == JsonValueKind.False)
            {
                value = false;
                return true;
            }
        }
        return false;
    }

    private static bool TryGetJsonInt(JsonElement element, string propertyName, out int value)
    {
        value = 0;
        if (element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.Number)
        {
            if (prop.TryGetInt32(out value))
                return true;
        }
        return false;
    }

    #endregion
}
#endif
