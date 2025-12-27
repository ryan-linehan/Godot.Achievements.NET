#if GODOT_PC || GODOT_WINDOWS || GODOT_LINUX || GODOT_MACOS || GODOT_X11 || GODOT_OSX
using System;
using System.Threading.Tasks;
using Godot.Achievements.Core;
using Godot.Achievements.Providers;
using Steamworks;

namespace Godot.Achievements.Providers.Steamworks;

/// <summary>
/// Steam achievement provider for PC/Desktop platforms.
/// Automatically uses Godot.Steamworks.Net if available as an autoload, otherwise falls back to direct Steamworks.NET calls.
/// Steamworks.NET operations are synchronous, so sync methods are the primary implementation.
/// </summary>
public partial class SteamAchievementProvider : AchievementProviderBase
{
    public static bool IsPlatformSupported => true;

    private readonly AchievementDatabase _database;
    private bool _isInitialized;
    private bool _useGodotSteamworks;

    // Reference to Godot.Steamworks.Net autoload (if available)
    private GodotObject? _godotSteamworks;
    private GodotObject? _achievementsManager;

    public override string ProviderName => ProviderNames.Steam;

    public SteamAchievementProvider(AchievementDatabase database)
    {
        _database = database;
        Initialize();
    }

    private void Initialize()
    {
        // First, try to use Godot.Steamworks.Net if available as an autoload
        if (TryInitializeGodotSteamworks())
        {
            _useGodotSteamworks = true;
            _isInitialized = true;
            this.Log("Using Godot.Steamworks.Net for Steam achievements");
            return;
        }

        // Fall back to direct Steamworks.NET initialization
        InitializeSteamworksDirect();
    }

