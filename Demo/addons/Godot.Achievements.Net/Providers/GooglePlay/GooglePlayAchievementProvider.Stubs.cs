#if !GODOT_ANDROID
using System.Threading.Tasks;
using Godot.Achievements.Core;
using Godot.Achievements.Providers;

namespace Godot.Achievements.Providers.GooglePlay;

/// <summary>
/// Stub implementation for non-Android platforms
/// </summary>
public class GooglePlayAchievementProvider : IAchievementProvider
{
    public static bool IsPlatformSupported => false;

    private readonly AchievementDatabase _database;

    public string ProviderName => ProviderNames.GooglePlay;

    public GooglePlayAchievementProvider(AchievementDatabase database)
    {
        _database = database;
    }

    public bool IsAvailable => false;

    public Task<AchievementUnlockResult> UnlockAchievement(string achievementId)
        => Task.FromResult(AchievementUnlockResult.FailureResult("Google Play Games is not supported on this platform"));

    public Task<int> GetProgress(string achievementId)
        => Task.FromResult(0);

    public Task<SyncResult> SetProgress(string achievementId, int currentProgress)
        => Task.FromResult(SyncResult.FailureResult("Google Play Games is not supported on this platform"));

    public Task<SyncResult> ResetAchievement(string achievementId)
        => Task.FromResult(SyncResult.FailureResult("Google Play Games is not supported on this platform"));

    public Task<SyncResult> ResetAllAchievements()
        => Task.FromResult(SyncResult.FailureResult("Google Play Games is not supported on this platform"));
}
#endif
