#if GODOT_PC
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

    /// <summary>
    /// Gets the Steam stat key for an achievement. Falls back to SteamId if SteamStatId is not set.
    /// </summary>
    private string? GetStatKey(Achievement achievement)
    {
        // Use SteamStatId if configured, otherwise fall back to SteamId
        var statId = achievement.SteamStatId;
        if (!string.IsNullOrEmpty(statId))
            return statId;

        // Fall back to SteamId (for backwards compatibility or when stat key matches achievement key)
        return achievement.SteamId;
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
            var achievement = _database.GetById(achievementId)!;
            var statKey = GetStatKey(achievement);
            if (string.IsNullOrEmpty(statKey))
            {
                var noStatError = $"Achievement '{achievementId}' has no Steam Stat ID configured";
                this.LogWarning(noStatError);
                EmitProgressIncremented(achievementId, 0, false, noStatError);
                return;
            }

            var currentProgress = _achievements!.GetStatProgress(statKey);
            var newProgress = currentProgress + amount;
            _achievements.SetStatProgress(statKey, newProgress);

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
            var achievement = _database.GetById(achievementId)!;
            var statKey = GetStatKey(achievement);
            if (string.IsNullOrEmpty(statKey))
            {
                this.LogWarning($"Achievement '{achievementId}' has no Steam Stat ID configured");
                return Task.FromResult(0);
            }

            var progress = _achievements!.GetStatProgress(statKey);
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

            var currentProgress = _achievements!.GetStatProgress(statKey);
            var newProgress = currentProgress + amount;
            _achievements.SetStatProgress(statKey, newProgress);

            // Auto-unlock if progress reached
            if (newProgress >= achievement.MaxProgress)
            {
                _achievements.UnlockAchievement(steamId!);
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
#else
// Stub implementation for non-desktop platforms (Android, iOS, Web, etc.)
using System.Threading.Tasks;
using Godot.Achievements.Core;
using Godot.Achievements.Providers;

namespace Godot.Achievements.Providers.Steamworks;

/// <summary>
/// Stub implementation for non-desktop platforms.
/// Steam is not supported on mobile platforms.
/// </summary>
public class SteamAchievementProvider : IAchievementProvider
{
    public static bool IsPlatformSupported => false;

    public string ProviderName => ProviderNames.Steam;

    public SteamAchievementProvider(AchievementDatabase database)
    {
    }

    public bool IsAvailable => false;

    // Sync methods (fire-and-forget, no-op on unsupported platforms)
    public void UnlockAchievement(string achievementId) { }
    public void IncrementProgress(string achievementId, int amount) { }
    public void ResetAchievement(string achievementId) { }
    public void ResetAllAchievements() { }

    // Async methods (return failure results)
    public Task<AchievementUnlockResult> UnlockAchievementAsync(string achievementId)
        => Task.FromResult(AchievementUnlockResult.FailureResult("Steam is not supported on this platform"));

    public Task<int> GetProgressAsync(string achievementId)
        => Task.FromResult(0);

    public Task<SyncResult> IncrementProgressAsync(string achievementId, int amount)
        => Task.FromResult(SyncResult.FailureResult("Steam is not supported on this platform"));

    public Task<SyncResult> ResetAchievementAsync(string achievementId)
        => Task.FromResult(SyncResult.FailureResult("Steam is not supported on this platform"));

    public Task<SyncResult> ResetAllAchievementsAsync()
        => Task.FromResult(SyncResult.FailureResult("Steam is not supported on this platform"));
}
#endif
