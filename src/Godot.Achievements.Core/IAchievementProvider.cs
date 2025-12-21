namespace Godot.Achievements.Core;

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
    /// Get achievement details from this platform
    /// </summary>
    Task<Achievement?> GetAchievement(string achievementId);

    /// <summary>
    /// Get all achievements from this platform
    /// </summary>
    Task<Achievement[]> GetAllAchievements();

    /// <summary>
    /// Get progress for a progressive achievement (0.0 to 1.0)
    /// </summary>
    Task<float> GetProgress(string achievementId);

    /// <summary>
    /// Set progress for a progressive achievement (0.0 to 1.0)
    /// </summary>
    Task SetProgress(string achievementId, float progress);

    /// <summary>
    /// Reset a specific achievement (for testing)
    /// </summary>
    Task<bool> ResetAchievement(string achievementId);

    /// <summary>
    /// Reset all achievements (for testing)
    /// </summary>
    Task<bool> ResetAllAchievements();
}
