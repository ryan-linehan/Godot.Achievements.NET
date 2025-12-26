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
/// Note: Google Play operations are inherently async. Sync methods start the operation
/// and return immediately. Use async methods if you need to wait for the result.
/// </summary>
public partial class GooglePlayAchievementProvider : AchievementProviderBase
{
    public static bool IsPlatformSupported => true;

    private readonly AchievementDatabase _database;
    private Node? _godotPlayGameServices;
    private Node? _signInClient;
    private Node? _achievementsClient;
    private AppResumeListener? _resumeListener;
    private bool _isInitialized;
    private bool _isAuthenticated;
    private bool _signInAttempted;

    // Track pending callbacks for async operations
    private readonly Dictionary<string, TaskCompletionSource<bool>> _pendingUnlocks = new();
    private readonly Dictionary<string, TaskCompletionSource<bool>> _pendingReveals = new();
    private TaskCompletionSource<List<Dictionary<string, object>>>? _pendingLoad;

    // Track pending sync operations for signal emission (maps googlePlayId -> achievementId)
    private readonly Dictionary<string, string> _pendingSyncAchievementIds = new();
    private readonly Dictionary<string, string> _pendingSyncProgressIds = new();

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

        if (_achievementsClient != null)
        {
            if (!_achievementsClient.IsConnected("achievement_unlocked", Callable.From<bool, string>(OnAchievementUnlocked)))
            {
                _achievementsClient.Connect("achievement_unlocked", Callable.From<bool, string>(OnAchievementUnlocked));
            }

            if (!_achievementsClient.IsConnected("achievement_revealed", Callable.From<bool, string>(OnAchievementRevealed)))
            {
                _achievementsClient.Connect("achievement_revealed", Callable.From<bool, string>(OnAchievementRevealed));
            }

            if (!_achievementsClient.IsConnected("achievements_loaded", Callable.From<Godot.Collections.Array>(OnAchievementsLoaded)))
            {
                _achievementsClient.Connect("achievements_loaded", Callable.From<Godot.Collections.Array>(OnAchievementsLoaded));
            }
        }
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

    private void OnAchievementUnlocked(bool isUnlocked, string googlePlayId)
    {
        // Handle async operations
        if (_pendingUnlocks.TryGetValue(googlePlayId, out var taskResult))
        {
            taskResult.TrySetResult(isUnlocked);
            _pendingUnlocks.Remove(googlePlayId);
        }

        // Emit signal for sync operations
        if (_pendingSyncAchievementIds.TryGetValue(googlePlayId, out var achievementId))
        {
            EmitAchievementUnlocked(achievementId, isUnlocked, isUnlocked ? null : "Failed to unlock achievement");
            _pendingSyncAchievementIds.Remove(googlePlayId);
        }
    }

    private void OnAchievementRevealed(bool isRevealed, string achievementId)
    {
        if (_pendingReveals.TryGetValue(achievementId, out var taskResult))
        {
            taskResult.TrySetResult(isRevealed);
            _pendingReveals.Remove(achievementId);
        }
    }

    private void OnAchievementsLoaded(Godot.Collections.Array achievements)
    {
        if (_pendingLoad == null) return;

        var result = new List<Dictionary<string, object>>();

        foreach (var item in achievements)
        {
            if (item.Obj is GodotObject achievementObj)
            {
                var dict = new Dictionary<string, object>
                {
                    ["achievement_id"] = achievementObj.Get("achievement_id").AsString(),
                    ["achievement_name"] = achievementObj.Get("achievement_name").AsString(),
                    ["state"] = achievementObj.Get("state").AsInt32(),
                    ["type"] = achievementObj.Get("type").AsInt32(),
                    ["current_steps"] = achievementObj.Get("current_steps").AsInt32(),
                    ["total_steps"] = achievementObj.Get("total_steps").AsInt32()
                };
                result.Add(dict);
            }
        }

        _pendingLoad.TrySetResult(result);
        _pendingLoad = null;
    }

    public override bool IsAvailable => _isInitialized && _isAuthenticated && _achievementsClient != null;

    #region Sync Methods

