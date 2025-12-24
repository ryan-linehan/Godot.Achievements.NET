#if GODOT_PC || GODOT_WINDOWS || GODOT_LINUX || GODOT_MACOS || GODOT_X11 || GODOT_OSX
using System;
using System.Threading.Tasks;
using Godot.Achievements.Core;

namespace Godot.Achievements.Steam;

/// <summary>
/// Steam achievement provider for PC/Desktop platforms
/// </summary>
public class SteamAchievementProvider : IAchievementProvider
{
    public static bool IsPlatformSupported => true;

    private readonly AchievementDatabase _database;
    private bool _isInitialized;

    public string ProviderName => "Steam";

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

            GD.Print("[Achievements] [Steam] Initialized (Steamworks.NET integration required)");
            _isInitialized = false;
        }
        catch (Exception ex)
        {
            GD.PushError($"[Achievements] [Steam] Failed to initialize: {ex.Message}");
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

    public async Task SetProgress(string achievementId, int currentProgress)
    {
        if (!IsAvailable)
            return;

        var achievement = _database.GetById(achievementId);
        if (achievement == null || string.IsNullOrEmpty(achievement.SteamId))
            return;

        // UNCOMMENT: Report progress to Steamworks
        float percentage = achievement.MaxProgress > 0 ? (float)currentProgress / achievement.MaxProgress * 100 : 0;
        this.Log($"Would set progress for {achievement.SteamId}: {currentProgress}/{achievement.MaxProgress} ({percentage:F1}%)");
        await Task.CompletedTask;
    }

    public async Task<bool> ResetAchievement(string achievementId)
    {
        if (!IsAvailable)
            return false;

        var achievement = _database.GetById(achievementId);
        if (achievement == null || string.IsNullOrEmpty(achievement.SteamId))
            return false;

        // UNCOMMENT: SteamUserStats.ClearAchievement(achievement.SteamId);
        this.Log($"Would reset achievement: {achievement.SteamId}");
        await Task.CompletedTask;
        return true;
    }

    public async Task<bool> ResetAllAchievements()
    {
        if (!IsAvailable)
            return false;

        // UNCOMMENT: SteamUserStats.ResetAllStats(true);
        this.Log("Would reset all achievements");
        await Task.CompletedTask;
        return true;
    }
}
#endif
