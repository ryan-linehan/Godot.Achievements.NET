#if GODOT_ANDROID
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot.Achievements.Core;
using Godot.Achievements.Providers;

namespace Godot.Achievements.Providers.GooglePlay;

/// <summary>
/// Helper node to receive app lifecycle notifications.
/// </summary>
internal partial class AppResumeListener : Node
{
    public event Action? OnAppResumed;

    public override void _Notification(int what)
    {
        if (what == NotificationApplicationResumed)
        {
            OnAppResumed?.Invoke();
        }
    }
}

/// <summary>
/// Google Play Games achievement provider for Android.
/// Integrates with the GodotPlayGameServices addon.
///
/// Note: Google Play operations are inherently async. Sync methods fire-and-forget.
/// Async methods await the corresponding Godot signal from the plugin.
/// </summary>
public partial class GooglePlayAchievementProvider : AchievementProviderBase
{
    private const double DefaultTimeoutSeconds = 10.0;

    public static bool IsPlatformSupported => true;

    private readonly AchievementDatabase _database;
    private Node? _godotPlayGameServices;
    private Node? _signInClient;
    private Node? _achievementsClient;
    private AppResumeListener? _resumeListener;
    private bool _isInitialized;
    private bool _isAuthenticated;
    private bool _signInAttempted;

    public override string ProviderName => ProviderNames.GooglePlay;

    public GooglePlayAchievementProvider(AchievementDatabase database)
    {
        _database = database;
        Initialize();
    }

    private void Initialize()
    {
        try
        {
            var sceneTree = Engine.GetMainLoop() as SceneTree;
            if (sceneTree?.Root == null)
            {
                this.LogError("Could not access scene tree");
                _isInitialized = false;
                return;
            }

            _godotPlayGameServices = sceneTree.Root.GetNodeOrNull("GodotPlayGameServices") as Node;
            if (_godotPlayGameServices == null)
            {
                this.LogWarning("GodotPlayGameServices autoload not found.");
                _isInitialized = false;
                return;
            }

            var initResult = _godotPlayGameServices.Call("initialize");
            if (initResult.AsInt32() != 0)
            {
                this.LogError("Failed to initialize Google Play Games Services plugin.");
                _isInitialized = false;
                return;
            }

            this.Log("GodotPlayGameServices plugin initialized");
            InstanceClientNodes(sceneTree.Root);

            if (_signInClient == null || _achievementsClient == null)
            {
                this.LogError("Failed to create client nodes");
                _isInitialized = false;
                return;
            }

            if (!sceneTree.Root.IsConnected("tree_exiting", Callable.From(Cleanup)))
            {
                sceneTree.Root.Connect("tree_exiting", Callable.From(Cleanup));
            }

            Callable.From(ConnectSignals).CallDeferred();
            Callable.From(CheckAuthentication).CallDeferred();

            _isInitialized = true;
            this.Log("Initialized with GodotPlayGameServices addon");
        }
        catch (Exception ex)
        {
            this.LogError($"Failed to initialize: {ex.Message}");
            _isInitialized = false;
        }
    }

    private void InstanceClientNodes(Node root)
    {
        try
        {
            var signInScript = GD.Load<Script>("res://addons/GodotPlayGameServices/scripts/sign_in/sign_in_client.gd");
            if (signInScript != null)
            {
                _signInClient = new Node();
                _signInClient.SetScript(signInScript);
                _signInClient.Name = "GooglePlaySignInClient";
                root.CallDeferred("add_child", _signInClient);
                this.Log("SignInClient node created");
            }

            var achievementsScript = GD.Load<Script>("res://addons/GodotPlayGameServices/scripts/achievements/achievements_client.gd");
            if (achievementsScript != null)
            {
                _achievementsClient = new Node();
                _achievementsClient.SetScript(achievementsScript);
                _achievementsClient.Name = "GooglePlayAchievementsClient";
                root.CallDeferred("add_child", _achievementsClient);
                this.Log("AchievementsClient node created");
            }

            _resumeListener = new AppResumeListener();
            _resumeListener.Name = "GooglePlayResumeListener";
            _resumeListener.OnAppResumed += OnAppResumed;
            root.CallDeferred("add_child", _resumeListener);
        }
        catch (Exception ex)
        {
            this.LogError($"Failed to instance client nodes: {ex.Message}");
        }
    }

