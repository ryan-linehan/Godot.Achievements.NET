#if GODOT_IOS
using System;
using System.Threading.Tasks;
using Godot;
using Godot.Achievements.Core;
using Godot.Achievements.Providers;
using Godot.Collections;

namespace Godot.Achievements.Providers.GameCenter;

/// <summary>
/// iOS Game Center achievement provider using GodotApplePlugins.
/// See: https://github.com/migueldeicaza/GodotApplePlugins
///
/// Note: Game Center operations are inherently async. Sync methods fire-and-forget.
/// Async methods await the corresponding callback from the plugin.
/// </summary>
public partial class GameCenterAchievementProvider : AchievementProviderBase
{
    private const double DefaultTimeoutSeconds = 30.0;

    public static bool IsPlatformSupported => true;

    private readonly AchievementDatabase _database;
    private GodotObject? _localPlayer;
    private bool _isInitialized;
    private bool _isAuthenticated;

    public override string ProviderName => ProviderNames.GameCenter;

    public GameCenterAchievementProvider(AchievementDatabase database)
    {
        _database = database;
        Initialize();
    }

    private void Initialize()
    {
        try
        {
            // Check if GodotApplePlugins is available
            if (!ClassDB.ClassExists("GKLocalPlayer"))
            {
                this.LogWarning("GKLocalPlayer class not found - ensure GodotApplePlugins is installed");
                _isInitialized = false;
                return;
            }

            if (!ClassDB.ClassExists("GKAchievement"))
            {
                this.LogWarning("GKAchievement class not found - ensure GodotApplePlugins is installed");
                _isInitialized = false;
                return;
            }

            // Get the local player to check authentication status
            var localPlayerInstance = ClassDB.Instantiate("GKLocalPlayer");
            if (localPlayerInstance.Obj is GodotObject localPlayerObj)
            {
                _localPlayer = localPlayerObj;
                _isAuthenticated = _localPlayer.Get("isAuthenticated").AsBool();
                _isInitialized = true;
                this.Log($"Initialized, authenticated: {_isAuthenticated}");
            }
            else
            {
                this.LogWarning("Could not instantiate GKLocalPlayer");
                _isInitialized = false;
            }
        }
        catch (Exception ex)
        {
            this.LogError($"Failed to initialize: {ex.Message}");
            _isInitialized = false;
        }
    }

    public override bool IsAvailable => _isInitialized && _isAuthenticated;

    #region Sync Methods (Fire-and-Forget)

    public override void UnlockAchievement(string achievementId)
    {
        var (gameCenterId, error) = ValidateAndGetGameCenterId(achievementId);
        if (error != null)
        {
            EmitAchievementUnlocked(achievementId, false, error);
            return;
        }

        var gcAchievement = CreateGKAchievement(gameCenterId!, 100.0);
        if (gcAchievement == null)
        {
            EmitAchievementUnlocked(achievementId, false, "Failed to create GKAchievement instance");
            return;
        }

        var achievementsArray = new Array { gcAchievement };
        var callback = Callable.From<Variant>((err) =>
        {
            bool success = err.VariantType == Variant.Type.Nil;
            if (success)
            {
                this.Log($"Unlocked achievement: {gameCenterId}");
            }
            else
            {
                this.LogError($"Failed to unlock achievement {gameCenterId}: {err.AsString()}");
            }
            EmitAchievementUnlocked(achievementId, success, success ? null : err.AsString());
        });

        CallGKAchievementStatic("report_achivement", achievementsArray, callback);
        this.Log($"Unlock fired for: {gameCenterId}");
    }

    public override void IncrementProgress(string achievementId, int amount)
    {
        var (gameCenterId, error) = ValidateAndGetGameCenterId(achievementId);
        if (error != null)
        {
            EmitProgressIncremented(achievementId, 0, false, error);
            return;
        }

        var achievement = _database.GetById(achievementId)!;

        // Game Center uses percentages (0-100)
        // Use achievement.CurrentProgress which already has the new total from local provider
        int currentProgress = achievement.CurrentProgress;
        double percentage = achievement.MaxProgress > 0
            ? Math.Min((double)currentProgress / achievement.MaxProgress * 100.0, 100.0)
            : 0;

        var gcAchievement = CreateGKAchievement(gameCenterId!, percentage);
        if (gcAchievement == null)
        {
            EmitProgressIncremented(achievementId, 0, false, "Failed to create GKAchievement instance");
            return;
        }

        var achievementsArray = new Array { gcAchievement };
        var callback = Callable.From<Variant>((err) =>
        {
            bool success = err.VariantType == Variant.Type.Nil;
            if (success)
            {
                this.Log($"Set progress for {gameCenterId}: {currentProgress}/{achievement.MaxProgress} ({percentage:F1}%)");
            }
            else
            {
                this.LogError($"Failed to set progress for {gameCenterId}: {err.AsString()}");
            }
            EmitProgressIncremented(achievementId, currentProgress, success, success ? null : err.AsString());
        });

        CallGKAchievementStatic("report_achivement", achievementsArray, callback);
        this.Log($"Increment progress fired for: {gameCenterId} (new total: {currentProgress})");
    }

