#if !GODOT_IOS
using System.Threading.Tasks;
using Godot.Achievements.Core;
using Godot.Achievements.Providers;

namespace Godot.Achievements.Providers.GameCenter;

/// <summary>
/// Stub implementation for non-iOS platforms
/// </summary>
public class GameCenterAchievementProvider : IAchievementProvider
{
    public static bool IsPlatformSupported => false;

    public string ProviderName => ProviderNames.GameCenter;

    public GameCenterAchievementProvider(AchievementDatabase database)
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
        => Task.FromResult(AchievementUnlockResult.FailureResult("Game Center is not supported on this platform"));

    public Task<int> GetProgressAsync(string achievementId)
        => Task.FromResult(0);

    public Task<SyncResult> IncrementProgressAsync(string achievementId, int amount)
        => Task.FromResult(SyncResult.FailureResult("Game Center is not supported on this platform"));

    public Task<SyncResult> ResetAchievementAsync(string achievementId)
        => Task.FromResult(SyncResult.FailureResult("Game Center is not supported on this platform"));

    public Task<SyncResult> ResetAllAchievementsAsync()
        => Task.FromResult(SyncResult.FailureResult("Game Center is not supported on this platform"));
}
#endif