    private void OnAppResumed()
    {
        if (_signInAttempted && !_isAuthenticated && _signInClient != null)
        {
            this.Log("App resumed after sign-in, checking auth status...");
            _signInClient.Call("is_authenticated");
        }
    }

    private void ConnectSignals()
    {
        if (_signInClient != null)
        {
            if (!_signInClient.IsConnected("user_authenticated", Callable.From<bool>(OnUserAuthenticated)))
            {
                _signInClient.Connect("user_authenticated", Callable.From<bool>(OnUserAuthenticated));
            }
        }

        // Connect achievement signals to relay results from sync methods
        if (_achievementsClient != null)
        {
            if (!_achievementsClient.IsConnected("achievement_unlocked", Callable.From<bool, string>(OnAchievementUnlocked)))
            {
                _achievementsClient.Connect("achievement_unlocked", Callable.From<bool, string>(OnAchievementUnlocked));
            }
        }
    }

    private void OnAchievementUnlocked(bool isUnlocked, string googlePlayId)
    {
        // Find the achievement ID from the Google Play ID
        var achievementId = FindAchievementIdByGooglePlayId(googlePlayId);
        if (achievementId != null)
        {
            EmitAchievementUnlocked(achievementId, isUnlocked, isUnlocked ? null : "Failed to unlock achievement");
        }
    }

    private string? FindAchievementIdByGooglePlayId(string googlePlayId)
    {
        foreach (var achievement in _database.Achievements)
        {
            if (achievement.GooglePlayId == googlePlayId)
            {
                return achievement.Id;
            }
        }
        return null;
    }

    private void CheckAuthentication()
    {
        if (_signInClient == null) return;
        _signInClient.Call("is_authenticated");
    }

    private void OnUserAuthenticated(bool isAuthenticated)
    {
        this.Log($"Authentication status changed: {isAuthenticated}");
        _isAuthenticated = isAuthenticated;

        if (!isAuthenticated && !_signInAttempted && _signInClient != null)
        {
            _signInAttempted = true;
            this.Log("Not authenticated, attempting sign-in...");
            _signInClient.Call("sign_in");
        }
    }

    public override bool IsAvailable => _isInitialized && _isAuthenticated && _achievementsClient != null;

    #region Sync Methods (Fire-and-Forget)

    public override void UnlockAchievement(string achievementId)
    {
        var (googlePlayId, error) = ValidateAndGetGooglePlayId(achievementId);
        if (error != null)
        {
            EmitAchievementUnlocked(achievementId, false, error);
            return;
        }

        _achievementsClient!.Call("unlock_achievement", googlePlayId);
        this.Log($"Unlock fired for: {googlePlayId}");
        // Signal will be emitted via OnAchievementUnlocked when plugin responds
    }

    public override void IncrementProgress(string achievementId, int amount)
    {
        var (googlePlayId, error) = ValidateAndGetGooglePlayId(achievementId);
        if (error != null)
        {
            EmitProgressIncremented(achievementId, 0, false, error);
            return;
        }

        _achievementsClient!.Call("increment_achievement", googlePlayId, amount);
        this.Log($"Incremented progress for: {googlePlayId} by {amount}");
        // Signal will be emitted via OnAchievementUnlocked if it triggers an unlock
        // Note: Google Play doesn't have a dedicated progress callback, only unlock
        EmitProgressIncremented(achievementId, amount, true);
    }

    public override void ResetAchievement(string achievementId)
    {
        this.LogWarning("ResetAchievement is not supported on Google Play Games.");
        EmitAchievementReset(achievementId, false, "ResetAchievement is not supported on Google Play Games");
    }

    public override void ResetAllAchievements()
    {
        this.LogWarning("ResetAllAchievements is not supported on Google Play Games.");
        EmitAllAchievementsReset(false, "ResetAllAchievements is not supported on Google Play Games");
    }

    #endregion

    #region Async Methods (Await Godot Signals)

