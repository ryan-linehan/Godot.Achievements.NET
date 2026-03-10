#if GODOT_PC
using System;
using System.Threading.Tasks;
using Godot.Achievements.Core;
using Godot.Achievements.Providers;
using GodotSteam;

namespace Godot.Achievements.Providers.GodotSteam;

/// <summary>
/// Steam achievement provider using GodotSteam with C# bindings.
/// Requires GodotSteam GDExtension and GodotSteam_CSharpBindings addon to be installed.
/// https://godotsteam.com/
/// https://github.com/LauraWebdev/GodotSteam_CSharpBindings
/// </summary>
public partial class GodotSteamAchievementProvider : AchievementProviderBase
{
    public static bool IsPlatformSupported => true;

    private readonly AchievementDatabase _database;
    private bool _isInitialized;

    public override string ProviderName => ProviderNames.Steam;

    public GodotSteamAchievementProvider(AchievementDatabase database)
    {
        _database = database;
        Initialize();
    }

    private void Initialize()
    {
        try
        {
            // Check if GodotSteam is available and initialized
            if (!Steam.IsSteamRunning())
            {
                this.LogWarning("Steam is not running. Make sure Steam is open.");
                return;
            }

            // Request current stats from Steam
            Steam.RequestCurrentStats();

            _isInitialized = true;
            this.Log("Initialized with GodotSteam C# bindings");
        }
        catch (Exception ex)
        {
            this.LogError($"Failed to initialize GodotSteam: {ex.Message}");
        }
    }

    public override bool IsAvailable => _isInitialized && Steam.IsSteamRunning();

    #region Helper Methods

    private (string? steamId, string? error) ValidateAndGetSteamId(string achievementId)
    {
        if (!_isInitialized)
            return (null, "GodotSteam not initialized. Ensure GodotSteam GDExtension and C# bindings are installed.");

        var achievement = _database.GetById(achievementId);
        if (achievement == null)
            return (null, $"Achievement '{achievementId}' not found in database");

        var steamId = achievement.SteamId;
        if (string.IsNullOrEmpty(steamId))
            return (null, $"Achievement '{achievementId}' has no Steam ID configured");

        return (steamId, null);
    }

    /// <summary>
    /// Gets the Steam stat key for an achievement. Falls back to SteamId if SteamStatId is not set.
    /// </summary>
    private string? GetStatKey(Achievement achievement)
    {
        var statId = achievement.SteamStatId;
        if (!string.IsNullOrEmpty(statId))
            return statId;

        return achievement.SteamId;
    }

