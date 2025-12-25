#if GODOT_PC || GODOT_WINDOWS || GODOT_LINUX || GODOT_MACOS || GODOT_X11 || GODOT_OSX
using System;
using System.Reflection;
using System.Threading.Tasks;
using Godot.Achievements.Core;
using Steamworks;

namespace Godot.Achievements.Steam;

/// <summary>
/// Steam achievement provider for PC/Desktop platforms.
/// Automatically uses Godot.Steamworks.Net if available, otherwise falls back to direct Steamworks.NET calls.
/// </summary>
public class SteamAchievementProvider : IAchievementProvider
{
    public static bool IsPlatformSupported => true;

    private readonly AchievementDatabase _database;
    private bool _isInitialized;
    private bool _statsReceived;
    private bool _useGodotSteamworks;
    private TaskCompletionSource<bool>? _statsReceivedTcs;

    // Reflection-based access to Godot.Steamworks.Net
    private object? _godotSteamworksInstance;
    private object? _achievementsManager;
    private MethodInfo? _unlockMethod;
    private MethodInfo? _isUnlockedMethod;
    private MethodInfo? _resetMethod;
    private MethodInfo? _resetAllMethod;

    // Callback for when stats are received from Steam (used in fallback mode)
    private Callback<UserStatsReceived_t>? _userStatsReceivedCallback;

    public string ProviderName => ProviderNames.Steam;

    public SteamAchievementProvider(AchievementDatabase database)
    {
        _database = database;
        Initialize();
    }

    private void Initialize()
    {
        // First, try to use Godot.Steamworks.Net if available
        if (TryInitializeGodotSteamworks())
        {
            _useGodotSteamworks = true;
            _isInitialized = true;
            _statsReceived = true; // Godot.Steamworks.Net handles this internally
            this.Log("Using Godot.Steamworks.Net for Steam achievements");
            return;
        }

        // Fall back to direct Steamworks.NET initialization
        InitializeSteamworksDirect();
    }

