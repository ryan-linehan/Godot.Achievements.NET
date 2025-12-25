#if GODOT_IOS
using System;
using System.Threading.Tasks;
using Godot;
using Godot.Achievements.Core;
using Godot.Collections;

namespace Godot.Achievements.iOS;

/// <summary>
/// iOS Game Center achievement provider using GodotApplePlugins
/// See: https://github.com/migueldeicaza/GodotApplePlugins
/// </summary>
public class GameCenterAchievementProvider : IAchievementProvider
{
    public static bool IsPlatformSupported => true;

    private readonly AchievementDatabase _database;
    private bool _isAuthenticated;
    private GodotObject? _localPlayer;
    private GodotObject? _gkAchievementClass;

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
            // Try to get the GKLocalPlayer singleton from GodotApplePlugins
            if (Engine.HasSingleton("GKLocalPlayer"))
            {
                _localPlayer = Engine.GetSingleton("GKLocalPlayer");

                // Check if the local player property is accessible
                var local = _localPlayer.Get("local");
                if (local.Obj is GodotObject localPlayerObj)
                {
                    _isAuthenticated = localPlayerObj.Get("isAuthenticated").AsBool();
                    this.Log($"Initialized, authenticated: {_isAuthenticated}");
                }
                else
                {
                    this.LogWarning("Could not access local player object");
                    _isAuthenticated = false;
                }
            }
            else
            {
                this.LogWarning("GKLocalPlayer singleton not found - ensure GodotApplePlugins is installed");
                _isAuthenticated = false;
            }