    public override async Task<AchievementUnlockResult> UnlockAchievementAsync(string achievementId)
    {
        var (googlePlayId, error) = ValidateAndGetGooglePlayId(achievementId);
        if (error != null)
            return AchievementUnlockResult.FailureResult(error);

        try
        {
            _achievementsClient!.Call("unlock_achievement", googlePlayId);

            var result = await AwaitSignalWithTimeout(_achievementsClient, "achievement_unlocked", DefaultTimeoutSeconds);
            if (result == null)
                return AchievementUnlockResult.FailureResult("Unlock operation timed out");

            var isUnlocked = result[0].AsBool();
            this.Log($"Unlocked achievement: {googlePlayId} (success: {isUnlocked})");

            return isUnlocked
                ? AchievementUnlockResult.SuccessResult()
                : AchievementUnlockResult.FailureResult("Google Play Games failed to unlock achievement");
        }
        catch (Exception ex)
        {
            return AchievementUnlockResult.FailureResult($"Exception unlocking achievement: {ex.Message}");
        }
    }

    public override async Task<int> GetProgressAsync(string achievementId)
    {
        var (googlePlayId, error) = ValidateAndGetGooglePlayId(achievementId);
        if (error != null)
            return 0;

        try
        {
            var achievements = await LoadAchievementsAsync();
            foreach (var gpAchievement in achievements)
            {
                if (gpAchievement.achievement_id == googlePlayId)
                {
                    return gpAchievement.current_steps;
                }
            }
        }
        catch (Exception ex)
        {
            this.LogError($"Failed to get progress: {ex.Message}");
        }

        return 0;
    }

    public override async Task<SyncResult> IncrementProgressAsync(string achievementId, int amount)
    {
        var (googlePlayId, error) = ValidateAndGetGooglePlayId(achievementId);
        if (error != null)
            return SyncResult.FailureResult(error);

        if (amount <= 0)
            return SyncResult.FailureResult("Amount must be positive");

        try
        {
            _achievementsClient!.Call("increment_achievement", googlePlayId, amount);

            var result = await AwaitSignalWithTimeout(_achievementsClient, "achievement_unlocked", DefaultTimeoutSeconds);
            if (result == null)
            {
                this.LogWarning($"Increment operation timed out for {googlePlayId}");
                // Still return success since the operation was fired
                return SyncResult.SuccessResult();
            }

            var isUnlocked = result[0].AsBool();
            this.Log($"Incremented achievement: {googlePlayId} by {amount} (unlocked: {isUnlocked})");
            return SyncResult.SuccessResult();
        }
        catch (Exception ex)
        {
            return SyncResult.FailureResult($"Exception incrementing progress: {ex.Message}");
        }
    }

    public override Task<SyncResult> ResetAchievementAsync(string achievementId)
    {
        ResetAchievement(achievementId);
        return Task.FromResult(SyncResult.SuccessResult("Google Play Games does not support resetting achievements."));
    }

