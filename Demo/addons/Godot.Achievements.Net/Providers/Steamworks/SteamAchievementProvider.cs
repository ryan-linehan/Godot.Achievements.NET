#if GODOT_PC || GODOT_WINDOWS || GODOT_LINUX || GODOT_MACOS || GODOT_X11 || GODOT_OSX
using System;
using System.Threading.Tasks;
using Godot.Achievements.Core;
using Godot.Achievements.Providers;
using Godot.Steamworks.Net;

namespace Godot.Achievements.Providers.Steamworks;

/// <summary>
/// Steam achievement provider for PC/Desktop platforms.
/// Requires Godot.Steamworks.NET addon to be installed and configured as an autoload.
/// </summary>
public partial class SteamAchievementProvider : AchievementProviderBase
{
    public static bool IsPlatformSupported => true;

    private readonly AchievementDatabase _database;
    private SteamworksAchievements? _achievements;
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
            if (GodotSteamworks.Instance == null)
            {
                this.LogWarning("GodotSteamworks.Instance is null. Ensure GodotSteamworks autoload is configured.");
                return;
            }

            if (!GodotSteamworks.Instance.IsInitialized)
            {
                this.LogWarning("Steam is not initialized. Make sure Steam is running.");
                return;
            }

            _achievements = GodotSteamworks.Achievements;
            if (_achievements == null)
            {
                this.LogError("Failed to get SteamworksAchievements");
                return;
            }

            _isInitialized = true;
            this.Log("Initialized with Godot.Steamworks.NET");
        }
        catch (Exception ex)
        {
            this.LogError($"Failed to initialize: {ex.Message}");
        }
    }

    public override bool IsAvailable => _isInitialized &&
        GodotSteamworks.Instance?.IsInitialized == true;

    #region Helper Methods

    private (string? steamId, string? error) ValidateAndGetSteamId(string achievementId)
    {
        if (!_isInitialized || _achievements == null)
            return (null, "Steam not initialized. Ensure Godot.Steamworks.NET is installed and Steam is running.");

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
            _achievements!.UnlockAchievement(steamId!);
            this.Log($"Unlocked achievement: {steamId}");
            EmitAchievementUnlocked(achievementId, true);
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
            var currentProgress = _achievements!.GetAchievementProgress(steamId!);
            var newProgress = currentProgress + amount;
            _achievements.SetAchievementProgress(steamId!, newProgress);

            this.Log($"Incremented progress for {steamId}: {currentProgress} -> {newProgress}");
            EmitProgressIncremented(achievementId, newProgress, true);

            // Check if achievement should be unlocked
            var achievement = _database.GetById(achievementId);
            if (achievement != null && newProgress >= achievement.MaxProgress)
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
            _achievements!.ClearAchievement(steamId!);
            this.Log($"Reset achievement: {steamId}");
            EmitAchievementReset(achievementId, true);
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
            this.LogWarning("Steam is not available");
            EmitAllAchievementsReset(false, "Steam is not available");
            return;
        }

        try
        {
            _achievements!.ResetAllAchievements();
            this.Log("Reset all achievements");
            EmitAllAchievementsReset(true);
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
            if (_achievements!.IsAchievementUnlocked(steamId!))
            {
                this.Log($"Achievement already unlocked: {steamId}");
                return Task.FromResult(AchievementUnlockResult.SuccessResult(wasAlreadyUnlocked: true));
            }

            _achievements.UnlockAchievement(steamId!);
            this.Log($"Unlocked achievement: {steamId}");
            return Task.FromResult(AchievementUnlockResult.SuccessResult());
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
            var progress = _achievements!.GetAchievementProgress(steamId!);
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
            var currentProgress = _achievements!.GetAchievementProgress(steamId!);
            var newProgress = currentProgress + amount;
            _achievements.SetAchievementProgress(steamId!, newProgress);

            // Auto-unlock if progress reached
            var achievement = _database.GetById(achievementId);
            if (achievement != null && newProgress >= achievement.MaxProgress)
            {
                _achievements.UnlockAchievement(steamId!);
            }

            this.Log($"Incremented progress for {steamId}: {newProgress}");
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
            _achievements!.ClearAchievement(steamId!);
            this.Log($"Reset achievement: {steamId}");
            return Task.FromResult(SyncResult.SuccessResult());
        }
        catch (Exception ex)
        {
            return Task.FromResult(SyncResult.FailureResult(ex.Message));
        }
    }

    public override Task<SyncResult> ResetAllAchievementsAsync()
    {
        if (!IsAvailable)
            return Task.FromResult(SyncResult.FailureResult("Steam is not available"));

        try
        {
            _achievements!.ResetAllAchievements();
            this.Log("Reset all achievements");
            return Task.FromResult(SyncResult.SuccessResult());
        }
        catch (Exception ex)
        {
            return Task.FromResult(SyncResult.FailureResult(ex.Message));
        }
    }

    #endregion
}
#endif