            // Get GKAchievement class for static method calls
            if (Engine.HasSingleton("GKAchievement"))
            {
                _gkAchievementClass = Engine.GetSingleton("GKAchievement");
            }
        }
        catch (Exception ex)
        {
            this.LogError($"Failed to initialize: {ex.Message}");
            _isAuthenticated = false;
        }
    }

    public bool IsAvailable => _isAuthenticated && IsGameCenterAvailable();

    private bool IsGameCenterAvailable()
    {
        if (_localPlayer == null) return false;

        try
        {
            var local = _localPlayer.Get("local");
            if (local.Obj is GodotObject localPlayerObj)
            {
                return localPlayerObj.Get("isAuthenticated").AsBool();
            }
        }
        catch
        {
            return false;
        }

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
            var tcs = new TaskCompletionSource<AchievementUnlockResult>();

            // Create a GKAchievement instance with 100% completion
            var gcAchievement = CreateGKAchievement(gameCenterId, 100.0);
            if (gcAchievement == null)
            {
                return AchievementUnlockResult.FailureResult("Failed to create GKAchievement instance");
            }

            // Create array of achievements to report
            var achievementsArray = new Array<GodotObject> { gcAchievement };

            // Create callback for the async operation
            var callback = Callable.From<Variant>((error) =>
            {
                if (error.VariantType == Variant.Type.Nil)
                {
                    this.Log($"Successfully unlocked achievement: {gameCenterId}");
                    tcs.TrySetResult(AchievementUnlockResult.SuccessResult());
                }
                else
                {
                    var errorMessage = error.AsString();
                    this.LogError($"Failed to unlock achievement {gameCenterId}: {errorMessage}");
                    tcs.TrySetResult(AchievementUnlockResult.FailureResult(errorMessage));
                }
            });

            // Call report_achivement (note: typo is intentional - matches GodotApplePlugins API)
            _gkAchievementClass?.Call("report_achivement", achievementsArray, callback);

            // Wait for the callback with timeout
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(30));
            var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

            if (completedTask == timeoutTask)
            {
                return AchievementUnlockResult.FailureResult("Game Center request timed out");
            }

            return await tcs.Task;
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

        var achievement = _database.GetById(achievementId);
        if (achievement == null)
            return 0;

        var gameCenterId = achievement.GameCenterId;
        if (string.IsNullOrEmpty(gameCenterId))
            return 0;

        try
        {
            var tcs = new TaskCompletionSource<int>();

            var callback = Callable.From<Array, Variant>((achievements, error) =>
            {
                if (error.VariantType != Variant.Type.Nil)
                {
                    tcs.TrySetResult(0);
                    return;
                }

                foreach (var item in achievements)
                {
                    if (item.Obj is GodotObject gcAchievement)
                    {
                        var identifier = gcAchievement.Get("identifier").AsString();
                        if (identifier == gameCenterId)
                        {
                            var percentComplete = gcAchievement.Get("percentComplete").AsDouble();
                            // Convert percentage back to progress value
                            int progress = achievement.MaxProgress > 0
                                ? (int)Math.Round(percentComplete / 100.0 * achievement.MaxProgress)
                                : 0;
                            tcs.TrySetResult(progress);
                            return;
                        }
                    }
                }

                tcs.TrySetResult(0);
            });

            _gkAchievementClass?.Call("load_achievements", callback);

            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(30));
            var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

            if (completedTask == timeoutTask)
            {
                return 0;
            }

            return await tcs.Task;
        }
        catch (Exception ex)
        {
            this.LogError($"Failed to get progress: {ex.Message}");
            return 0;
        }
    }

    public async Task<SyncResult> SetProgress(string achievementId, int currentProgress)
    {
        if (!IsAvailable)
            return SyncResult.FailureResult("Game Center is not available");

        var achievement = _database.GetById(achievementId);
        if (achievement == null)
            return SyncResult.FailureResult($"Achievement '{achievementId}' not found");

        var gameCenterId = achievement.GameCenterId;
        if (string.IsNullOrEmpty(gameCenterId))
            return SyncResult.FailureResult($"Achievement '{achievementId}' has no Game Center ID configured");

        try
        {
            // Calculate percentage (Game Center uses 0-100)
            double percentage = achievement.MaxProgress > 0
                ? (double)currentProgress / achievement.MaxProgress * 100.0
                : 0;

            // Clamp to valid range
            percentage = Math.Clamp(percentage, 0, 100);

            var tcs = new TaskCompletionSource<SyncResult>();

            var gcAchievement = CreateGKAchievement(gameCenterId, percentage);
            if (gcAchievement == null)
            {
                return SyncResult.FailureResult("Failed to create GKAchievement instance");
            }

            var achievementsArray = new Array<GodotObject> { gcAchievement };

            var callback = Callable.From<Variant>((error) =>
            {
                if (error.VariantType == Variant.Type.Nil)
                {
                    this.Log($"Set progress for {gameCenterId}: {currentProgress}/{achievement.MaxProgress} ({percentage:F1}%)");
                    tcs.TrySetResult(SyncResult.SuccessResult());
                }
                else
                {
                    var errorMessage = error.AsString();
                    this.LogError($"Failed to set progress for {gameCenterId}: {errorMessage}");
                    tcs.TrySetResult(SyncResult.FailureResult(errorMessage));
                }
            });

            _gkAchievementClass?.Call("report_achivement", achievementsArray, callback);

            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(30));
            var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

            if (completedTask == timeoutTask)
            {
                return SyncResult.FailureResult("Game Center request timed out");
            }

            return await tcs.Task;
        }
        catch (Exception ex)
        {
            return SyncResult.FailureResult($"Game Center exception: {ex.Message}");
        }
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

        // Game Center doesn't support resetting individual achievements
        // We can only set progress to 0, but this won't actually reset a completed achievement
        this.LogWarning($"Game Center does not support resetting individual achievements. Use ResetAllAchievements for testing.");

        await Task.CompletedTask;
        return SyncResult.FailureResult("Game Center does not support resetting individual achievements");
    }

    public async Task<SyncResult> ResetAllAchievements()
    {
        if (!IsAvailable)
            return SyncResult.FailureResult("Game Center is not available");

        try
        {
            var tcs = new TaskCompletionSource<SyncResult>();

            var callback = Callable.From<Variant>((error) =>
            {
                if (error.VariantType == Variant.Type.Nil)
                {
                    this.Log("Successfully reset all achievements");
                    tcs.TrySetResult(SyncResult.SuccessResult());
                }
                else
                {
                    var errorMessage = error.AsString();
                    this.LogError($"Failed to reset achievements: {errorMessage}");
                    tcs.TrySetResult(SyncResult.FailureResult(errorMessage));
                }
            });

            // Call reset_achivements (note: typo is intentional - matches GodotApplePlugins API)
            _gkAchievementClass?.Call("reset_achivements", callback);

            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(30));
            var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

            if (completedTask == timeoutTask)
            {
                return SyncResult.FailureResult("Game Center request timed out");
            }

            return await tcs.Task;
        }
        catch (Exception ex)
        {
            return SyncResult.FailureResult($"Game Center exception: {ex.Message}");
        }
    }

    /// <summary>
    /// Creates a GKAchievement instance with the specified identifier and progress percentage
    /// </summary>
    private GodotObject? CreateGKAchievement(string identifier, double percentComplete)
    {
        try
        {
            // Try to instantiate GKAchievement via ClassDB if available
            if (ClassDB.ClassExists("GKAchievement"))
            {
                var achievement = ClassDB.Instantiate("GKAchievement");
                if (achievement.Obj is GodotObject gcAchievement)
                {
                    gcAchievement.Set("identifier", identifier);
                    gcAchievement.Set("percentComplete", percentComplete);
                    gcAchievement.Set("showsCompletionBanner", true);
                    return gcAchievement;
                }
            }

            // Alternative: Try to create via the singleton if it has a factory method
            if (_gkAchievementClass != null)
            {
                var result = _gkAchievementClass.Call("new", identifier);
                if (result.Obj is GodotObject gcAchievement)
                {
                    gcAchievement.Set("percentComplete", percentComplete);
                    gcAchievement.Set("showsCompletionBanner", true);
                    return gcAchievement;
                }
            }

            this.LogError("Failed to create GKAchievement instance - class not available");
            return null;
        }
        catch (Exception ex)
        {
            this.LogError($"Error creating GKAchievement: {ex.Message}");
            return null;
        }
    }
}
#endif
