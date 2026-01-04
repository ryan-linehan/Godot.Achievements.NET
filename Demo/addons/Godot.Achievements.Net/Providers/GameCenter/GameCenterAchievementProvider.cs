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

    // API method names (typos are intentional - matches GodotApplePlugins API)
    private static readonly StringName MethodReportAchievement = "report_achivement";
    private static readonly StringName MethodResetAchievements = "reset_achivements";
    private static readonly StringName MethodLoadAchievements = "load_achievements";

    public static bool IsPlatformSupported => true;

    private readonly AchievementDatabase _database;
    private GodotObject? _gameCenterManager;
    private GodotObject? _localPlayer;
    private GodotObject? _gkAchievementStatic; // Cached instance for static method calls
    private TaskCompletionSource<bool>? _authenticationTcs;
    private bool _isInitialized;
    private bool _isAuthenticated;
    private bool _isDisposed;

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
            if (!ClassDB.ClassExists("GameCenterManager"))
            {
                this.LogWarning("GameCenterManager class not found - ensure GodotApplePlugins is installed");
                _isInitialized = false;
                return;
            }

            if (!ClassDB.ClassExists("GKAchievement"))
            {
                this.LogWarning("GKAchievement class not found - ensure GodotApplePlugins is installed");
                _isInitialized = false;
                return;
            }

            // Create GameCenterManager instance
            var managerInstance = ClassDB.Instantiate("GameCenterManager");
            if (managerInstance.Obj is GodotObject managerObj)
            {
                _gameCenterManager = managerObj;

                // Connect to authentication signals
                _gameCenterManager.Connect("authentication_result", Callable.From<bool>(OnAuthenticationResult));
                _gameCenterManager.Connect("authentication_error", Callable.From<string>(OnAuthenticationError));

                // Cache a GKAchievement instance for static method calls
                var gkInstance = ClassDB.Instantiate("GKAchievement");
                if (gkInstance.Obj is GodotObject gkObj)
                {
                    _gkAchievementStatic = gkObj;
                }

                // Connect cleanup to tree exit
                var sceneTree = Engine.GetMainLoop() as SceneTree;
                if (sceneTree?.Root != null)
                {
                    sceneTree.Root.TreeExiting += Cleanup;
                }

                _isInitialized = true;
                this.Log("GameCenterManager initialized, attempting authentication...");

                // Trigger authentication
                _gameCenterManager.Call("authenticate");
            }
            else
            {
                this.LogWarning("Could not instantiate GameCenterManager");
                _isInitialized = false;
            }
        }
        catch (Exception ex)
        {
            this.LogError($"Failed to initialize: {ex.Message}");
            _isInitialized = false;
        }
    }

    /// <summary>
    /// Authenticates the player with Game Center asynchronously.
    /// Returns true if authentication succeeded, false otherwise.
    /// </summary>
    public async Task<bool> AuthenticateAsync()
    {
        if (_isAuthenticated)
            return true;

        if (!_isInitialized || _gameCenterManager == null)
            return false;

        _authenticationTcs = new TaskCompletionSource<bool>();
        _gameCenterManager.Call("authenticate");

        return await AsyncTimeoutHelper.AwaitWithTimeout(_authenticationTcs, DefaultTimeoutSeconds, false);
    }

    private void OnAuthenticationResult(bool success)
    {
        _isAuthenticated = success;
        this.Log($"Authentication result: {(success ? "success" : "failed")}");

        if (success && _gameCenterManager != null)
        {
            // Get the local player from GameCenterManager
            var localPlayerVariant = _gameCenterManager.Get("local_player");
            if (localPlayerVariant.Obj is GodotObject localPlayerObj)
            {
                _localPlayer = localPlayerObj;
                var playerAlias = _localPlayer.Get("alias").AsString();
                this.Log($"Authenticated as: {playerAlias}");
            }
        }

        _authenticationTcs?.TrySetResult(success);
    }

    private void OnAuthenticationError(string error)
    {
        _isAuthenticated = false;
        this.LogError($"Authentication error: {error}");
        _authenticationTcs?.TrySetResult(false);
    }

    /// <summary>
    /// Refreshes the authentication status from Game Center.
    /// Call this to check if authentication state has changed.
    /// </summary>
    public void RefreshAuthenticationStatus()
    {
        if (_localPlayer == null || !_isInitialized)
        {
            this.LogWarning("Cannot refresh auth status - provider not initialized");
            return;
        }

        try
        {
            bool wasAuthenticated = _isAuthenticated;
            _isAuthenticated = _localPlayer.Get("is_authenticated").AsBool();

            if (wasAuthenticated != _isAuthenticated)
            {
                this.Log($"Authentication status changed: {wasAuthenticated} -> {_isAuthenticated}");

                if (_isAuthenticated)
                {
                    var playerAlias = _localPlayer.Get("alias").AsString();
                    this.Log($"Now authenticated as: {playerAlias}");
                }
            }
        }
        catch (Exception ex)
        {
            this.LogError($"Failed to refresh authentication status: {ex.Message}");
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

        var achievementsArray = new Godot.Collections.Array { gcAchievement };
        var callback = Callable.From<Variant>((err) =>
        {
            if (IsSuccess(err))
            {
                this.Log($"Unlocked achievement: {gameCenterId}");
                EmitAchievementUnlocked(achievementId, true, null);
            }
            else
            {
                var errorMessage = err.AsString();
                this.LogError($"Failed to unlock achievement {gameCenterId}: {errorMessage}");
                EmitAchievementUnlocked(achievementId, false, errorMessage);
            }
        });

        CallGKAchievementStatic(MethodReportAchievement, achievementsArray, callback);
        this.Log($"Unlock fired for: {gameCenterId}");
    }

    /// <remarks>
    /// The <paramref name="amount"/> parameter is validated but not directly used.
    /// Game Center only supports absolute percentages, not increments. The actual progress
    /// is read from achievement.CurrentProgress which already includes the increment
    /// applied by LocalAchievementProvider.
    /// </remarks>
    public override void IncrementProgress(string achievementId, int amount)
    {
        var (gameCenterId, error) = ValidateAndGetGameCenterId(achievementId);
        if (error != null)
        {
            EmitProgressIncremented(achievementId, 0, false, error);
            return;
        }

        if (amount <= 0)
        {
            EmitProgressIncremented(achievementId, 0, false, "Amount must be positive");
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

        var achievementsArray = new Godot.Collections.Array { gcAchievement };
        var callback = Callable.From<Variant>((err) =>
        {
            if (IsSuccess(err))
            {
                this.Log($"Set progress for {gameCenterId}: {currentProgress}/{achievement.MaxProgress} ({percentage:F1}%)");
                EmitProgressIncremented(achievementId, currentProgress, true, null);
            }
            else
            {
                var errorMessage = err.AsString();
                this.LogError($"Failed to set progress for {gameCenterId}: {errorMessage}");
                EmitProgressIncremented(achievementId, currentProgress, false, errorMessage);
            }
        });

        CallGKAchievementStatic(MethodReportAchievement, achievementsArray, callback);
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
            if (IsSuccess(err))
            {
                this.Log("Reset all achievements");
                EmitAllAchievementsReset(true, null);
            }
            else
            {
                var errorMessage = err.AsString();
                this.LogError($"Failed to reset achievements: {errorMessage}");
                EmitAllAchievementsReset(false, errorMessage);
            }
        });

        CallGKAchievementStatic(MethodResetAchievements, callback);
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
            var achievementsArray = new Godot.Collections.Array { gcAchievement };

            var callback = Callable.From<Variant>((err) =>
            {
                if (IsSuccess(err))
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

            CallGKAchievementStatic(MethodReportAchievement, achievementsArray, callback);

            return await AsyncTimeoutHelper.AwaitWithTimeout(tcs, DefaultTimeoutSeconds,
                AchievementUnlockResult.FailureResult("Game Center request timed out"));
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

            var callback = Callable.From<Godot.Collections.Array, Variant>((achievements, err) =>
            {
                if (!IsSuccess(err))
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
                            var percentComplete = gcAchievement.Get("percent_complete").AsDouble();
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

            CallGKAchievementStatic(MethodLoadAchievements, callback);

            return await AsyncTimeoutHelper.AwaitWithTimeout(tcs, DefaultTimeoutSeconds, 0);
        }
        catch (Exception ex)
        {
            this.LogError($"Failed to get progress: {ex.Message}");
            return 0;
        }
    }

    /// <inheritdoc cref="IncrementProgress"/>
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
            // Game Center uses percentages - CurrentProgress already includes increment from local provider
            int currentProgress = achievement.CurrentProgress;
            double percentage = achievement.MaxProgress > 0
                ? Math.Min((double)currentProgress / achievement.MaxProgress * 100.0, 100.0)
                : 0;

            var gcAchievement = CreateGKAchievement(gameCenterId!, percentage);
            if (gcAchievement == null)
                return SyncResult.FailureResult("Failed to create GKAchievement instance");

            var tcs = new TaskCompletionSource<SyncResult>();
            var achievementsArray = new Godot.Collections.Array { gcAchievement };

            var callback = Callable.From<Variant>((err) =>
            {
                if (IsSuccess(err))
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

            CallGKAchievementStatic(MethodReportAchievement, achievementsArray, callback);

            return await AsyncTimeoutHelper.AwaitWithTimeout(tcs, DefaultTimeoutSeconds,
                SyncResult.FailureResult("Game Center request timed out"));
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
                if (IsSuccess(err))
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

            CallGKAchievementStatic(MethodResetAchievements, callback);

            return await AsyncTimeoutHelper.AwaitWithTimeout(tcs, DefaultTimeoutSeconds,
                SyncResult.FailureResult("Game Center request timed out"));
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
                gcAchievement.Set("percent_complete", percentComplete);
                gcAchievement.Set("shows_completion_banner", true);
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
    /// Calls a static method on the GKAchievement class using the cached instance.
    /// </summary>
    private void CallGKAchievementStatic(StringName method, params Variant[] args)
    {
        try
        {
            if (_gkAchievementStatic == null)
            {
                this.LogError($"Failed to call GKAchievement.{method} - cached instance not available");
                return;
            }

            _gkAchievementStatic.Call(method, args);
        }
        catch (Exception ex)
        {
            this.LogError($"Error calling GKAchievement.{method}: {ex.Message}");
        }
    }

    /// <summary>
    /// Checks if a Game Center callback error variant indicates success.
    /// </summary>
    private static bool IsSuccess(Variant error) => error.VariantType == Variant.Type.Nil;

    /// <summary>
    /// Cleans up resources used by the provider.
    /// Call this when the provider is no longer needed.
    /// </summary>
    public void Cleanup()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        _isAuthenticated = false;
        _isInitialized = false;

        // Disconnect from tree exiting
        var sceneTree = Engine.GetMainLoop() as SceneTree;
        if (sceneTree?.Root != null)
        {
            sceneTree.Root.TreeExiting -= Cleanup;
        }

        if (_gameCenterManager != null)
        {
            _gameCenterManager.Disconnect("authentication_result", Callable.From<bool>(OnAuthenticationResult));
            _gameCenterManager.Disconnect("authentication_error", Callable.From<string>(OnAuthenticationError));
            _gameCenterManager = null;
        }

        _localPlayer = null;
        _gkAchievementStatic = null;

        this.Log("GameCenterAchievementProvider cleaned up");
    }

    #endregion
}
#endif
