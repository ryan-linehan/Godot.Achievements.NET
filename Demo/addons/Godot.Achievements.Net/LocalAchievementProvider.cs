using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Godot.Achievements.Core;

/// <summary>
/// Local achievement provider that persists achievement state to user://achievements.json
/// This is the source of truth for all achievement unlocks
/// </summary>
public class LocalAchievementProvider : IAchievementProvider
{
    private const string SavePath = "user://achievements.json";
    private readonly AchievementDatabase _database;
    private Dictionary<string, AchievementState> _achievementStates = new();

    public string ProviderName => "Local";
    public bool IsAvailable => true;

    public LocalAchievementProvider(AchievementDatabase database)
    {
        _database = database;
        LoadFromDisk();
    }

    public Task<AchievementUnlockResult> UnlockAchievement(string achievementId)
    {
        var achievement = _database.GetById(achievementId);
        if (achievement == null)
        {
            return Task.FromResult(AchievementUnlockResult.FailureResult($"Achievement '{achievementId}' not found in database"));
        }

        // Check if already unlocked
        if (_achievementStates.TryGetValue(achievementId, out var state) && state.IsUnlocked)
        {
            achievement.IsUnlocked = true;
            achievement.UnlockedAt = state.UnlockedAt;
            achievement.CurrentProgress = achievement.MaxProgress;
            return Task.FromResult(AchievementUnlockResult.SuccessResult(wasAlreadyUnlocked: true));
        }

        // Unlock the achievement
        var newState = new AchievementState
        {
            IsUnlocked = true,
            UnlockedAt = DateTime.UtcNow,
            CurrentProgress = achievement.MaxProgress
        };

        _achievementStates[achievementId] = newState;
        achievement.IsUnlocked = true;
        achievement.UnlockedAt = newState.UnlockedAt;
        achievement.CurrentProgress = achievement.MaxProgress;

        SaveToDisk();

        return Task.FromResult(AchievementUnlockResult.SuccessResult(wasAlreadyUnlocked: false));
    }

    public Task<Achievement?> GetAchievement(string achievementId)
    {
        var achievement = _database.GetById(achievementId);
        if (achievement == null)
            return Task.FromResult<Achievement?>(null);

        // Apply local state to achievement
        if (_achievementStates.TryGetValue(achievementId, out var state))
        {
            achievement.IsUnlocked = state.IsUnlocked;
            achievement.UnlockedAt = state.UnlockedAt;
            achievement.CurrentProgress = state.CurrentProgress;
        }

        return Task.FromResult<Achievement?>(achievement);
    }

    public Task<Achievement[]> GetAllAchievements()
    {
        // Apply local state to all achievements
        foreach (var achievement in _database.Achievements)
        {
            if (_achievementStates.TryGetValue(achievement.Id, out var state))
            {
                achievement.IsUnlocked = state.IsUnlocked;
                achievement.UnlockedAt = state.UnlockedAt;
                achievement.CurrentProgress = state.CurrentProgress;
            }
        }

        return Task.FromResult(_database.Achievements.ToArray());
    }

    public Task<int> GetProgress(string achievementId)
    {
        if (_achievementStates.TryGetValue(achievementId, out var state))
        {
            return Task.FromResult(state.CurrentProgress);
        }

        return Task.FromResult(0);
    }

    public Task SetProgress(string achievementId, int currentProgress)
    {
        var achievement = _database.GetById(achievementId);
        if (achievement == null)
        {
            GD.PushWarning($"Achievement '{achievementId}' not found in database");
            return Task.CompletedTask;
        }

        // Clamp progress to 0 - MaxProgress
        currentProgress = Mathf.Clamp(currentProgress, 0, achievement.MaxProgress);

        // Get or create state
        if (!_achievementStates.TryGetValue(achievementId, out var state))
        {
            state = new AchievementState();
            _achievementStates[achievementId] = state;
        }

        state.CurrentProgress = currentProgress;
        achievement.CurrentProgress = currentProgress;

        // Auto-unlock if progress reaches max
        if (currentProgress >= achievement.MaxProgress && !state.IsUnlocked)
        {
            state.IsUnlocked = true;
            state.UnlockedAt = DateTime.UtcNow;
            achievement.IsUnlocked = true;
            achievement.UnlockedAt = state.UnlockedAt;
        }

        SaveToDisk();

        return Task.CompletedTask;
    }

