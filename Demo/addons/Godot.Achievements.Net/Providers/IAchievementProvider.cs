using System.Threading.Tasks;
using Godot.Achievements.Core;

namespace Godot.Achievements.Providers;

/// <summary>
/// Extension methods for provider logging with consistent [Achievements] [ProviderName] format
/// </summary>
public static class ProviderLogExtensions
{
    public static void Log(this IAchievementProvider provider, string message)
    {
        AchievementLogger.Log(provider.ProviderName, message);
    }

    public static void LogWarning(this IAchievementProvider provider, string message)
    {
        AchievementLogger.Warning(provider.ProviderName, message);
    }

    public static void LogError(this IAchievementProvider provider, string message)
    {
        AchievementLogger.Error(provider.ProviderName, message);
    }
}

/// <summary>
/// Result of an achievement unlock operation
/// </summary>
public readonly struct AchievementUnlockResult
{
    public bool Success { get; init; }
    public string? Error { get; init; }
    public bool WasAlreadyUnlocked { get; init; }

    public static AchievementUnlockResult SuccessResult(bool wasAlreadyUnlocked = false) =>
        new() { Success = true, WasAlreadyUnlocked = wasAlreadyUnlocked };

    public static AchievementUnlockResult FailureResult(string error) =>
        new() { Success = false, Error = error };
}

/// <summary>
/// Result of a sync operation (progress update, reset, etc.)
/// </summary>
public readonly struct SyncResult
{
    public bool Success { get; init; }
    public string? Message { get; init; }

    public static SyncResult SuccessResult(string? message = null) =>
        new() { Success = true, Message = message };

    public static SyncResult FailureResult(string message) =>
        new() { Success = false, Message = message };

    /// <summary>
    /// Implicit conversion to bool for easy success checks
    /// </summary>
    public static implicit operator bool(SyncResult result) => result.Success;
}

/// <summary>
/// Interface for platform-specific achievement providers (Steam, Game Center, Google Play, etc.)
/// Providers implement both sync and async versions of each operation.
/// Sync methods are preferred for gameplay code (no frame blocking).
/// </summary>
public interface IAchievementProvider
{
    /// <summary>
    /// Whether this provider is supported on the current platform (compile-time check)
    /// Use preprocessor directives inside the implementation to enforce this.
    /// </summary>
    static virtual bool IsPlatformSupported => false;

    /// <summary>
    /// Name of the provider. Use values from <see cref="ProviderNames"/> for built-in providers.
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Whether the provider is currently available (SDK initialized, user logged in, etc.)
    /// </summary>
    bool IsAvailable { get; }
    #region Sync Methods
    /// <summary>
    /// Unlock an achievement on this platform
    /// </summary>
    void UnlockAchievement(string achievementId);

    /// <summary>
    /// Increment progress for a progressive achievement
    /// </summary>
    void IncrementProgress(string achievementId, int amount);

    /// <summary>
    /// Reset a specific achievement - for testing
    /// </summary>
    void ResetAchievement(string achievementId);

    /// <summary>
    /// Reset all achievements - for testing
    /// </summary>
    void ResetAllAchievements();
    #endregion

    #region Async Methods
    /// <summary>
    /// Unlock an achievement on this platform (async)
    /// </summary>
    Task<AchievementUnlockResult> UnlockAchievementAsync(string achievementId);

    /// <summary>
    /// Get current progress for a progressive achievement (async)
    /// </summary>
    Task<int> GetProgressAsync(string achievementId);

    /// <summary>
    /// Increment progress for a progressive achievement (async)
    /// </summary>
    Task<SyncResult> IncrementProgressAsync(string achievementId, int amount);

    /// <summary>
    /// Reset a specific achievement - for testing (async)
    /// </summary>
    Task<SyncResult> ResetAchievementAsync(string achievementId);

    /// <summary>
    /// Reset all achievements - for testing (async)
    /// </summary>
    Task<SyncResult> ResetAllAchievementsAsync();
    #endregion
}