    public override void UnlockAchievement(string achievementId)
    {
        if (!_isInitialized)
        {
            this.LogWarning("Google Play Games plugin not initialized");
            EmitAchievementUnlocked(achievementId, false, "Google Play Games plugin not initialized");
            return;
        }

        if (!_isAuthenticated)
        {
            this.LogWarning("User not authenticated with Google Play Games");
            EmitAchievementUnlocked(achievementId, false, "User not authenticated with Google Play Games");
            return;
        }

        if (_achievementsClient == null)
        {
            this.LogWarning("AchievementsClient not available");
            EmitAchievementUnlocked(achievementId, false, "AchievementsClient not available");
            return;
        }

        var achievement = _database.GetById(achievementId);
        if (achievement == null)
        {
            this.LogWarning($"Achievement '{achievementId}' not found in database");
            EmitAchievementUnlocked(achievementId, false, $"Achievement '{achievementId}' not found in database");
            return;
        }

        var googlePlayId = achievement.GooglePlayId;
        if (string.IsNullOrEmpty(googlePlayId))
        {
            this.LogWarning($"Achievement '{achievementId}' has no Google Play ID configured");
            EmitAchievementUnlocked(achievementId, false, $"Achievement '{achievementId}' has no Google Play ID configured");
            return;
        }

        // Fire-and-forget: start the operation, signal will be emitted in OnAchievementUnlocked callback
        _pendingSyncAchievementIds[googlePlayId] = achievementId;
        _achievementsClient.Call("unlock_achievement", googlePlayId);
        this.Log($"Unlock started for: {googlePlayId}");
    }

    public override void IncrementProgress(string achievementId, int amount)
    {
        if (!_isInitialized)
        {
            this.LogWarning("Google Play Games plugin not initialized");
            EmitProgressIncremented(achievementId, 0, false, "Google Play Games plugin not initialized");
            return;
        }

        if (!_isAuthenticated)
        {
            this.LogWarning("User not authenticated with Google Play Games");
            EmitProgressIncremented(achievementId, 0, false, "User not authenticated with Google Play Games");
            return;
        }

        if (_achievementsClient == null)
        {
            this.LogWarning("AchievementsClient not available");
            EmitProgressIncremented(achievementId, 0, false, "AchievementsClient not available");
            return;
        }

        var achievement = _database.GetById(achievementId);
        if (achievement == null)
        {
            this.LogWarning($"Achievement '{achievementId}' not found in database");
            EmitProgressIncremented(achievementId, 0, false, $"Achievement '{achievementId}' not found in database");
            return;
        }

        var googlePlayId = achievement.GooglePlayId;
        if (string.IsNullOrEmpty(googlePlayId))
        {
            this.LogWarning($"Achievement '{achievementId}' has no Google Play ID configured");
            EmitProgressIncremented(achievementId, 0, false, $"Achievement '{achievementId}' has no Google Play ID configured");
            return;
        }

        // Fire-and-forget: increment steps - signal emitted in OnAchievementUnlocked callback if it auto-unlocks
        _pendingSyncProgressIds[googlePlayId] = achievementId;
        _achievementsClient.Call("increment_achievement", googlePlayId, amount);
        this.Log($"Incremented progress for: {googlePlayId} by {amount}");

        // Emit progress signal immediately since Google Play doesn't have a specific progress callback
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

    #region Async Methods

    public override async Task<AchievementUnlockResult> UnlockAchievementAsync(string achievementId)
    {
        if (!_isInitialized)
            return AchievementUnlockResult.FailureResult("Google Play Games plugin not initialized");

        if (!_isAuthenticated)
            return AchievementUnlockResult.FailureResult("User not authenticated with Google Play Games");

        if (_achievementsClient == null)
            return AchievementUnlockResult.FailureResult("AchievementsClient not available");

        var achievement = _database.GetById(achievementId);
        if (achievement == null)
            return AchievementUnlockResult.FailureResult($"Achievement '{achievementId}' not found in database");

        var googlePlayId = achievement.GooglePlayId;
        if (string.IsNullOrEmpty(googlePlayId))
            return AchievementUnlockResult.FailureResult($"Achievement '{achievementId}' has no Google Play ID configured");

        try
        {
            var taskResult = new TaskCompletionSource<bool>();
            _pendingUnlocks[googlePlayId] = taskResult;

            _achievementsClient.Call("unlock_achievement", googlePlayId);

            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(10));
            var completedTask = await Task.WhenAny(taskResult.Task, timeoutTask);

            if (completedTask == timeoutTask)
            {
                _pendingUnlocks.Remove(googlePlayId);
                return AchievementUnlockResult.FailureResult("Unlock operation timed out");
            }

            var success = await taskResult.Task;
            this.Log($"Unlocked achievement: {googlePlayId} (success: {success})");

            return success
                ? AchievementUnlockResult.SuccessResult()
                : AchievementUnlockResult.FailureResult("Google Play Games failed to unlock achievement");
        }
        catch (Exception ex)
        {
            _pendingUnlocks.Remove(googlePlayId);
            return AchievementUnlockResult.FailureResult($"Exception unlocking achievement: {ex.Message}");
        }
    }