    /// <summary>
    /// Try to initialize using Godot.Steamworks.Net via reflection
    /// </summary>
    private bool TryInitializeGodotSteamworks()
    {
        try
        {
            // Look for GodotSteamworks type
            var godotSteamworksType = Type.GetType("Godot.Steamworks.GodotSteamworks, Godot.Steamworks.Net");
            if (godotSteamworksType == null)
            {
                // Try alternative assembly names
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    godotSteamworksType = assembly.GetType("Godot.Steamworks.GodotSteamworks");
                    if (godotSteamworksType != null) break;
                }
            }

            if (godotSteamworksType == null)
                return false;

            // Get the Instance property
            var instanceProperty = godotSteamworksType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
            if (instanceProperty == null)
                return false;

            _godotSteamworksInstance = instanceProperty.GetValue(null);
            if (_godotSteamworksInstance == null)
                return false;

            // Get the Achievements property
            var achievementsProperty = godotSteamworksType.GetProperty("Achievements", BindingFlags.Public | BindingFlags.Instance);
            if (achievementsProperty == null)
                return false;

            _achievementsManager = achievementsProperty.GetValue(_godotSteamworksInstance);
            if (_achievementsManager == null)
                return false;

            // Cache the methods we need
            var achievementsType = _achievementsManager.GetType();
            _unlockMethod = achievementsType.GetMethod("Unlock", new[] { typeof(string) });
            _isUnlockedMethod = achievementsType.GetMethod("IsUnlocked", new[] { typeof(string) });
            _resetMethod = achievementsType.GetMethod("Reset", new[] { typeof(string) });
            _resetAllMethod = achievementsType.GetMethod("ResetAll", Type.EmptyTypes);

            return _unlockMethod != null;
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
            // Check if Steam is running and initialized
            if (!SteamAPI.Init())
            {
                this.LogWarning("Steam API failed to initialize. Make sure Steam is running and steam_appid.txt is present.");
                _isInitialized = false;
                return;
            }

            // Set up callback for receiving stats
            _userStatsReceivedCallback = Callback<UserStatsReceived_t>.Create(OnUserStatsReceived);

            // Request stats from Steam
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

    private void OnUserStatsReceived(UserStatsReceived_t callback)
    {
        if (callback.m_eResult == EResult.k_EResultOK)
        {
            _statsReceived = true;
            this.Log("Steam user stats received successfully");
            _statsReceivedTcs?.TrySetResult(true);
        }
        else
        {
            this.LogWarning($"Failed to receive Steam user stats: {callback.m_eResult}");
            _statsReceivedTcs?.TrySetResult(false);
        }
    }

    /// <summary>
    /// Wait for stats to be received from Steam (with timeout)
    /// </summary>
    private async Task<bool> EnsureStatsReceived()
    {
        if (_statsReceived)
            return true;

        if (_statsReceivedTcs == null)
        {
            _statsReceivedTcs = new TaskCompletionSource<bool>();
        }

        // Wait up to 5 seconds for stats
        var timeoutTask = Task.Delay(5000);
        var completedTask = await Task.WhenAny(_statsReceivedTcs.Task, timeoutTask);

        if (completedTask == timeoutTask)
        {
            this.LogWarning("Timeout waiting for Steam stats");
            return false;
        }

        return await _statsReceivedTcs.Task;
    }

    public bool IsAvailable => _isInitialized && (_useGodotSteamworks || SteamAPI.IsSteamRunning());

    public async Task<AchievementUnlockResult> UnlockAchievement(string achievementId)
    {
        if (!IsAvailable)
            return AchievementUnlockResult.FailureResult("Steam is not available");

        var achievement = _database.GetById(achievementId);
        if (achievement == null)
            return AchievementUnlockResult.FailureResult($"Achievement '{achievementId}' not found");

        var steamId = achievement.SteamId;
        if (string.IsNullOrEmpty(steamId))
            return AchievementUnlockResult.FailureResult($"Achievement '{achievementId}' has no Steam ID configured");

        try
        {
            if (_useGodotSteamworks && _achievementsManager != null && _unlockMethod != null)
            {
                // Check if already unlocked
                if (_isUnlockedMethod != null)
                {
                    var isUnlocked = (bool?)_isUnlockedMethod.Invoke(_achievementsManager, new object[] { steamId });
                    if (isUnlocked == true)
                    {
                        this.Log($"Achievement '{steamId}' was already unlocked");
                        return AchievementUnlockResult.SuccessResult(wasAlreadyUnlocked: true);
                    }
                }

                // Unlock via Godot.Steamworks.Net
                _unlockMethod.Invoke(_achievementsManager, new object[] { steamId });
                this.Log($"Unlocked Steam achievement via Godot.Steamworks.Net: {steamId}");
                return AchievementUnlockResult.SuccessResult();
            }
            else
            {
                // Fall back to direct Steamworks.NET
                return await UnlockAchievementDirect(steamId);
            }
        }
        catch (Exception ex)
        {
            return AchievementUnlockResult.FailureResult($"Steam exception: {ex.Message}");
        }
    }

    private async Task<AchievementUnlockResult> UnlockAchievementDirect(string steamId)
    {
        if (!await EnsureStatsReceived())
            return AchievementUnlockResult.FailureResult("Failed to receive Steam stats");

        // Check if already unlocked
        if (SteamUserStats.GetAchievement(steamId, out bool alreadyUnlocked) && alreadyUnlocked)
        {
            this.Log($"Achievement '{steamId}' was already unlocked");
            return AchievementUnlockResult.SuccessResult(wasAlreadyUnlocked: true);
        }

        // Unlock the achievement
        if (!SteamUserStats.SetAchievement(steamId))
        {
            return AchievementUnlockResult.FailureResult($"Failed to set achievement '{steamId}'");
        }

        // Store the stats to Steam
        if (!SteamUserStats.StoreStats())
        {
            return AchievementUnlockResult.FailureResult($"Failed to store stats for achievement '{steamId}'");
        }

        this.Log($"Unlocked Steam achievement: {steamId}");
        return AchievementUnlockResult.SuccessResult();
    }

    public async Task<int> GetProgress(string achievementId)
    {
        if (!IsAvailable)
            return 0;

        var achievement = _database.GetById(achievementId);
        if (achievement == null)
            return 0;

        // Godot.Steamworks.Net doesn't expose progress API, so always use direct access
        if (!_useGodotSteamworks)
        {
            if (!await EnsureStatsReceived())
                return 0;
        }

        // Steam doesn't have native progress for achievements, but we can use stats
        // Convention: use the SteamId + "_PROGRESS" as the stat name
        var statName = $"{achievement.SteamId}_PROGRESS";

        try
        {
            if (SteamUserStats.GetStat(statName, out int progress))
            {
                return progress;
            }
        }
        catch (Exception ex)
        {
            this.LogWarning($"Failed to get progress stat '{statName}': {ex.Message}");
        }

        return 0;
    }

    public async Task<SyncResult> SetProgress(string achievementId, int currentProgress)
    {
        if (!IsAvailable)
            return SyncResult.FailureResult("Steam is not available");

        var achievement = _database.GetById(achievementId);
        if (achievement == null)
            return SyncResult.FailureResult($"Achievement '{achievementId}' not found");

        if (string.IsNullOrEmpty(achievement.SteamId))
            return SyncResult.FailureResult($"Achievement '{achievementId}' has no Steam ID configured");

        // Progress tracking requires direct Steamworks.NET access
        if (!_useGodotSteamworks)
        {
            if (!await EnsureStatsReceived())
                return SyncResult.FailureResult("Failed to receive Steam stats");
        }

        try
        {
            // Use stat for progress tracking
            var statName = $"{achievement.SteamId}_PROGRESS";

            if (!SteamUserStats.SetStat(statName, currentProgress))
            {
                this.LogWarning($"Failed to set stat '{statName}' - stat may not exist in Steamworks partner portal");
            }

            // Show progress indicator to user (this is a Steam overlay notification)
            SteamUserStats.IndicateAchievementProgress(
                achievement.SteamId,
                (uint)currentProgress,
                (uint)achievement.MaxProgress
            );

            // If progress reached max, unlock the achievement
            if (currentProgress >= achievement.MaxProgress)
            {
                var unlockResult = await UnlockAchievement(achievementId);
                if (!unlockResult.Success)
                {
                    return SyncResult.FailureResult(unlockResult.Error ?? "Failed to unlock achievement");
                }
            }
            else
            {
                // Store stats to Steam
                if (!SteamUserStats.StoreStats())
                {
                    return SyncResult.FailureResult("Failed to store stats to Steam");
                }
            }

            var percentage = achievement.MaxProgress > 0 ? (float)currentProgress / achievement.MaxProgress * 100 : 0;
            this.Log($"Set progress for '{achievement.SteamId}': {currentProgress}/{achievement.MaxProgress} ({percentage:F1}%)");

            return SyncResult.SuccessResult();
        }
        catch (Exception ex)
        {
            return SyncResult.FailureResult($"Steam exception: {ex.Message}");
        }
    }

    public async Task<SyncResult> ResetAchievement(string achievementId)
    {
        if (!IsAvailable)
            return SyncResult.FailureResult("Steam is not available");

        var achievement = _database.GetById(achievementId);
        if (achievement == null)
            return SyncResult.FailureResult($"Achievement '{achievementId}' not found");

        if (string.IsNullOrEmpty(achievement.SteamId))
            return SyncResult.FailureResult($"Achievement '{achievementId}' has no Steam ID configured");

        try
        {
            if (_useGodotSteamworks && _achievementsManager != null && _resetMethod != null)
            {
                // Reset via Godot.Steamworks.Net
                _resetMethod.Invoke(_achievementsManager, new object[] { achievement.SteamId });
                this.Log($"Reset Steam achievement via Godot.Steamworks.Net: {achievement.SteamId}");
                return SyncResult.SuccessResult();
            }
            else
            {
                // Fall back to direct Steamworks.NET
                return await ResetAchievementDirect(achievement.SteamId);
            }
        }
        catch (Exception ex)
        {
            return SyncResult.FailureResult($"Steam exception: {ex.Message}");
        }
    }

    private async Task<SyncResult> ResetAchievementDirect(string steamId)
    {
        if (!await EnsureStatsReceived())
            return SyncResult.FailureResult("Failed to receive Steam stats");

        // Clear the achievement
        if (!SteamUserStats.ClearAchievement(steamId))
        {
            return SyncResult.FailureResult($"Failed to clear achievement '{steamId}'");
        }

        // Also reset the progress stat if it exists
        var statName = $"{steamId}_PROGRESS";
        SteamUserStats.SetStat(statName, 0);

        // Store changes to Steam
        if (!SteamUserStats.StoreStats())
        {
            return SyncResult.FailureResult("Failed to store stats to Steam");
        }

        this.Log($"Reset Steam achievement: {steamId}");
        return SyncResult.SuccessResult();
    }

    public async Task<SyncResult> ResetAllAchievements()
    {
        if (!IsAvailable)
            return SyncResult.FailureResult("Steam is not available");

        try
        {
            if (_useGodotSteamworks && _achievementsManager != null && _resetAllMethod != null)
            {
                // Reset via Godot.Steamworks.Net
                _resetAllMethod.Invoke(_achievementsManager, null);
                this.Log("Reset all Steam achievements via Godot.Steamworks.Net");
                return SyncResult.SuccessResult();
            }
            else
            {
                // Fall back to direct Steamworks.NET
                return await ResetAllAchievementsDirect();
            }
        }
        catch (Exception ex)
        {
            return SyncResult.FailureResult($"Steam exception: {ex.Message}");
        }
    }

    private async Task<SyncResult> ResetAllAchievementsDirect()
    {
        if (!await EnsureStatsReceived())
            return SyncResult.FailureResult("Failed to receive Steam stats");

        // Reset all stats including achievements
        if (!SteamUserStats.ResetAllStats(bAchievementsToo: true))
        {
            return SyncResult.FailureResult("Failed to reset all Steam stats and achievements");
        }

        // Re-request stats after reset
        SteamUserStats.RequestCurrentStats();

        this.Log("Reset all Steam achievements and stats");
        return SyncResult.SuccessResult();
    }

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
            _statsReceived = false;
            this.Log("Steam API shutdown");
        }
    }
}
#endif
