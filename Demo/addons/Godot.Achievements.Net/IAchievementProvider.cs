using System.Threading.Tasks;

namespace Godot.Achievements.Core;

/// <summary>
/// Extension methods for provider logging with consistent [Achievements] [ProviderName] format
/// </summary>
public static class ProviderLogExtensions
{
    public static void Log(this IAchievementProvider provider, string message)
    {
        GD.Print($"[Achievements] [{provider.ProviderName}] {message}");
    }

    public static void LogWarning(this IAchievementProvider provider, string message)
    {
        GD.PushWarning($"[Achievements] [{provider.ProviderName}] {message}");
    }

    public static void LogError(this IAchievementProvider provider, string message)
    {
        GD.PushError($"[Achievements] [{provider.ProviderName}] {message}");
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
/// Interface for platform-specific achievement providers (Steam, Game Center, Google Play, etc.)
/// </summary>
public interface IAchievementProvider
{
    /// <summary>
    /// Whether this provider is supported on the current platform (compile-time check)
    /// </summary>
    static abstract bool IsPlatformSupported { get; }

    /// <summary>
    /// Name of the provider (e.g., "Steam", "Game Center", "Local")
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Whether the provider is currently available (SDK initialized, user logged in, etc.)
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Unlock an achievement on this platform
    /// </summary>
    Task<AchievementUnlockResult> UnlockAchievement(string achievementId);

    /// <summary>
    /// Get current progress for a progressive achievement
    /// </summary>
    Task<int> GetProgress(string achievementId);

    /// <summary>
    /// Set current progress for a progressive achievement
    /// </summary>
    Task SetProgress(string achievementId, int currentProgress);

    /// <summary>
    /// Reset a specific achievement (for testing)
    /// </summary>
    Task<bool> ResetAchievement(string achievementId);

    /// <summary>
    /// Reset all achievements (for testing)
    /// </summary>
    Task<bool> ResetAllAchievements();
}
