#if TOOLS
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Godot.Achievements.Core.Editor;

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
                if (columnMap.ContainsKey("IconPath"))
                {
                    var iconPath = GetCSVValue(values, columnMap, "IconPath");
                    if (!string.IsNullOrWhiteSpace(iconPath))
                    {
                        var newIcon = ResourceLoader.Load<Texture2D>(iconPath);
                        if (newIcon != null && achievement.Icon != newIcon)
                        {
                            achievement.Icon = newIcon;
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
            file.StoreCsvLine(new string[] { "Id", "DisplayName", "Description", "IconPath", "SteamId", "GameCenterId", "GooglePlayId", "IsIncremental", "MaxProgress" });

            // Write achievement rows using Godot's StoreCsvLine (handles escaping automatically)
            foreach (var achievement in database.Achievements)
            {
                file.StoreCsvLine(new string[]
                {
                    achievement.Id ?? "",
                    achievement.DisplayName ?? "",
                    achievement.Description ?? "",
                    achievement.Icon?.ResourcePath ?? "",
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

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            // Peek first non-whitespace character to determine format
            List<AchievementImportDto>? dtos;
            var firstChar = jsonContent.TrimStart()[0];

            try
            {
                if (firstChar == '[')
                {
                    // Direct array format: [...]
                    dtos = JsonSerializer.Deserialize<List<AchievementImportDto>>(jsonContent, options);
                }
                else if (firstChar == '{')
                {
                    // Wrapper format: { "achievements": [...] }
                    var wrapper = JsonSerializer.Deserialize<AchievementImportWrapper>(jsonContent, options);
                    dtos = wrapper?.Achievements;
                }
                else
                {
                    return ImportResult.FailureResult("JSON must be an array of achievements or an object with an 'achievements' array.");
                }
            }
            catch (JsonException ex)
            {
                return ImportResult.FailureResult($"Invalid JSON format: {ex.Message}");
            }

            if (dtos == null || dtos.Count == 0)
                return ImportResult.FailureResult("No achievements found in JSON.");

            int importedCount = 0;
            int updatedCount = 0;
            int skippedCount = 0;

            foreach (var dto in dtos)
            {
                if (string.IsNullOrWhiteSpace(dto.Id))
                    continue;

                // Check if achievement already exists
                var existing = database.GetById(dto.Id);
                bool isNew = existing == null;

                var achievement = existing ?? new Achievement { Id = dto.Id };
                bool hasChanges = false;

                // Update fields from DTO, tracking if any changes occur
                if (dto.DisplayName != null && achievement.DisplayName != dto.DisplayName)
                {
                    achievement.DisplayName = dto.DisplayName;
                    hasChanges = true;
                }

                if (dto.Description != null && achievement.Description != dto.Description)
                {
                    achievement.Description = dto.Description;
                    hasChanges = true;
                }

                if (dto.SteamId != null && achievement.SteamId != dto.SteamId)
                {
                    achievement.SteamId = dto.SteamId;
                    hasChanges = true;
                }

                if (dto.GameCenterId != null && achievement.GameCenterId != dto.GameCenterId)
                {
                    achievement.GameCenterId = dto.GameCenterId;
                    hasChanges = true;
                }

                if (dto.GooglePlayId != null && achievement.GooglePlayId != dto.GooglePlayId)
                {
                    achievement.GooglePlayId = dto.GooglePlayId;
                    hasChanges = true;
                }

                if (dto.IsIncremental.HasValue && achievement.IsIncremental != dto.IsIncremental.Value)
                {
                    achievement.IsIncremental = dto.IsIncremental.Value;
                    hasChanges = true;
                }

                if (dto.MaxProgress.HasValue && achievement.MaxProgress != dto.MaxProgress.Value)
                {
                    achievement.MaxProgress = dto.MaxProgress.Value;
                    hasChanges = true;
                }

                if (!string.IsNullOrWhiteSpace(dto.IconPath))
                {
                    var newIcon = ResourceLoader.Load<Texture2D>(dto.IconPath);
                    if (newIcon != null && achievement.Icon != newIcon)
                    {
                        achievement.Icon = newIcon;
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
                    IconPath = achievement.Icon?.ResourcePath ?? "",
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

    #endregion
}
#endif