    private bool IsAchievementUnlocked(string steamId)
    {
        try
        {
            var result = Steam.GetAchievement(steamId);
            if (result.ContainsKey("achieved"))
            {
                return result["achieved"].AsBool();
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    #endregion

    #region Sync Methods

    public override void UnlockAchievement(string achievementId)
    {
        var (steamId, error) = ValidateAndGetSteamId(achievementId);
        if (error != null)
        {
            this.LogWarning(error);
            EmitAchievementUnlocked(achievementId, false, error);
            return;
        }

        try
        {
            var success = Steam.SetAchievement(steamId!);
            if (success)
            {
                Steam.StoreStats();
                this.Log($"Unlocked achievement: {steamId}");
                EmitAchievementUnlocked(achievementId, true);
            }
            else
            {
                this.LogError($"Failed to unlock achievement: {steamId}");
                EmitAchievementUnlocked(achievementId, false, "SetAchievement returned false");
            }
        }
        catch (Exception ex)
        {
            this.LogError($"Failed to unlock achievement: {ex.Message}");
            EmitAchievementUnlocked(achievementId, false, ex.Message);
        }
    }

    public override void IncrementProgress(string achievementId, int amount)
    {
        var (steamId, error) = ValidateAndGetSteamId(achievementId);
        if (error != null)
        {
            this.LogWarning(error);
            EmitProgressIncremented(achievementId, 0, false, error);
            return;
        }

        try
        {
            var achievement = _database.GetById(achievementId)!;
            var statKey = GetStatKey(achievement);
            if (string.IsNullOrEmpty(statKey))
            {
                var noStatError = $"Achievement '{achievementId}' has no Steam Stat ID configured";
                this.LogWarning(noStatError);
                EmitProgressIncremented(achievementId, 0, false, noStatError);
                return;
            }

            var currentProgress = Steam.GetStatInt(statKey);
            var newProgress = currentProgress + amount;
            Steam.SetStatInt(statKey, newProgress);
            Steam.StoreStats();

            this.Log($"Incremented progress for stat {statKey}: {currentProgress} -> {newProgress}");
            EmitProgressIncremented(achievementId, newProgress, true);

            // Check if achievement should be unlocked
            if (newProgress >= achievement.MaxProgress)
            {
                UnlockAchievement(achievementId);
            }
        }
        catch (Exception ex)
        {
            this.LogError($"Failed to increment progress: {ex.Message}");
            EmitProgressIncremented(achievementId, 0, false, ex.Message);
        }
    }

    public override void ResetAchievement(string achievementId)
    {
        var (steamId, error) = ValidateAndGetSteamId(achievementId);
        if (error != null)
        {
            this.LogWarning(error);
            EmitAchievementReset(achievementId, false, error);
            return;
        }

        try
        {
            var success = Steam.ClearAchievement(steamId!);
            if (success)
            {
                Steam.StoreStats();
                this.Log($"Reset achievement: {steamId}");
                EmitAchievementReset(achievementId, true);
            }
            else
            {
                this.LogError($"Failed to reset achievement: {steamId}");
                EmitAchievementReset(achievementId, false, "ClearAchievement returned false");
            }
        }
        catch (Exception ex)
        {
            this.LogError($"Failed to reset achievement: {ex.Message}");
            EmitAchievementReset(achievementId, false, ex.Message);
        }
    }

    public override void ResetAllAchievements()
    {
        if (!IsAvailable)
        {
            this.LogWarning("GodotSteam is not available");
            EmitAllAchievementsReset(false, "GodotSteam is not available");
            return;
        }

        try
        {
            // ResetAllStats with true clears achievements too
            var success = Steam.ResetAllStats(true);
            if (success)
            {
                this.Log("Reset all achievements and stats");
                EmitAllAchievementsReset(true);
            }
            else
            {
                this.LogError("Failed to reset all achievements");
                EmitAllAchievementsReset(false, "ResetAllStats returned false");
            }
        }
        catch (Exception ex)
        {
            this.LogError($"Failed to reset all achievements: {ex.Message}");
            EmitAllAchievementsReset(false, ex.Message);
        }
    }

    #endregion

    #region Async Methods

    public override Task<AchievementUnlockResult> UnlockAchievementAsync(string achievementId)
    {
        var (steamId, error) = ValidateAndGetSteamId(achievementId);
        if (error != null)
            return Task.FromResult(AchievementUnlockResult.FailureResult(error));

        try
        {
            if (IsAchievementUnlocked(steamId!))
            {
                this.Log($"Achievement already unlocked: {steamId}");
                return Task.FromResult(AchievementUnlockResult.SuccessResult());
            }

            var success = Steam.SetAchievement(steamId!);
            if (success)
            {
                Steam.StoreStats();
                this.Log($"Unlocked achievement: {steamId}");
                return Task.FromResult(AchievementUnlockResult.SuccessResult());
            }
            else
            {
                return Task.FromResult(AchievementUnlockResult.FailureResult("SetAchievement returned false"));
            }
        }
        catch (Exception ex)
        {
            return Task.FromResult(AchievementUnlockResult.FailureResult(ex.Message));
        }
    }

    public override Task<int> GetProgressAsync(string achievementId)
    {
        var (steamId, error) = ValidateAndGetSteamId(achievementId);
        if (error != null)
            return Task.FromResult(0);

        try
        {
            var achievement = _database.GetById(achievementId)!;
            var statKey = GetStatKey(achievement);
            if (string.IsNullOrEmpty(statKey))
            {
                this.LogWarning($"Achievement '{achievementId}' has no Steam Stat ID configured");
                return Task.FromResult(0);
            }

            var progress = Steam.GetStatInt(statKey);
            return Task.FromResult(progress);
        }
        catch (Exception ex)
        {
            this.LogWarning($"Failed to get progress: {ex.Message}");
            return Task.FromResult(0);
        }
    }

    public override Task<SyncResult> IncrementProgressAsync(string achievementId, int amount)
    {
        var (steamId, error) = ValidateAndGetSteamId(achievementId);
        if (error != null)
            return Task.FromResult(SyncResult.FailureResult(error));

        if (amount <= 0)
            return Task.FromResult(SyncResult.FailureResult("Amount must be positive"));

        try
        {
            var achievement = _database.GetById(achievementId)!;
            var statKey = GetStatKey(achievement);
            if (string.IsNullOrEmpty(statKey))
            {
                return Task.FromResult(SyncResult.FailureResult($"Achievement '{achievementId}' has no Steam Stat ID configured"));
            }

            var currentProgress = Steam.GetStatInt(statKey);
            var newProgress = currentProgress + amount;
            Steam.SetStatInt(statKey, newProgress);
            Steam.StoreStats();

            // Auto-unlock if progress reached
            if (newProgress >= achievement.MaxProgress)
            {
                Steam.SetAchievement(steamId!);
                Steam.StoreStats();
            }

            this.Log($"Incremented progress for stat {statKey}: {newProgress}");
            return Task.FromResult(SyncResult.SuccessResult());
        }
        catch (Exception ex)
        {
            return Task.FromResult(SyncResult.FailureResult(ex.Message));
        }
    }

    public override Task<SyncResult> ResetAchievementAsync(string achievementId)
    {
        var (steamId, error) = ValidateAndGetSteamId(achievementId);
        if (error != null)
            return Task.FromResult(SyncResult.FailureResult(error));

        try
        {
            var success = Steam.ClearAchievement(steamId!);
            if (success)
            {
                Steam.StoreStats();
                this.Log($"Reset achievement: {steamId}");
                return Task.FromResult(SyncResult.SuccessResult());
            }
            else
            {
                return Task.FromResult(SyncResult.FailureResult("ClearAchievement returned false"));
            }
        }
        catch (Exception ex)
        {
            return Task.FromResult(SyncResult.FailureResult(ex.Message));
        }
    }

    public override Task<SyncResult> ResetAllAchievementsAsync()
    {
        if (!IsAvailable)
            return Task.FromResult(SyncResult.FailureResult("GodotSteam is not available"));

        try
        {
            var success = Steam.ResetAllStats(true);
            if (success)
            {
                this.Log("Reset all achievements and stats");
                return Task.FromResult(SyncResult.SuccessResult());
            }
            else
            {
                return Task.FromResult(SyncResult.FailureResult("ResetAllStats returned false"));
            }
        }
        catch (Exception ex)
        {
            return Task.FromResult(SyncResult.FailureResult(ex.Message));
        }
    }

    #endregion
}
#endif
