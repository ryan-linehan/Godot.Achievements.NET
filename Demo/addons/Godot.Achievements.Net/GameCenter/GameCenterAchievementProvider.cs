#if GODOT_IOS
using System;
using System.Threading.Tasks;
using Godot.Achievements.Core;

namespace Godot.Achievements.iOS;

/// <summary>
/// iOS Game Center achievement provider
/// </summary>
public class GameCenterAchievementProvider : IAchievementProvider
{
    public static bool IsPlatformSupported => true;

    private readonly AchievementDatabase _database;
    private bool _isAuthenticated;

    public string ProviderName => ProviderNames.GameCenter;

    public GameCenterAchievementProvider(AchievementDatabase database)
    {
        _database = database;
        Initialize();
    }

    private void Initialize()
    {
        try
        {
            // UNCOMMENT when iOS Game Center bindings are set up:
            // GKLocalPlayer.LocalPlayer.Authenticate((viewController, error) =>
            // {
            //     if (error != null)
            //     {
            //         GD.PushError($"[GameCenter] Authentication failed: {error}");
            //         _isAuthenticated = false;
            //     }
            //     else
            //     {
            //         _isAuthenticated = GKLocalPlayer.LocalPlayer.IsAuthenticated;
            //     }
            // });

            GD.Print("[Achievements] [Game Center] Initialized (iOS bindings required)");
            _isAuthenticated = false;
        }
        catch (Exception ex)
        {
            GD.PushError($"[Achievements] [Game Center] Failed to initialize: {ex.Message}");
            _isAuthenticated = false;
        }
    }

    public bool IsAvailable => _isAuthenticated && IsGameCenterAvailable();

    private bool IsGameCenterAvailable()
    {
        // UNCOMMENT: return GKLocalPlayer.IsAvailable;
        return false;
    }

    public async Task<AchievementUnlockResult> UnlockAchievement(string achievementId)
    {
        if (!IsAvailable)
            return AchievementUnlockResult.FailureResult("Game Center is not available");

        var achievement = _database.GetById(achievementId);
        if (achievement == null)
            return AchievementUnlockResult.FailureResult($"Achievement '{achievementId}' not found");

        var gameCenterId = achievement.GameCenterId;
        if (string.IsNullOrEmpty(gameCenterId))
            return AchievementUnlockResult.FailureResult($"Achievement '{achievementId}' has no Game Center ID configured");

        try
        {
            // UNCOMMENT:
            // var gcAchievement = new GKAchievement(gameCenterId) { PercentComplete = 100.0 };
            // var tcs = new TaskCompletionSource<bool>();
            // GKAchievement.ReportAchievements(new[] { gcAchievement }, error => tcs.SetResult(error == null));
            // if (!await tcs.Task) return AchievementUnlockResult.FailureResult("Failed to unlock");

            this.Log($"Would unlock achievement: {gameCenterId}");
            await Task.Delay(10);
            return AchievementUnlockResult.SuccessResult();
        }
        catch (Exception ex)
        {
            return AchievementUnlockResult.FailureResult($"Game Center exception: {ex.Message}");
        }
    }

    public async Task<int> GetProgress(string achievementId)
    {
        if (!IsAvailable)
            return 0;

        // UNCOMMENT: Load progress from Game Center
        await Task.CompletedTask;
        return 0;
    }

    public async Task<SyncResult> SetProgress(string achievementId, int currentProgress)
    {
        if (!IsAvailable)
            return SyncResult.FailureResult("Game Center is not available");

        var achievement = _database.GetById(achievementId);
        if (achievement == null)
            return SyncResult.FailureResult($"Achievement '{achievementId}' not found");

        if (string.IsNullOrEmpty(achievement.GameCenterId))
            return SyncResult.FailureResult($"Achievement '{achievementId}' has no Game Center ID configured");

        // UNCOMMENT: Report progress to Game Center
        float percentage = achievement.MaxProgress > 0 ? (float)currentProgress / achievement.MaxProgress * 100 : 0;
        this.Log($"Would set progress for {achievement.GameCenterId}: {currentProgress}/{achievement.MaxProgress} ({percentage:F1}%)");
        await Task.CompletedTask;
        return SyncResult.SuccessResult();
    }

    public async Task<SyncResult> ResetAchievement(string achievementId)
    {
        if (!IsAvailable)
            return SyncResult.FailureResult("Game Center is not available");

        var achievement = _database.GetById(achievementId);
        if (achievement == null)
            return SyncResult.FailureResult($"Achievement '{achievementId}' not found");

        if (string.IsNullOrEmpty(achievement.GameCenterId))
            return SyncResult.FailureResult($"Achievement '{achievementId}' has no Game Center ID configured");

        this.Log($"Would reset achievement: {achievement.GameCenterId}");
        await Task.CompletedTask;
        return SyncResult.SuccessResult();
    }

    public async Task<SyncResult> ResetAllAchievements()
    {
        if (!IsAvailable)
            return SyncResult.FailureResult("Game Center is not available");

        // UNCOMMENT: GKAchievement.ResetAchievements(error => { });
        this.Log("Would reset all achievements");
        await Task.CompletedTask;
        return SyncResult.SuccessResult();
    }
}
#endif
