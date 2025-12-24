#if !(GODOT_PC || GODOT_WINDOWS || GODOT_LINUX || GODOT_MACOS || GODOT_X11 || GODOT_OSX)
using System.Threading.Tasks;
using Godot.Achievements.Core;

namespace Godot.Achievements.Steam;

/// <summary>
/// Stub implementation for non-desktop platforms
/// </summary>
public class SteamAchievementProvider : IAchievementProvider
{
    public static bool IsPlatformSupported => false;

    private readonly AchievementDatabase _database;

    public string ProviderName => "Steam";

    public SteamAchievementProvider(AchievementDatabase database)
    {
        _database = database;
    }

    public bool IsAvailable => false;

    public Task<AchievementUnlockResult> UnlockAchievement(string achievementId)
        => Task.FromResult(AchievementUnlockResult.FailureResult("Steam is not supported on this platform"));

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
