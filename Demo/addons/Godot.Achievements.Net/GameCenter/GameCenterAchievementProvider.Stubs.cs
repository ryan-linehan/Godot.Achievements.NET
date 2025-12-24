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

    private readonly AchievementDatabase _database;

    public string ProviderName => "Game Center";

    public GameCenterAchievementProvider(AchievementDatabase database)
    {
        _database = database;
    }

    public bool IsAvailable => false;

    public Task<AchievementUnlockResult> UnlockAchievement(string achievementId)
        => Task.FromResult(AchievementUnlockResult.FailureResult("Game Center is not supported on this platform"));

    public Task<int> GetProgress(string achievementId)
        => Task.FromResult(0);

    public Task SetProgress(string achievementId, int currentProgress)
        => Task.CompletedTask;

    public Task<bool> ResetAchievement(string achievementId)
        => Task.FromResult(false);

    public Task<bool> ResetAllAchievements()
        => Task.FromResult(false);
}
#endif
