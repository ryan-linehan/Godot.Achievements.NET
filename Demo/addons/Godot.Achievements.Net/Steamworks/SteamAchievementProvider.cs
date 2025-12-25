#if GODOT_PC || GODOT_WINDOWS || GODOT_LINUX || GODOT_MACOS || GODOT_X11 || GODOT_OSX
using System;
using System.Threading.Tasks;
using Godot.Achievements.Core;
using Steamworks;

namespace Godot.Achievements.Steam;

/// <summary>
/// Steam achievement provider for PC/Desktop platforms using Steamworks.NET
/// </summary>
public class SteamAchievementProvider : IAchievementProvider
{
    public static bool IsPlatformSupported => true;

    private readonly AchievementDatabase _database;
    private bool _isInitialized;
    private bool _statsReceived;
    private TaskCompletionSource<bool>? _statsReceivedTcs;

    // Callback for when stats are received from Steam
    private Callback<UserStatsReceived_t>? _userStatsReceivedCallback;

    public string ProviderName => ProviderNames.Steam;

    public SteamAchievementProvider(AchievementDatabase database)
    {
        _database = database;
        Initialize();
    }

    private void Initialize()
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
            this.Log("Steam API initialized successfully");
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

    public bool IsAvailable => _isInitialized && SteamAPI.IsSteamRunning();

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

        // Ensure we have stats before trying to set achievements
        if (!await EnsureStatsReceived())
            return AchievementUnlockResult.FailureResult("Failed to receive Steam stats");

        try
        {
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
        catch (Exception ex)
        {
            return AchievementUnlockResult.FailureResult($"Steam exception: {ex.Message}");
        }
    }

    public async Task<int> GetProgress(string achievementId)
    {
        if (!IsAvailable)
            return 0;

        var achievement = _database.GetById(achievementId);
        if (achievement == null)
            return 0;

        // Ensure we have stats
        if (!await EnsureStatsReceived())
            return 0;

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

        // Ensure we have stats
        if (!await EnsureStatsReceived())
            return SyncResult.FailureResult("Failed to receive Steam stats");

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
                if (!SteamUserStats.SetAchievement(achievement.SteamId))
                {
                    return SyncResult.FailureResult($"Failed to set achievement '{achievement.SteamId}'");
                }
            }

            // Store stats to Steam
            if (!SteamUserStats.StoreStats())
            {
                return SyncResult.FailureResult("Failed to store stats to Steam");
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

        // Ensure we have stats
        if (!await EnsureStatsReceived())
            return SyncResult.FailureResult("Failed to receive Steam stats");

        try
        {
            // Clear the achievement
            if (!SteamUserStats.ClearAchievement(achievement.SteamId))
            {
                return SyncResult.FailureResult($"Failed to clear achievement '{achievement.SteamId}'");
            }

            // Also reset the progress stat if it exists
            var statName = $"{achievement.SteamId}_PROGRESS";
            SteamUserStats.SetStat(statName, 0);

            // Store changes to Steam
            if (!SteamUserStats.StoreStats())
            {
                return SyncResult.FailureResult("Failed to store stats to Steam");
            }

            this.Log($"Reset Steam achievement: {achievement.SteamId}");
            return SyncResult.SuccessResult();
        }
        catch (Exception ex)
        {
            return SyncResult.FailureResult($"Steam exception: {ex.Message}");
        }
    }

    public async Task<SyncResult> ResetAllAchievements()
    {
        if (!IsAvailable)
            return SyncResult.FailureResult("Steam is not available");

        // Ensure we have stats
        if (!await EnsureStatsReceived())
            return SyncResult.FailureResult("Failed to receive Steam stats");

        try
        {
            // Reset all stats including achievements
            // The parameter 'true' means also reset achievements
            if (!SteamUserStats.ResetAllStats(bAchievementsToo: true))
            {
                return SyncResult.FailureResult("Failed to reset all Steam stats and achievements");
            }

            // Re-request stats after reset
            SteamUserStats.RequestCurrentStats();

            this.Log("Reset all Steam achievements and stats");
            return SyncResult.SuccessResult();
        }
        catch (Exception ex)
        {
            return SyncResult.FailureResult($"Steam exception: {ex.Message}");
        }
    }

    /// <summary>
    /// Call this method in your game's process loop to handle Steam callbacks
    /// </summary>
    public void RunCallbacks()
    {
        if (_isInitialized)
        {
            SteamAPI.RunCallbacks();
        }
    }

    /// <summary>
    /// Shutdown the Steam API when done
    /// </summary>
    public void Shutdown()
    {
        if (_isInitialized)
        {
            SteamAPI.Shutdown();
            _isInitialized = false;
            _statsReceived = false;
            this.Log("Steam API shutdown");
        }
    }
}
#endif
