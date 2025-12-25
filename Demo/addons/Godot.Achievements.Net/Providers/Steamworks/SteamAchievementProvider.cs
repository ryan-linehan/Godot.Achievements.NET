#if GODOT_PC || GODOT_WINDOWS || GODOT_LINUX || GODOT_MACOS || GODOT_X11 || GODOT_OSX
using System;
using System.Threading.Tasks;
using Godot.Achievements.Core;
using Godot.Achievements.Providers;

namespace Godot.Achievements.Providers.Steamworks;

/// <summary>
/// Steam achievement provider for PC/Desktop platforms
/// </summary>
public class SteamAchievementProvider : IAchievementProvider
{
    public static bool IsPlatformSupported => true;

    private readonly AchievementDatabase _database;
    private bool _isInitialized;

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
            // UNCOMMENT when Steamworks.NET is installed:
            // if (SteamAPI.RestartAppIfNecessary(new AppId_t(YOUR_APP_ID)))
            // {
            //     _isInitialized = false;
            //     return;
            // }
            // _isInitialized = SteamAPI.Init();

            this.Log("Initialized (Steamworks.NET integration required)");
            _isInitialized = false;
        }
        catch (Exception ex)
        {
            this.LogError($"Failed to initialize: {ex.Message}");
            _isInitialized = false;
        }
    }

    public bool IsAvailable => _isInitialized && IsSteamworksAvailable();

    private bool IsSteamworksAvailable()
    {
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
            // UNCOMMENT:
            // bool success = SteamUserStats.SetAchievement(steamId);
            // if (success) success = SteamUserStats.StoreStats();
            // if (!success) return AchievementUnlockResult.FailureResult("Failed to unlock");

            this.Log($"Would unlock achievement: {steamId}");
            await Task.Delay(10);
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

        // UNCOMMENT: Load progress from Steamworks
        await Task.CompletedTask;
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

        // UNCOMMENT: Report progress to Steamworks
        float percentage = achievement.MaxProgress > 0 ? (float)currentProgress / achievement.MaxProgress * 100 : 0;
        this.Log($"Would set progress for {achievement.SteamId}: {currentProgress}/{achievement.MaxProgress} ({percentage:F1}%)");
        await Task.CompletedTask;
        return SyncResult.SuccessResult();
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

        // UNCOMMENT: SteamUserStats.ClearAchievement(achievement.SteamId);
        this.Log($"Would reset achievement: {achievement.SteamId}");
        await Task.CompletedTask;
        return SyncResult.SuccessResult();
    }

    public async Task<SyncResult> ResetAllAchievements()
    {
        if (!IsAvailable)
            return SyncResult.FailureResult("Steam is not available");

        // UNCOMMENT: SteamUserStats.ResetAllStats(true);
        this.Log("Would reset all achievements");
        await Task.CompletedTask;
        return SyncResult.SuccessResult();
    }
}
#endif
