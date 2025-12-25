#if !GODOT_IOS
using System.Threading.Tasks;
using Godot.Achievements.Core;

namespace Godot.Achievements.iOS;

/// <summary>
/// Stub implementation for non-iOS platforms
/// </summary>
public class GameCenterAchievementProvider : IAchievementProvider
{
    public static bool IsPlatformSupported => false;

    public string ProviderName => ProviderNames.GameCenter;

    public GameCenterAchievementProvider(AchievementDatabase database)
    {
        // Database not used in stub - platform not supported
        _ = database;
    }

    public bool IsAvailable => false;

    public Task<AchievementUnlockResult> UnlockAchievement(string achievementId)
        => Task.FromResult(AchievementUnlockResult.FailureResult("Game Center is not supported on this platform"));

    public Task<int> GetProgress(string achievementId)
        => Task.FromResult(0);

    public Task<SyncResult> SetProgress(string achievementId, int currentProgress)
        => Task.FromResult(SyncResult.FailureResult("Game Center is not supported on this platform"));

    public Task<SyncResult> ResetAchievement(string achievementId)
        => Task.FromResult(SyncResult.FailureResult("Game Center is not supported on this platform"));

    public Task<SyncResult> ResetAllAchievements()
        => Task.FromResult(SyncResult.FailureResult("Game Center is not supported on this platform"));
}
#endif
