using System;
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
    private Godot.Collections.Dictionary<string, AchievementState> _achievementStates = new();

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
            achievement.Progress = 1.0f;
            return Task.FromResult(AchievementUnlockResult.SuccessResult(wasAlreadyUnlocked: true));
        }

        // Unlock the achievement
        var newState = new AchievementState
        {
            IsUnlocked = true,
            UnlockedAt = DateTime.UtcNow,
            Progress = 1.0f
        };

        _achievementStates[achievementId] = newState;
        achievement.IsUnlocked = true;
        achievement.UnlockedAt = newState.UnlockedAt;
        achievement.Progress = 1.0f;

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
            achievement.Progress = state.Progress;
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
                achievement.Progress = state.Progress;
            }
        }

        return Task.FromResult(_database.Achievements.ToArray());
    }

    public Task<float> GetProgress(string achievementId)
    {
        if (_achievementStates.TryGetValue(achievementId, out var state))
        {
            return Task.FromResult(state.Progress);
        }

        return Task.FromResult(0f);
    }

    public Task SetProgress(string achievementId, float progress)
    {
        var achievement = _database.GetById(achievementId);
        if (achievement == null)
        {
            GD.PushWarning($"Achievement '{achievementId}' not found in database");
            return Task.CompletedTask;
        }

        // Clamp progress to 0.0 - 1.0
        progress = Mathf.Clamp(progress, 0f, 1f);

        // Get or create state
        if (!_achievementStates.TryGetValue(achievementId, out var state))
        {
            state = new AchievementState();
            _achievementStates[achievementId] = state;
        }

        state.Progress = progress;
        achievement.Progress = progress;

        // Auto-unlock if progress reaches 100%
        if (progress >= 1.0f && !state.IsUnlocked)
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
            achievement.Progress = 0f;

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
            achievement.Progress = 0f;
        }

        SaveToDisk();
        GD.Print($"[Local] Reset all achievements");
        return Task.FromResult(true);
    }

    /// <summary>
    /// Load achievement states from disk
    /// </summary>
    private void LoadFromDisk()
    {
        if (!FileAccess.FileExists(SavePath))
        {
            _achievementStates = new Godot.Collections.Dictionary<string, AchievementState>();
            return;
        }

        var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Read);
        if (file == null)
        {
            GD.PushError($"Failed to open achievement save file: {FileAccess.GetOpenError()}");
            _achievementStates = new Godot.Collections.Dictionary<string, AchievementState>();
            return;
        }

        var jsonText = file.GetAsText();
        file.Close();

        var json = new Json();
        var error = json.Parse(jsonText);
        if (error != Error.Ok)
        {
            GD.PushError($"Failed to parse achievement save file: {json.GetErrorMessage()}");
            _achievementStates = new Godot.Collections.Dictionary<string, AchievementState>();
            return;
        }

        var data = json.Data.AsGodotDictionary<string, Godot.Collections.Dictionary>();
        _achievementStates = new Godot.Collections.Dictionary<string, AchievementState>();

        foreach (var kvp in data)
        {
            var stateDict = kvp.Value;
            var state = new AchievementState
            {
                IsUnlocked = stateDict.TryGetValue("IsUnlocked", out var unlocked) && (bool)unlocked,
                Progress = stateDict.TryGetValue("Progress", out var progress) ? Convert.ToSingle(progress) : 0f
            };

            if (stateDict.TryGetValue("UnlockedAt", out var unlockedAt) && unlockedAt is string dateStr)
            {
                if (DateTime.TryParse(dateStr, out var date))
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
    /// </summary>
    private void SaveToDisk()
    {
        var data = new Godot.Collections.Dictionary<string, Godot.Collections.Dictionary>();

        foreach (var kvp in _achievementStates)
        {
            var stateDict = new Godot.Collections.Dictionary
            {
                ["IsUnlocked"] = kvp.Value.IsUnlocked,
                ["Progress"] = kvp.Value.Progress
            };

            if (kvp.Value.UnlockedAt.HasValue)
            {
                stateDict["UnlockedAt"] = kvp.Value.UnlockedAt.Value.ToString("O"); // ISO 8601 format
            }

            data[kvp.Key] = stateDict;
        }

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
    public float Progress { get; set; }
}
