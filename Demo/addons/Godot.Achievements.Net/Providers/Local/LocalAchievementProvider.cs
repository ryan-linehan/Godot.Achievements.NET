using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot.Achievements.Core;

namespace Godot.Achievements.Providers.Local;

/// <summary>
/// Local achievement provider that persists achievement state to user://achievements.json
/// This is the source of truth for all achievement unlocks
/// </summary>
public partial class LocalAchievementProvider : AchievementProviderBase
{
    public static bool IsPlatformSupported => true;

    private const string SavePath = "user://achievements.json";
    private readonly AchievementDatabase _database;
    private Dictionary<string, AchievementState> _achievementStates = new();

    public override string ProviderName => ProviderNames.Local;
    public override bool IsAvailable => true;

    public LocalAchievementProvider(AchievementDatabase database)
    {
        _database = database;
        LoadFromDisk();
    }

    #region Sync Methods

    public override void UnlockAchievement(string achievementId)
    {
        var achievement = _database.GetById(achievementId);
        if (achievement == null)
        {
            this.LogWarning($"Achievement '{achievementId}' not found in database");
            EmitAchievementUnlocked(achievementId, false, $"Achievement '{achievementId}' not found in database");
            return;
        }

        if (_achievementStates.TryGetValue(achievementId, out var state) && state.IsUnlocked)
        {
            // Already unlocked, just sync state to achievement object
            achievement.IsUnlocked = true;
            achievement.UnlockedAt = state.UnlockedAt;
            achievement.CurrentProgress = achievement.MaxProgress;
            EmitAchievementUnlocked(achievementId, true);
            return;
        }

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
        EmitAchievementUnlocked(achievementId, true);
    }

    public override void IncrementProgress(string achievementId, int amount)
    {
        var achievement = _database.GetById(achievementId);
        if (achievement == null)
        {
            this.LogWarning($"Achievement '{achievementId}' not found in database");
            EmitProgressIncremented(achievementId, 0, false, $"Achievement '{achievementId}' not found in database");
            return;
        }

        if (!_achievementStates.TryGetValue(achievementId, out var state))
        {
            state = new AchievementState();
            _achievementStates[achievementId] = state;
        }

        // Don't modify if already unlocked
        if (state.IsUnlocked)
        {
            EmitProgressIncremented(achievementId, state.CurrentProgress, true);
            return;
        }

        int newProgress = Mathf.Clamp(state.CurrentProgress + amount, 0, achievement.MaxProgress);
        state.CurrentProgress = newProgress;
        achievement.CurrentProgress = newProgress;

        if (newProgress >= achievement.MaxProgress)
        {
            state.IsUnlocked = true;
            state.UnlockedAt = DateTime.UtcNow;
            achievement.IsUnlocked = true;
            achievement.UnlockedAt = state.UnlockedAt;
        }

        SaveToDisk();
        EmitProgressIncremented(achievementId, newProgress, true);
    }

    public override void ResetAchievement(string achievementId)
    {
        var achievement = _database.GetById(achievementId);
        if (achievement == null)
        {
            this.LogWarning($"Achievement '{achievementId}' not found in database");
            EmitAchievementReset(achievementId, false, $"Achievement '{achievementId}' not found in database");
            return;
        }

        if (_achievementStates.Remove(achievementId))
        {
            achievement.IsUnlocked = false;
            achievement.UnlockedAt = null;
            achievement.CurrentProgress = 0;
            SaveToDisk();
            this.Log($"Reset achievement: {achievementId}");
        }
        EmitAchievementReset(achievementId, true);
    }

    public override void ResetAllAchievements()
    {
        _achievementStates.Clear();

        foreach (var achievement in _database.Achievements)
        {
            achievement.IsUnlocked = false;
            achievement.UnlockedAt = null;
            achievement.CurrentProgress = 0;
        }

        SaveToDisk();
        this.Log("Reset all achievements");
        EmitAllAchievementsReset(true);
    }

    #endregion

    #region Async Methods

    public override Task<AchievementUnlockResult> UnlockAchievementAsync(string achievementId)
    {
        var achievement = _database.GetById(achievementId);
        if (achievement == null)
        {
            return Task.FromResult(AchievementUnlockResult.FailureResult($"Achievement '{achievementId}' not found in database"));
        }

        bool wasAlreadyUnlocked = achievement.IsUnlocked;
        UnlockAchievement(achievementId);

        return Task.FromResult(AchievementUnlockResult.SuccessResult(wasAlreadyUnlocked));
    }