    public override void ResetAchievement(string achievementId)
    {
        this.LogWarning("ResetAchievement is not supported on Game Center. Use ResetAllAchievements instead.");
        EmitAchievementReset(achievementId, false, "Game Center does not support resetting individual achievements");
    }

    public override void ResetAllAchievements()
    {
        if (!IsAvailable)
        {
            EmitAllAchievementsReset(false, "Game Center is not available");
            return;
        }

        var callback = Callable.From<Variant>((err) =>
        {
            bool success = err.VariantType == Variant.Type.Nil;
            if (success)
            {
                this.Log("Reset all achievements");
            }
            else
            {
                this.LogError($"Failed to reset achievements: {err.AsString()}");
            }
            EmitAllAchievementsReset(success, success ? null : err.AsString());
        });

        // Note: typo is intentional - matches GodotApplePlugins API
        CallGKAchievementStatic("reset_achivements", callback);
        this.Log("Reset all achievements fired");
    }

    #endregion

    #region Async Methods

    public override async Task<AchievementUnlockResult> UnlockAchievementAsync(string achievementId)
    {
        var (gameCenterId, error) = ValidateAndGetGameCenterId(achievementId);
        if (error != null)
            return AchievementUnlockResult.FailureResult(error);

        try
        {
            var gcAchievement = CreateGKAchievement(gameCenterId!, 100.0);
            if (gcAchievement == null)
                return AchievementUnlockResult.FailureResult("Failed to create GKAchievement instance");

            var tcs = new TaskCompletionSource<AchievementUnlockResult>();
            var achievementsArray = new Array { gcAchievement };

            var callback = Callable.From<Variant>((err) =>
            {
                if (err.VariantType == Variant.Type.Nil)
                {
                    this.Log($"Unlocked achievement: {gameCenterId}");
                    tcs.TrySetResult(AchievementUnlockResult.SuccessResult());
                }
                else
                {
                    var errorMessage = err.AsString();
                    this.LogError($"Failed to unlock achievement {gameCenterId}: {errorMessage}");
                    tcs.TrySetResult(AchievementUnlockResult.FailureResult(errorMessage));
                }
            });

            CallGKAchievementStatic("report_achivement", achievementsArray, callback);

            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(DefaultTimeoutSeconds));
            var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

            if (completedTask == timeoutTask)
                return AchievementUnlockResult.FailureResult("Game Center request timed out");

            return await tcs.Task;
        }
        catch (Exception ex)
        {
            return AchievementUnlockResult.FailureResult($"Game Center exception: {ex.Message}");
        }
    }

    public override async Task<int> GetProgressAsync(string achievementId)
    {
        var (gameCenterId, error) = ValidateAndGetGameCenterId(achievementId);
        if (error != null)
            return 0;

        var achievement = _database.GetById(achievementId)!;

        try
        {
            var tcs = new TaskCompletionSource<int>();

            var callback = Callable.From<Array, Variant>((achievements, err) =>
            {
                if (err.VariantType != Variant.Type.Nil)
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

            CallGKAchievementStatic("load_achievements", callback);

            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(DefaultTimeoutSeconds));
            var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

            if (completedTask == timeoutTask)
                return 0;

            return await tcs.Task;
        }
        catch (Exception ex)
        {
            this.LogError($"Failed to get progress: {ex.Message}");
            return 0;
        }
    }

    public override async Task<SyncResult> IncrementProgressAsync(string achievementId, int amount)
    {
        var (gameCenterId, error) = ValidateAndGetGameCenterId(achievementId);
        if (error != null)
            return SyncResult.FailureResult(error);

        if (amount <= 0)
            return SyncResult.FailureResult("Amount must be positive");

        var achievement = _database.GetById(achievementId)!;

        try
        {
            // Use achievement.CurrentProgress which already has the new total from local provider
            int currentProgress = achievement.CurrentProgress;
            double percentage = achievement.MaxProgress > 0
                ? Math.Min((double)currentProgress / achievement.MaxProgress * 100.0, 100.0)
                : 0;

            var gcAchievement = CreateGKAchievement(gameCenterId!, percentage);
            if (gcAchievement == null)
                return SyncResult.FailureResult("Failed to create GKAchievement instance");

            var tcs = new TaskCompletionSource<SyncResult>();
            var achievementsArray = new Array { gcAchievement };

            var callback = Callable.From<Variant>((err) =>
            {
                if (err.VariantType == Variant.Type.Nil)
                {
                    this.Log($"Set progress for {gameCenterId}: {currentProgress}/{achievement.MaxProgress} ({percentage:F1}%)");
                    tcs.TrySetResult(SyncResult.SuccessResult());
                }
                else
                {
                    var errorMessage = err.AsString();
                    this.LogError($"Failed to set progress for {gameCenterId}: {errorMessage}");
                    tcs.TrySetResult(SyncResult.FailureResult(errorMessage));
                }
            });

            CallGKAchievementStatic("report_achivement", achievementsArray, callback);

            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(DefaultTimeoutSeconds));
            var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

            if (completedTask == timeoutTask)
                return SyncResult.FailureResult("Game Center request timed out");

            return await tcs.Task;
        }
        catch (Exception ex)
        {
            return SyncResult.FailureResult($"Game Center exception: {ex.Message}");
        }
    }

    public override Task<SyncResult> ResetAchievementAsync(string achievementId)
    {
        ResetAchievement(achievementId);
        return Task.FromResult(SyncResult.FailureResult("Game Center does not support resetting individual achievements"));
    }

    public override async Task<SyncResult> ResetAllAchievementsAsync()
    {
        if (!IsAvailable)
            return SyncResult.FailureResult("Game Center is not available");

        try
        {
            var tcs = new TaskCompletionSource<SyncResult>();

            var callback = Callable.From<Variant>((err) =>
            {
                if (err.VariantType == Variant.Type.Nil)
                {
                    this.Log("Successfully reset all achievements");
                    tcs.TrySetResult(SyncResult.SuccessResult());
                }
                else
                {
                    var errorMessage = err.AsString();
                    this.LogError($"Failed to reset achievements: {errorMessage}");
                    tcs.TrySetResult(SyncResult.FailureResult(errorMessage));
                }
            });

            CallGKAchievementStatic("reset_achivements", callback);

            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(DefaultTimeoutSeconds));
            var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

            if (completedTask == timeoutTask)
                return SyncResult.FailureResult("Game Center request timed out");

            return await tcs.Task;
        }
        catch (Exception ex)
        {
            return SyncResult.FailureResult($"Game Center exception: {ex.Message}");
        }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Validates the provider state and returns the Game Center ID for an achievement.
    /// </summary>
    private (string? gameCenterId, string? error) ValidateAndGetGameCenterId(string achievementId)
    {
        if (!_isInitialized)
            return (null, "Game Center plugin not initialized");

        if (!_isAuthenticated)
            return (null, "User not authenticated with Game Center");

        var achievement = _database.GetById(achievementId);
        if (achievement == null)
            return (null, $"Achievement '{achievementId}' not found in database");

        var gameCenterId = achievement.GameCenterId;
        if (string.IsNullOrEmpty(gameCenterId))
            return (null, $"Achievement '{achievementId}' has no Game Center ID configured");

        return (gameCenterId, null);
    }

    /// <summary>
    /// Creates a GKAchievement instance with the specified identifier and progress percentage.
    /// </summary>
    private GodotObject? CreateGKAchievement(string identifier, double percentComplete)
    {
        try
        {
            var achievement = ClassDB.Instantiate("GKAchievement");
            if (achievement.Obj is GodotObject gcAchievement)
            {
                gcAchievement.Set("identifier", identifier);
                gcAchievement.Set("percentComplete", percentComplete);
                gcAchievement.Set("showsCompletionBanner", true);
                return gcAchievement;
            }

            this.LogError("Failed to instantiate GKAchievement");
            return null;
        }
        catch (Exception ex)
        {
            this.LogError($"Error creating GKAchievement: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Calls a static method on the GKAchievement class.
    /// </summary>
    private void CallGKAchievementStatic(string method, params Variant[] args)
    {
        try
        {
            var instance = ClassDB.Instantiate("GKAchievement");
            if (instance.Obj is GodotObject obj)
            {
                obj.Call(method, args);
                return;
            }

            this.LogError($"Failed to call GKAchievement.{method} - class not available");
        }
        catch (Exception ex)
        {
            this.LogError($"Error calling GKAchievement.{method}: {ex.Message}");
        }
    }

    #endregion
}
#endif
