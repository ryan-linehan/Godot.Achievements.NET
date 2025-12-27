#if GODOT_PC || GODOT_WINDOWS || GODOT_LINUX || GODOT_MACOS || GODOT_X11 || GODOT_OSX
using System;
using System.Threading.Tasks;
using Godot.Achievements.Core;
using Godot.Achievements.Providers;

namespace Godot.Achievements.Providers.Steamworks;

/// <summary>
/// Steam achievement provider for PC/Desktop platforms.
/// Steamworks.NET operations are synchronous, so sync methods are the primary implementation.
/// </summary>
public partial class SteamAchievementProvider : AchievementProviderBase
{
    public static bool IsPlatformSupported => true;

    private readonly AchievementDatabase _database;
    private bool _isInitialized;

    public override string ProviderName => ProviderNames.Steam;

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

    public override bool IsAvailable => _isInitialized && IsSteamworksAvailable();

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

    #region Sync Methods

    public override void UnlockAchievement(string achievementId)
    {
        if (!IsAvailable)
        {
            this.LogWarning("Steam is not available");
            EmitAchievementUnlocked(achievementId, false, "Steam is not available");
            return;
        }

        var achievement = _database.GetById(achievementId);
        if (achievement == null)
        {
            this.LogWarning($"Achievement '{achievementId}' not found");
            EmitAchievementUnlocked(achievementId, false, $"Achievement '{achievementId}' not found");
            return;
        }

        var steamId = achievement.SteamId;
        if (string.IsNullOrEmpty(steamId))
        {
            this.LogWarning($"Achievement '{achievementId}' has no Steam ID configured");
            EmitAchievementUnlocked(achievementId, false, $"Achievement '{achievementId}' has no Steam ID configured");
            return;
        }

        try
        {
            // UNCOMMENT:
            // bool success = SteamUserStats.SetAchievement(steamId);
            // if (success) SteamUserStats.StoreStats();

            this.Log($"Would unlock achievement: {steamId}");
            EmitAchievementUnlocked(achievementId, true);
        }
        catch (Exception ex)
        {
            this.LogError($"Steam exception: {ex.Message}");
            EmitAchievementUnlocked(achievementId, false, $"Steam exception: {ex.Message}");
        }
    }

    public override void IncrementProgress(string achievementId, int amount)
    {
        if (!IsAvailable)
        {
            this.LogWarning("Steam is not available");
            EmitProgressIncremented(achievementId, 0, false, "Steam is not available");
            return;
        }

        var achievement = _database.GetById(achievementId);
        if (achievement == null)
        {
            this.LogWarning($"Achievement '{achievementId}' not found");
            EmitProgressIncremented(achievementId, 0, false, $"Achievement '{achievementId}' not found");
            return;
        }

        if (string.IsNullOrEmpty(achievement.SteamId))
        {
            this.LogWarning($"Achievement '{achievementId}' has no Steam ID configured");
            EmitProgressIncremented(achievementId, 0, false, $"Achievement '{achievementId}' has no Steam ID configured");
            return;
        }

        // UNCOMMENT: Increment progress in Steamworks
        // int currentProgress;
        // SteamUserStats.GetStat(achievement.SteamId + "_progress", out currentProgress);
        // SteamUserStats.SetStat(achievement.SteamId + "_progress", currentProgress + amount);
        // SteamUserStats.StoreStats();

        this.Log($"Would increment progress for {achievement.SteamId} by {amount}");
        EmitProgressIncremented(achievementId, amount, true);
    }

    public override void ResetAchievement(string achievementId)
    {
        if (!IsAvailable)
        {
            this.LogWarning("Steam is not available");
            EmitAchievementReset(achievementId, false, "Steam is not available");
            return;
        }

        var achievement = _database.GetById(achievementId);
        if (achievement == null)
        {
            this.LogWarning($"Achievement '{achievementId}' not found");
            EmitAchievementReset(achievementId, false, $"Achievement '{achievementId}' not found");
            return;
        }

        if (string.IsNullOrEmpty(achievement.SteamId))
        {
            this.LogWarning($"Achievement '{achievementId}' has no Steam ID configured");
            EmitAchievementReset(achievementId, false, $"Achievement '{achievementId}' has no Steam ID configured");
            return;
        }

        // UNCOMMENT: SteamUserStats.ClearAchievement(achievement.SteamId);
        this.Log($"Would reset achievement: {achievement.SteamId}");
        EmitAchievementReset(achievementId, true);
    }