    public override Task<int> GetProgressAsync(string achievementId)
    {
        if (_achievementStates.TryGetValue(achievementId, out var state))
        {
            return Task.FromResult(state.CurrentProgress);
        }
        return Task.FromResult(0);
    }

    public override Task<SyncResult> IncrementProgressAsync(string achievementId, int amount)
    {
        var achievement = _database.GetById(achievementId);
        if (achievement == null)
        {
            return Task.FromResult(SyncResult.FailureResult($"Achievement '{achievementId}' not found in database"));
        }

        IncrementProgress(achievementId, amount);
        return Task.FromResult(SyncResult.SuccessResult());
    }

    public override Task<SyncResult> ResetAchievementAsync(string achievementId)
    {
        var achievement = _database.GetById(achievementId);
        if (achievement == null)
        {
            return Task.FromResult(SyncResult.FailureResult($"Achievement '{achievementId}' not found in database"));
        }

        ResetAchievement(achievementId);
        return Task.FromResult(SyncResult.SuccessResult());
    }

    public override Task<SyncResult> ResetAllAchievementsAsync()
    {
        ResetAllAchievements();
        return Task.FromResult(SyncResult.SuccessResult());
    }

    #endregion

    #region Persistence

    private void LoadFromDisk()
    {
        if (!FileAccess.FileExists(SavePath))
        {
            _achievementStates = new Dictionary<string, AchievementState>();
            return;
        }

        var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Read);
        if (file == null)
        {
            this.LogError($"Failed to open achievement save file: {FileAccess.GetOpenError()}");
            _achievementStates = new Dictionary<string, AchievementState>();
            return;
        }

        var jsonText = file.GetAsText();
        file.Close();

        var json = new Json();
        var error = json.Parse(jsonText);
        if (error != Error.Ok)
        {
            this.LogError($"Failed to parse achievement save file: {json.GetErrorMessage()}");
            _achievementStates = new Dictionary<string, AchievementState>();
            return;
        }

        var data = json.Data.AsGodotDictionary<string, Godot.Collections.Dictionary>();
        _achievementStates = new Dictionary<string, AchievementState>();

        foreach (var kvp in data)
        {
            var stateDict = kvp.Value;
            var state = new AchievementState
            {
                IsUnlocked = stateDict.TryGetValue("IsUnlocked", out var unlocked) && unlocked.AsBool(),
                CurrentProgress = stateDict.TryGetValue("CurrentProgress", out var progress) ? progress.AsInt32() : 0
            };

            if (stateDict.TryGetValue("UnlockedAt", out var unlockedAt))
            {
                var dateStr = unlockedAt.AsString();
                if (!string.IsNullOrEmpty(dateStr) && DateTime.TryParse(dateStr, out var date))
                {
                    state.UnlockedAt = date;
                }
            }

            _achievementStates[kvp.Key] = state;
        }
        // Sync loaded states to Achievement objects in the database
        SyncStatesToDatabase();
        this.Log($"Loaded {_achievementStates.Count} achievement states from {SavePath}");
    }


    private void SaveToDisk()
    {
        var data = new Godot.Collections.Dictionary<string, Godot.Collections.Dictionary>();

        foreach (var kvp in _achievementStates)
        {
            var stateDict = new Godot.Collections.Dictionary
            {
                ["IsUnlocked"] = kvp.Value.IsUnlocked,
                ["CurrentProgress"] = kvp.Value.CurrentProgress
            };

            if (kvp.Value.UnlockedAt.HasValue)
            {
                stateDict["UnlockedAt"] = kvp.Value.UnlockedAt.Value.ToString("O");
            }

            data[kvp.Key] = stateDict;
        }

        var jsonText = Json.Stringify(data, "\t");

        var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write);
        if (file == null)
        {
            this.LogError($"Failed to write achievement save file: {FileAccess.GetOpenError()}");
            return;
        }

        file.StoreString(jsonText);
        file.Close();

        this.Log($"Saved {_achievementStates.Count} achievement states to {SavePath}");
    }

    private void SyncStatesToDatabase()
    {
        foreach (var kvp in _achievementStates)
        {
            var achievement = _database.GetById(kvp.Key);
            if (achievement != null)
            {
                achievement.IsUnlocked = kvp.Value.IsUnlocked;
                achievement.CurrentProgress = kvp.Value.CurrentProgress;
                achievement.UnlockedAt = kvp.Value.UnlockedAt;
            }
        }
    }
    #endregion
}

internal class AchievementState
{
    public bool IsUnlocked { get; set; }
    public DateTime? UnlockedAt { get; set; }
    public int CurrentProgress { get; set; }
}