    public override async Task<int> GetProgressAsync(string achievementId)
    {
        if (!IsAvailable)
            return 0;

        var achievement = _database.GetById(achievementId);
        if (achievement == null)
            return 0;

        var googlePlayId = achievement.GooglePlayId;
        if (string.IsNullOrEmpty(googlePlayId))
            return 0;

        try
        {
            var achievements = await LoadAchievementsAsync();

            foreach (var gpAchievement in achievements)
            {
                if (gpAchievement.TryGetValue("achievement_id", out var id) && id?.ToString() == googlePlayId)
                {
                    if (gpAchievement.TryGetValue("current_steps", out var steps))
                    {
                        return Convert.ToInt32(steps);
                    }
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
        if (!_isInitialized)
            return SyncResult.FailureResult("Google Play Games plugin not initialized");

        if (!_isAuthenticated)
            return SyncResult.FailureResult("User not authenticated with Google Play Games");

        if (_achievementsClient == null)
            return SyncResult.FailureResult("AchievementsClient not available");

        var achievement = _database.GetById(achievementId);
        if (achievement == null)
            return SyncResult.FailureResult($"Achievement '{achievementId}' not found in database");

        var googlePlayId = achievement.GooglePlayId;
        if (string.IsNullOrEmpty(googlePlayId))
            return SyncResult.FailureResult($"Achievement '{achievementId}' has no Google Play ID configured");

        if (amount <= 0)
        {
            return SyncResult.FailureResult("Amount must be positive");
        }

        try
        {
            var taskResult = new TaskCompletionSource<bool>();
            _pendingUnlocks[googlePlayId] = taskResult;

            _achievementsClient.Call("increment_achievement", googlePlayId, amount);

            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(10));
            var completedTask = await Task.WhenAny(taskResult.Task, timeoutTask);

            if (completedTask == timeoutTask)
            {
                _pendingUnlocks.Remove(googlePlayId);
                this.LogWarning($"Increment operation timed out for {googlePlayId}");
            }
            else
            {
                var success = await taskResult.Task;
                this.Log($"Incremented achievement: {googlePlayId} by {amount} (success: {success})");
            }

            return SyncResult.SuccessResult();
        }
        catch (Exception ex)
        {
            _pendingUnlocks.Remove(googlePlayId);
            return SyncResult.FailureResult($"Exception setting progress: {ex.Message}");
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

    #region Additional Methods

    private async Task<List<Dictionary<string, object>>> LoadAchievementsAsync(bool forceReload = false)
    {
        if (_achievementsClient == null)
            return new List<Dictionary<string, object>>();

        try
        {
            _pendingLoad = new TaskCompletionSource<List<Dictionary<string, object>>>();

            _achievementsClient.Call("load_achievements", forceReload);

            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(15));
            var completedTask = await Task.WhenAny(_pendingLoad.Task, timeoutTask);

            if (completedTask == timeoutTask)
            {
                _pendingLoad = null;
                this.LogWarning("Load achievements timed out");
                return new List<Dictionary<string, object>>();
            }

            return await _pendingLoad.Task;
        }
        catch (Exception ex)
        {
            this.LogError($"Failed to load achievements: {ex.Message}");
            return new List<Dictionary<string, object>>();
        }
    }

    public async Task<bool> RevealAchievementAsync(string achievementId)
    {
        if (!IsAvailable)
            return false;

        var achievement = _database.GetById(achievementId);
        if (achievement == null)
            return false;

        var googlePlayId = achievement.GooglePlayId;
        if (string.IsNullOrEmpty(googlePlayId))
            return false;

        try
        {
            var taskResult = new TaskCompletionSource<bool>();
            _pendingReveals[googlePlayId] = taskResult;

            _achievementsClient!.Call("reveal_achievement", googlePlayId);

            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(10));
            var completedTask = await Task.WhenAny(taskResult.Task, timeoutTask);

            if (completedTask == timeoutTask)
            {
                _pendingReveals.Remove(googlePlayId);
                return false;
            }

            return await taskResult.Task;
        }
        catch (Exception ex)
        {
            _pendingReveals.Remove(googlePlayId);
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