    public override Task<SyncResult> ResetAllAchievementsAsync()
    {
        ResetAllAchievements();
        return Task.FromResult(SyncResult.SuccessResult("Google Play Games does not support resetting achievements."));
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Validates the provider state and returns the Google Play ID for an achievement.
    /// </summary>
    private (string? googlePlayId, string? error) ValidateAndGetGooglePlayId(string achievementId)
    {
        if (!_isInitialized)
            return (null, "Google Play Games plugin not initialized");

        if (!_isAuthenticated)
            return (null, "User not authenticated with Google Play Games");

        if (_achievementsClient == null)
            return (null, "AchievementsClient not available");

        var achievement = _database.GetById(achievementId);
        if (achievement == null)
            return (null, $"Achievement '{achievementId}' not found in database");

        var googlePlayId = achievement.GooglePlayId;
        if (string.IsNullOrEmpty(googlePlayId))
            return (null, $"Achievement '{achievementId}' has no Google Play ID configured");

        return (googlePlayId, null);
    }

    /// <summary>
    /// Awaits a Godot signal with a timeout.
    /// Returns the signal arguments or null if timed out.
    /// </summary>
    private async Task<Godot.Collections.Array?> AwaitSignalWithTimeout(GodotObject target, string signalName, double timeoutSeconds)
    {
        var tcs = new TaskCompletionSource<Godot.Collections.Array?>();

        var callable = Callable.From<Variant, Variant>((arg0, arg1) =>
        {
            tcs.TrySetResult(new Godot.Collections.Array { arg0, arg1 });
        });

        target.Connect(signalName, callable, (uint)GodotObject.ConnectFlags.OneShot);

        return await AsyncTimeoutHelper.AwaitWithTimeout(tcs, timeoutSeconds, null);
    }

    /// <summary>
    /// Record type for parsed achievement data from Google Play.
    /// </summary>
    private record GooglePlayAchievementData(
        string achievement_id,
        string achievement_name,
        int state,
        int type,
        int current_steps,
        int total_steps
    );

    private async Task<List<GooglePlayAchievementData>> LoadAchievementsAsync(bool forceReload = false)
    {
        if (_achievementsClient == null)
            return new List<GooglePlayAchievementData>();

        try
        {
            _achievementsClient.Call("load_achievements", forceReload);

            var result = await AwaitAchievementsLoadedWithTimeout(DefaultTimeoutSeconds + 5);
            return result ?? new List<GooglePlayAchievementData>();
        }
        catch (Exception ex)
        {
            this.LogError($"Failed to load achievements: {ex.Message}");
            return new List<GooglePlayAchievementData>();
        }
    }

    private async Task<List<GooglePlayAchievementData>?> AwaitAchievementsLoadedWithTimeout(double timeoutSeconds)
    {
        var tcs = new TaskCompletionSource<List<GooglePlayAchievementData>?>();

        var callable = Callable.From<Godot.Collections.Array>((achievements) =>
        {
            var result = new List<GooglePlayAchievementData>();
            foreach (var item in achievements)
            {
                if (item.Obj is GodotObject achievementObj)
                {
                    result.Add(new GooglePlayAchievementData(
                        achievementObj.Get("achievement_id").AsString(),
                        achievementObj.Get("achievement_name").AsString(),
                        achievementObj.Get("state").AsInt32(),
                        achievementObj.Get("type").AsInt32(),
                        achievementObj.Get("current_steps").AsInt32(),
                        achievementObj.Get("total_steps").AsInt32()
                    ));
                }
            }
            tcs.TrySetResult(result);
        });

        _achievementsClient!.Connect("achievements_loaded", callable, (uint)GodotObject.ConnectFlags.OneShot);

        return await AsyncTimeoutHelper.AwaitWithTimeout(tcs, timeoutSeconds, null);
    }

    public async Task<bool> RevealAchievementAsync(string achievementId)
    {
        var (googlePlayId, error) = ValidateAndGetGooglePlayId(achievementId);
        if (error != null)
            return false;

        try
        {
            _achievementsClient!.Call("reveal_achievement", googlePlayId);

            var result = await AwaitSignalWithTimeout(_achievementsClient, "achievement_revealed", DefaultTimeoutSeconds);
            if (result == null)
                return false;

            return result[0].AsBool();
        }
        catch (Exception ex)
        {
            this.LogError($"Failed to reveal achievement: {ex.Message}");
            return false;
        }
    }

    public void ShowAchievementsUI()
    {
        if (!IsAvailable)
        {
            this.LogWarning("Cannot show achievements UI - provider not available");
            return;
        }

        _achievementsClient!.Call("show_achievements");
    }

    public void SignIn()
    {
        if (_signInClient == null)
        {
            this.LogWarning("SignInClient not available");
            return;
        }

        _signInClient.Call("sign_in");
    }

    public bool IsAuthenticated => _isAuthenticated;

    private void Cleanup()
    {
        if (_signInClient != null && GodotObject.IsInstanceValid(_signInClient))
        {
            _signInClient.QueueFree();
            _signInClient = null;
        }

        if (_achievementsClient != null && GodotObject.IsInstanceValid(_achievementsClient))
        {
            _achievementsClient.QueueFree();
            _achievementsClient = null;
        }

        if (_resumeListener != null && GodotObject.IsInstanceValid(_resumeListener))
        {
            _resumeListener.OnAppResumed -= OnAppResumed;
            _resumeListener.QueueFree();
            _resumeListener = null;
        }

        _isInitialized = false;
        _isAuthenticated = false;
    }

    #endregion
}
#endif
