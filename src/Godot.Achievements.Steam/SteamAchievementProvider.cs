#if GODOT_PC || GODOT_WINDOWS || GODOT_LINUX || GODOT_MACOS
using System;
using System.Linq;
using System.Threading.Tasks;
using Godot.Achievements.Core;

namespace Godot.Achievements.Steam;

/// <summary>
/// Steam achievement provider using Steamworks.NET
/// Note: This requires Steamworks.NET to be installed and steam_appid.txt configured
/// </summary>
public class SteamAchievementProvider : IAchievementProvider
{
    private readonly AchievementDatabase _database;
    private bool _isInitialized;

    public string ProviderName => "Steam";

    public bool IsAvailable
    {
        get
        {
            // Check if Steamworks is initialized via reflection to avoid hard dependency
            // In a real implementation, you would check: return SteamAPI.IsSteamRunning();
            return _isInitialized && IsSteamworksAvailable();
        }
    }

    public SteamAchievementProvider(AchievementDatabase database)
    {
        _database = database;
        _isInitialized = InitializeSteamworks();
    }

    private bool InitializeSteamworks()
    {
        try
        {
            // In a real implementation with Steamworks.NET:
            // if (SteamAPI.RestartAppIfNecessary(new AppId_t(YOUR_APP_ID)))
            // {
            //     // App restarting through Steam
            //     return false;
            // }
            //
            // if (!SteamAPI.Init())
            // {
            //     GD.PushError("[Steam] SteamAPI.Init() failed");
            //     return false;
            // }

            // For now, we'll just log that we would initialize here
            GD.Print("[Steam] SteamAchievementProvider initialized (Steamworks.NET integration required)");
            return false; // Set to true when Steamworks.NET is properly integrated
        }
        catch (Exception ex)
        {
            GD.PushError($"[Steam] Failed to initialize: {ex.Message}");
            return false;
        }
    }

    public async Task<AchievementUnlockResult> UnlockAchievement(string achievementId)
    {
        if (!IsAvailable)
        {
            return AchievementUnlockResult.FailureResult("Steam is not available");
        }

        var achievement = _database.GetById(achievementId);
        if (achievement == null)
        {
            return AchievementUnlockResult.FailureResult($"Achievement '{achievementId}' not found");
        }

        // Get Steam achievement ID
        var steamId = achievement.SteamId;
        if (string.IsNullOrEmpty(steamId))
        {
            return AchievementUnlockResult.FailureResult($"Achievement '{achievementId}' has no Steam ID configured");
        }

        try
        {
            // Real implementation with Steamworks.NET:
            // bool success = SteamUserStats.SetAchievement(steamId);
            // if (success)
            // {
            //     success = SteamUserStats.StoreStats();
            // }
            //
            // if (!success)
            // {
            //     return AchievementUnlockResult.FailureResult("Failed to unlock Steam achievement");
            // }

            GD.Print($"[Steam] Would unlock achievement: {steamId}");

            // Simulate async operation
            await Task.Delay(10);

            return AchievementUnlockResult.SuccessResult();
        }
        catch (Exception ex)
        {
            return AchievementUnlockResult.FailureResult($"Steam exception: {ex.Message}");
        }
    }

    public async Task<Achievement?> GetAchievement(string achievementId)
    {
        var achievement = _database.GetById(achievementId);
        if (achievement == null)
            return null;

        if (!IsAvailable)
            return achievement;

        var steamId = achievement.SteamId;
        if (string.IsNullOrEmpty(steamId))
            return achievement;

        try
        {
            // Real implementation with Steamworks.NET:
            // bool achieved;
            // uint unlockTime;
            // if (SteamUserStats.GetAchievementAndUnlockTime(steamId, out achieved, out unlockTime))
            // {
            //     achievement.IsUnlocked = achieved;
            //     if (achieved && unlockTime > 0)
            //     {
            //         achievement.UnlockedAt = DateTimeOffset.FromUnixTimeSeconds(unlockTime).DateTime;
            //     }
            // }

            await Task.CompletedTask;
            return achievement;
        }
        catch (Exception ex)
        {
            GD.PushError($"[Steam] Error getting achievement: {ex.Message}");
            return achievement;
        }
    }

    public async Task<Achievement[]> GetAllAchievements()
    {
        if (!IsAvailable)
            return _database.Achievements.ToArray();

        try
        {
            // Real implementation with Steamworks.NET:
            // Request user stats from Steam
            // SteamUserStats.RequestCurrentStats();
            // Wait for callback...

            // Update all achievements with Steam state
            foreach (var achievement in _database.Achievements)
            {
                if (!string.IsNullOrEmpty(achievement.SteamId))
                {
                    // Update from Steam
                    await GetAchievement(achievement.Id);
                }
            }

            return _database.Achievements.ToArray();
        }
        catch (Exception ex)
        {
            GD.PushError($"[Steam] Error getting achievements: {ex.Message}");
            return _database.Achievements.ToArray();
        }
    }

    public async Task<float> GetProgress(string achievementId)
    {
        if (!IsAvailable)
            return 0f;

        var achievement = _database.GetById(achievementId);
        if (achievement == null || string.IsNullOrEmpty(achievement.SteamId))
            return 0f;

        try
        {
            // Real implementation with Steamworks.NET:
            // For progressive achievements, Steam uses stats
            // int statValue;
            // if (SteamUserStats.GetStat($"{achievement.SteamId}_STAT", out statValue))
            // {
            //     int maxValue = 100; // Get from achievement config
            //     return (float)statValue / maxValue;
            // }

            await Task.CompletedTask;
            return 0f;
        }
        catch (Exception ex)
        {
            GD.PushError($"[Steam] Error getting progress: {ex.Message}");
            return 0f;
        }
    }

    public async Task SetProgress(string achievementId, float progress)
    {
        if (!IsAvailable)
            return;

        var achievement = _database.GetById(achievementId);
        if (achievement == null || string.IsNullOrEmpty(achievement.SteamId))
            return;

        try
        {
            // Real implementation with Steamworks.NET:
            // For progressive achievements, update the stat
            // int maxValue = 100; // Get from achievement config
            // int statValue = (int)(progress * maxValue);
            // SteamUserStats.SetStat($"{achievement.SteamId}_STAT", statValue);
            // SteamUserStats.StoreStats();

            GD.Print($"[Steam] Would set progress for {achievement.SteamId}: {progress * 100:F1}%");

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            GD.PushError($"[Steam] Error setting progress: {ex.Message}");
        }
    }

    private bool IsSteamworksAvailable()
    {
        // Check if Steamworks.NET types are available via reflection
        try
        {
            var steamAPIType = Type.GetType("Steamworks.SteamAPI, Steamworks.NET");
            return steamAPIType != null;
        }
        catch
        {
            return false;
        }
    }
}
#endif
