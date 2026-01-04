namespace Godot.Achievements.Providers;

/// <summary>
/// Result of an achievement unlock operation
/// </summary>
public readonly struct AchievementUnlockResult
{
    public bool Success { get; init; }
    public string? Error { get; init; }

    public static AchievementUnlockResult SuccessResult() =>
        new() { Success = true };

    public static AchievementUnlockResult FailureResult(string error) =>
        new() { Success = false, Error = error };
}