    public Task<bool> ResetAchievement(string achievementId)
    {
        var achievement = _database.GetById(achievementId);
        if (achievement == null)
        {
            GD.PushWarning($"[Local] Achievement '{achievementId}' not found in database");
            return Task.FromResult(false);
        }

        // Remove from state dictionary
        if (_achievementStates.Remove(achievementId))
        {
            // Reset runtime state
            achievement.IsUnlocked = false;
            achievement.UnlockedAt = null;
            achievement.CurrentProgress = 0;

            SaveToDisk();
            GD.Print($"[Local] Reset achievement: {achievementId}");
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }

    public Task<bool> ResetAllAchievements()
    {
        // Clear all states
        _achievementStates.Clear();

        // Reset all runtime states
        foreach (var achievement in _database.Achievements)
        {
            achievement.IsUnlocked = false;
            achievement.UnlockedAt = null;
            achievement.CurrentProgress = 0;
        }

        SaveToDisk();
        GD.Print($"[Local] Reset all achievements");
        return Task.FromResult(true);
    }

    /// <summary>
    /// Load achievement states from disk
    /// Deserializes JSON save file and reconstructs achievement state objects
    /// Gracefully handles missing files or corrupted data by initializing empty state
    /// </summary>
    private void LoadFromDisk()
    {
        // Initialize with empty state if save file doesn't exist (first run)
        if (!FileAccess.FileExists(SavePath))
        {
            _achievementStates = new Dictionary<string, AchievementState>();
            return;
        }

        var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Read);
        if (file == null)
        {
            GD.PushError($"Failed to open achievement save file: {FileAccess.GetOpenError()}");
            _achievementStates = new Dictionary<string, AchievementState>();
            return;
        }

        var jsonText = file.GetAsText();
        file.Close();

        // Parse JSON using Godot's Json class (AOT-compatible)
        var json = new Json();
        var error = json.Parse(jsonText);
        if (error != Error.Ok)
        {
            GD.PushError($"Failed to parse achievement save file: {json.GetErrorMessage()}");
            _achievementStates = new Dictionary<string, AchievementState>();
            return;
        }

        // Deserialize from Godot dictionary format to our internal C# Dictionary
        var data = json.Data.AsGodotDictionary<string, Godot.Collections.Dictionary>();
        _achievementStates = new Dictionary<string, AchievementState>();
    
        foreach (var kvp in data)
        {
            var stateDict = kvp.Value;
            var state = new AchievementState
            {
                // Use TryGetValue with fallback defaults for missing/malformed fields
                IsUnlocked = stateDict.TryGetValue("IsUnlocked", out var unlocked) && unlocked.AsBool(),
                CurrentProgress = stateDict.TryGetValue("CurrentProgress", out var progress) ? progress.AsInt32() : 0
            };

            // Parse optional UnlockedAt field (stored as ISO 8601 string)
            if (stateDict.TryGetValue("UnlockedAt", out var unlockedAt))
            {
                var dateStr = unlockedAt.AsString();
                // Gracefully handle null/empty dates or invalid formats
                if (!string.IsNullOrEmpty(dateStr) && DateTime.TryParse(dateStr, out var date))
                {
                    state.UnlockedAt = date;
                }
            }

            _achievementStates[kvp.Key] = state;
        }

        GD.Print($"[Achievements] Loaded {_achievementStates.Count} achievement states from {SavePath}");
    }

    /// <summary>
    /// Save achievement states to disk
    /// Serializes state to JSON using Godot's Json.Stringify (AOT-compatible)
    /// Dates are stored in ISO 8601 format for portability across platforms
    /// </summary>
    private void SaveToDisk()
    {
        // Convert internal C# Dictionary to Godot.Collections.Dictionary for serialization
        var data = new Godot.Collections.Dictionary<string, Godot.Collections.Dictionary>();

        foreach (var kvp in _achievementStates)
        {
            var stateDict = new Godot.Collections.Dictionary
            {
                ["IsUnlocked"] = kvp.Value.IsUnlocked,
                ["CurrentProgress"] = kvp.Value.CurrentProgress
            };

            // Only include UnlockedAt if it has a value (reduces file size for locked achievements)
            if (kvp.Value.UnlockedAt.HasValue)
            {
                // ISO 8601 "O" format ensures proper round-trip parsing and timezone handling
                stateDict["UnlockedAt"] = kvp.Value.UnlockedAt.Value.ToString("O");
            }

            data[kvp.Key] = stateDict;
        }

        // Pretty-print JSON with tabs for human readability
        var jsonText = Json.Stringify(data, "\t");

        var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write);
        if (file == null)
        {
            GD.PushError($"Failed to write achievement save file: {FileAccess.GetOpenError()}");
            return;
        }

        file.StoreString(jsonText);
        file.Close();

        GD.Print($"[Achievements] Saved {_achievementStates.Count} achievement states to {SavePath}");
    }
}

/// <summary>
/// Internal structure for storing achievement state
/// </summary>
internal class AchievementState
{
    public bool IsUnlocked { get; set; }
    public DateTime? UnlockedAt { get; set; }
    public int CurrentProgress { get; set; }
}