    /// <summary>
    /// Try to find and use Godot.Steamworks.Net autoload singleton
    /// </summary>
    private bool TryInitializeGodotSteamworks()
    {
        try
        {
            var mainLoop = Engine.GetMainLoop();
            if (mainLoop is not SceneTree sceneTree)
                return false;

            if (!sceneTree.Root.HasNode("GodotSteamworks"))
                return false;

            _godotSteamworks = sceneTree.Root.GetNode("GodotSteamworks");
            if (_godotSteamworks == null)
                return false;

            _achievementsManager = _godotSteamworks.Get("Achievements").AsGodotObject();
            if (_achievementsManager == null)
                return false;

            var isInitialized = _godotSteamworks.Get("IsInitialized").AsBool();
            if (!isInitialized)
            {
                this.LogWarning("Godot.Steamworks.Net found but not initialized");
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            this.LogWarning($"Failed to initialize Godot.Steamworks.Net integration: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Initialize Steam directly using Steamworks.NET
    /// </summary>
    private void InitializeSteamworksDirect()
    {
        try
        {
            if (!SteamAPI.Init())
            {
                this.LogWarning("Steam API failed to initialize. Make sure Steam is running and steam_appid.txt is present.");
                _isInitialized = false;
                return;
            }

            if (!SteamUserStats.RequestCurrentStats())
            {
                this.LogWarning("Failed to request current stats from Steam");
            }

            _isInitialized = true;
            this.Log("Steam API initialized directly via Steamworks.NET");
        }
        catch (Exception ex)
        {
            this.LogError($"Failed to initialize Steam: {ex.Message}");
            _isInitialized = false;
        }
    }

    public override bool IsAvailable => _isInitialized && IsSteamAvailable();

    private bool IsSteamAvailable()
    {
        if (_useGodotSteamworks)
        {
            return _godotSteamworks?.Get("IsInitialized").AsBool() ?? false;
        }
        return SteamAPI.IsSteamRunning();
    }

    #region Helper Methods

    private (string? steamId, string? error) ValidateAndGetSteamId(string achievementId)
    {
        if (!_isInitialized)
            return (null, "Steam not initialized");

        if (!IsAvailable)
            return (null, "Steam is not available");

        var achievement = _database.GetById(achievementId);
        if (achievement == null)
            return (null, $"Achievement '{achievementId}' not found in database");

        var steamId = achievement.SteamId;
        if (string.IsNullOrEmpty(steamId))
            return (null, $"Achievement '{achievementId}' has no Steam ID configured");

        return (steamId, null);
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
            if (_useGodotSteamworks && _achievementsManager != null)
            {
                _achievementsManager.Call("Unlock", steamId);
                this.Log($"Unlocked achievement via Godot.Steamworks.Net: {steamId}");
                EmitAchievementUnlocked(achievementId, true);
            }
            else
            {
                bool success = SteamUserStats.SetAchievement(steamId!);
                if (success) success = SteamUserStats.StoreStats();

                if (success)
                {
                    this.Log($"Unlocked achievement: {steamId}");
                    EmitAchievementUnlocked(achievementId, true);
                }
                else
                {
                    this.LogWarning($"Failed to unlock achievement: {steamId}");
                    EmitAchievementUnlocked(achievementId, false, "Failed to unlock achievement");
                }
            }
        }
        catch (Exception ex)
        {
            this.LogError($"Steam exception: {ex.Message}");
            EmitAchievementUnlocked(achievementId, false, $"Steam exception: {ex.Message}");
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
            // Steam uses stats for progress tracking
            var statName = $"{steamId}_PROGRESS";
            int currentProgress = 0;
            SteamUserStats.GetStat(statName, out currentProgress);

            int newProgress = currentProgress + amount;
            bool success = SteamUserStats.SetStat(statName, newProgress);
            if (success) success = SteamUserStats.StoreStats();

            if (success)
            {
                this.Log($"Incremented progress for {steamId}: {currentProgress} -> {newProgress}");
                EmitProgressIncremented(achievementId, newProgress, true);

                // Check if achievement should be unlocked
                var achievement = _database.GetById(achievementId);
                if (achievement != null && newProgress >= achievement.MaxProgress)
                {
                    UnlockAchievement(achievementId);
                }
            }
            else
            {
                this.LogWarning($"Failed to increment progress for {steamId}");
                EmitProgressIncremented(achievementId, currentProgress, false, "Failed to increment progress");
            }
        }
        catch (Exception ex)
        {
            this.LogError($"Steam exception: {ex.Message}");
            EmitProgressIncremented(achievementId, 0, false, $"Steam exception: {ex.Message}");
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
            if (_useGodotSteamworks && _achievementsManager != null)
            {
                _achievementsManager.Call("Reset", steamId);
                this.Log($"Reset achievement via Godot.Steamworks.Net: {steamId}");
                EmitAchievementReset(achievementId, true);
            }
            else
            {
                bool success = SteamUserStats.ClearAchievement(steamId!);

                // Also reset the progress stat
                var statName = $"{steamId}_PROGRESS";
                SteamUserStats.SetStat(statName, 0);

                if (success) success = SteamUserStats.StoreStats();

                if (success)
                {
                    this.Log($"Reset achievement: {steamId}");
                    EmitAchievementReset(achievementId, true);
                }
                else
                {
                    this.LogWarning($"Failed to reset achievement: {steamId}");
                    EmitAchievementReset(achievementId, false, "Failed to reset achievement");
                }
            }
        }
        catch (Exception ex)
        {
            this.LogError($"Steam exception: {ex.Message}");
            EmitAchievementReset(achievementId, false, $"Steam exception: {ex.Message}");
        }
    }

    public override void ResetAllAchievements()
    {
        if (!IsAvailable)
        {
            this.LogWarning("Steam is not available");
            EmitAllAchievementsReset(false, "Steam is not available");
            return;
        }

        try
        {
            if (_useGodotSteamworks && _achievementsManager != null)
            {
                _achievementsManager.Call("ResetAll");
                this.Log("Reset all achievements via Godot.Steamworks.Net");
                EmitAllAchievementsReset(true);
            }
            else
            {
                bool success = SteamUserStats.ResetAllStats(bAchievementsToo: true);

                if (success)
                {
                    this.Log("Reset all achievements");
                    EmitAllAchievementsReset(true);
                }
                else
                {
                    this.LogWarning("Failed to reset all achievements");
                    EmitAllAchievementsReset(false, "Failed to reset all achievements");
                }
            }
        }
        catch (Exception ex)
        {
            this.LogError($"Steam exception: {ex.Message}");
            EmitAllAchievementsReset(false, $"Steam exception: {ex.Message}");
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
            if (_useGodotSteamworks && _achievementsManager != null)
            {
                // Check if already unlocked
                var isUnlocked = _achievementsManager.Call("IsUnlocked", steamId).AsBool();
                if (isUnlocked)
                {
                    this.Log($"Achievement already unlocked: {steamId}");
                    return Task.FromResult(AchievementUnlockResult.SuccessResult(wasAlreadyUnlocked: true));
                }

                _achievementsManager.Call("Unlock", steamId);
                this.Log($"Unlocked achievement via Godot.Steamworks.Net: {steamId}");
                return Task.FromResult(AchievementUnlockResult.SuccessResult());
            }
            else
            {
                // Check if already unlocked
                if (SteamUserStats.GetAchievement(steamId!, out bool alreadyUnlocked) && alreadyUnlocked)
                {
                    this.Log($"Achievement already unlocked: {steamId}");
                    return Task.FromResult(AchievementUnlockResult.SuccessResult(wasAlreadyUnlocked: true));
                }

                bool success = SteamUserStats.SetAchievement(steamId!);
                if (success) success = SteamUserStats.StoreStats();

                if (success)
                {
                    this.Log($"Unlocked achievement: {steamId}");
                    return Task.FromResult(AchievementUnlockResult.SuccessResult());
                }
                else
                {
                    return Task.FromResult(AchievementUnlockResult.FailureResult("Failed to unlock achievement"));
                }
            }
        }
        catch (Exception ex)
        {
            return Task.FromResult(AchievementUnlockResult.FailureResult($"Steam exception: {ex.Message}"));
        }
    }

    public override Task<int> GetProgressAsync(string achievementId)
    {
        var (steamId, error) = ValidateAndGetSteamId(achievementId);
        if (error != null)
            return Task.FromResult(0);

        try
        {
            var statName = $"{steamId}_PROGRESS";
            if (SteamUserStats.GetStat(statName, out int progress))
            {
                return Task.FromResult(progress);
            }
        }
        catch (Exception ex)
        {
            this.LogWarning($"Failed to get progress: {ex.Message}");
        }

        return Task.FromResult(0);
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
            var statName = $"{steamId}_PROGRESS";
            int currentProgress = 0;
            SteamUserStats.GetStat(statName, out currentProgress);

            int newProgress = currentProgress + amount;
            bool success = SteamUserStats.SetStat(statName, newProgress);

            // Show progress indicator
            var achievement = _database.GetById(achievementId);
            if (achievement != null)
            {
                SteamUserStats.IndicateAchievementProgress(
                    steamId!,
                    (uint)newProgress,
                    (uint)achievement.MaxProgress
                );

                // Auto-unlock if progress reached
                if (newProgress >= achievement.MaxProgress)
                {
                    SteamUserStats.SetAchievement(steamId!);
                }
            }

            if (success) success = SteamUserStats.StoreStats();

            if (success)
            {
                this.Log($"Incremented progress for {steamId}: {newProgress}");
                return Task.FromResult(SyncResult.SuccessResult());
            }
            else
            {
                return Task.FromResult(SyncResult.FailureResult("Failed to increment progress"));
            }
        }
        catch (Exception ex)
        {
            return Task.FromResult(SyncResult.FailureResult($"Steam exception: {ex.Message}"));
        }
    }

    public override Task<SyncResult> ResetAchievementAsync(string achievementId)
    {
        var (steamId, error) = ValidateAndGetSteamId(achievementId);
        if (error != null)
            return Task.FromResult(SyncResult.FailureResult(error));

        try
        {
            if (_useGodotSteamworks && _achievementsManager != null)
            {
                _achievementsManager.Call("Reset", steamId);
                this.Log($"Reset achievement via Godot.Steamworks.Net: {steamId}");
                return Task.FromResult(SyncResult.SuccessResult());
            }
            else
            {
                bool success = SteamUserStats.ClearAchievement(steamId!);

                var statName = $"{steamId}_PROGRESS";
                SteamUserStats.SetStat(statName, 0);

                if (success) success = SteamUserStats.StoreStats();

                if (success)
                {
                    this.Log($"Reset achievement: {steamId}");
                    return Task.FromResult(SyncResult.SuccessResult());
                }
                else
                {
                    return Task.FromResult(SyncResult.FailureResult("Failed to reset achievement"));
                }
            }
        }
        catch (Exception ex)
        {
            return Task.FromResult(SyncResult.FailureResult($"Steam exception: {ex.Message}"));
        }
    }

    public override Task<SyncResult> ResetAllAchievementsAsync()
    {
        if (!IsAvailable)
            return Task.FromResult(SyncResult.FailureResult("Steam is not available"));

        try
        {
            if (_useGodotSteamworks && _achievementsManager != null)
            {
                _achievementsManager.Call("ResetAll");
                this.Log("Reset all achievements via Godot.Steamworks.Net");
                return Task.FromResult(SyncResult.SuccessResult());
            }
            else
            {
                bool success = SteamUserStats.ResetAllStats(bAchievementsToo: true);

                if (success)
                {
                    this.Log("Reset all achievements");
                    return Task.FromResult(SyncResult.SuccessResult());
                }
                else
                {
                    return Task.FromResult(SyncResult.FailureResult("Failed to reset all achievements"));
                }
            }
        }
        catch (Exception ex)
        {
            return Task.FromResult(SyncResult.FailureResult($"Steam exception: {ex.Message}"));
        }
    }

    #endregion

    #region Steam-Specific Methods

    /// <summary>
    /// Call this method in your game's process loop to handle Steam callbacks.
    /// Not needed if using Godot.Steamworks.Net (it handles this automatically).
    /// </summary>
    public void RunCallbacks()
    {
        if (_isInitialized && !_useGodotSteamworks)
        {
            SteamAPI.RunCallbacks();
        }
    }

    /// <summary>
    /// Shutdown the Steam API when done.
    /// Not needed if using Godot.Steamworks.Net (it handles this automatically).
    /// </summary>
    public void Shutdown()
    {
        if (_isInitialized && !_useGodotSteamworks)
        {
            SteamAPI.Shutdown();
            _isInitialized = false;
            this.Log("Steam API shutdown");
        }
    }

    #endregion
}
#endif