    public override void ResetAllAchievements()
    {
        if (!IsAvailable)
        {
            this.LogWarning("Steam is not available");
            EmitAllAchievementsReset(false, "Steam is not available");
            return;
        }

        // UNCOMMENT: SteamUserStats.ResetAllStats(true);
        this.Log("Would reset all achievements");
        EmitAllAchievementsReset(true);
    }

    #endregion

    #region Async Methods

    public override Task<AchievementUnlockResult> UnlockAchievementAsync(string achievementId)
    {
        if (!IsAvailable)
            return Task.FromResult(AchievementUnlockResult.FailureResult("Steam is not available"));

        var achievement = _database.GetById(achievementId);
        if (achievement == null)
            return Task.FromResult(AchievementUnlockResult.FailureResult($"Achievement '{achievementId}' not found"));

        if (string.IsNullOrEmpty(achievement.SteamId))
            return Task.FromResult(AchievementUnlockResult.FailureResult($"Achievement '{achievementId}' has no Steam ID configured"));

        try
        {
            // UNCOMMENT:
            // bool success = SteamUserStats.SetAchievement(achievement.SteamId);
            // if (success) success = SteamUserStats.StoreStats();
            // return Task.FromResult(success ? AchievementUnlockResult.SuccessResult() : AchievementUnlockResult.FailureResult("Failed to unlock"));

            this.Log($"Would unlock achievement: {achievement.SteamId}");
            return Task.FromResult(AchievementUnlockResult.SuccessResult());
        }
        catch (Exception ex)
        {
            return Task.FromResult(AchievementUnlockResult.FailureResult($"Steam exception: {ex.Message}"));
        }
    }

    public override Task<int> GetProgressAsync(string achievementId)
    {
        if (!IsAvailable)
            return Task.FromResult(0);

        // UNCOMMENT: Load progress from Steamworks
        // var achievement = _database.GetById(achievementId);
        // if (achievement == null || string.IsNullOrEmpty(achievement.SteamId))
        //     return Task.FromResult(0);
        // int progress;
        // SteamUserStats.GetStat(achievement.SteamId + "_progress", out progress);
        // return Task.FromResult(progress);

        return Task.FromResult(0);
    }

    public override Task<SyncResult> IncrementProgressAsync(string achievementId, int amount)
    {
        if (!IsAvailable)
            return Task.FromResult(SyncResult.FailureResult("Steam is not available"));

        var achievement = _database.GetById(achievementId);
        if (achievement == null)
            return Task.FromResult(SyncResult.FailureResult($"Achievement '{achievementId}' not found"));

        if (string.IsNullOrEmpty(achievement.SteamId))
            return Task.FromResult(SyncResult.FailureResult($"Achievement '{achievementId}' has no Steam ID configured"));

        // UNCOMMENT: Increment progress in Steamworks
        // int currentProgress;
        // SteamUserStats.GetStat(achievement.SteamId + "_progress", out currentProgress);
        // SteamUserStats.SetStat(achievement.SteamId + "_progress", currentProgress + amount);
        // SteamUserStats.StoreStats();

        this.Log($"Would increment progress for {achievement.SteamId} by {amount}");
        return Task.FromResult(SyncResult.SuccessResult());
    }

    public override Task<SyncResult> ResetAchievementAsync(string achievementId)
    {
        if (!IsAvailable)
            return Task.FromResult(SyncResult.FailureResult("Steam is not available"));

        var achievement = _database.GetById(achievementId);
        if (achievement == null)
            return Task.FromResult(SyncResult.FailureResult($"Achievement '{achievementId}' not found"));

        if (string.IsNullOrEmpty(achievement.SteamId))
            return Task.FromResult(SyncResult.FailureResult($"Achievement '{achievementId}' has no Steam ID configured"));

        // UNCOMMENT: SteamUserStats.ClearAchievement(achievement.SteamId);
        this.Log($"Would reset achievement: {achievement.SteamId}");
        return Task.FromResult(SyncResult.SuccessResult());
    }

    public override Task<SyncResult> ResetAllAchievementsAsync()
    {
        if (!IsAvailable)
            return Task.FromResult(SyncResult.FailureResult("Steam is not available"));

        // UNCOMMENT: SteamUserStats.ResetAllStats(true);
        this.Log("Would reset all achievements");
        return Task.FromResult(SyncResult.SuccessResult());
    }

    #endregion
}
#endif
