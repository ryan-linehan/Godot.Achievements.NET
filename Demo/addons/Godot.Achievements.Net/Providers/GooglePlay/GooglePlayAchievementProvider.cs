#if GODOT_ANDROID
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot.Achievements.Core;
using Godot.Achievements.Providers;

namespace Godot.Achievements.Providers.GooglePlay;

/// <summary>
/// Google Play Games achievement provider for Android.
/// Integrates with the GodotPlayGameServices addon:
/// https://github.com/godot-sdk-integrations/godot-play-game-services
///
/// Requirements:
/// 1. Install GodotPlayGameServices addon in your project
/// 2. Enable the GodotPlayGameServices autoload in Project Settings
/// 3. Configure Google Play Console with your game and achievements
/// 4. Set up OAuth credentials matching your package name and SHA-1 certificate
/// 5. Enable Google Play provider in project settings
/// </summary>
public class GooglePlayAchievementProvider : IAchievementProvider
{
    public static bool IsPlatformSupported => true;

    private readonly AchievementDatabase _database;
    private Node? _godotPlayGameServices;
    private Node? _signInClient;
    private Node? _achievementsClient;
    private bool _isInitialized;
    private bool _isAuthenticated;

    // Track pending callbacks for async operations
    private readonly Dictionary<string, TaskCompletionSource<bool>> _pendingUnlocks = new();
    private readonly Dictionary<string, TaskCompletionSource<bool>> _pendingReveals = new();
    private TaskCompletionSource<List<Dictionary<string, object>>>? _pendingLoad;

    public string ProviderName => ProviderNames.GooglePlay;

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

            // Get the GodotPlayGameServices autoload
            _godotPlayGameServices = sceneTree.Root.GetNodeOrNull("GodotPlayGameServices") as Node;
            if (_godotPlayGameServices == null)
            {
                this.LogWarning("GodotPlayGameServices autoload not found. Make sure it's enabled in Project Settings > Autoload.");
                _isInitialized = false;
                return;
            }

            // Initialize the plugin
            var initResult = _godotPlayGameServices.Call("initialize");
            if (initResult.AsInt32() != 0) // 0 = OK (PlayGamesPluginError.OK)
            {
                this.LogError("Failed to initialize Google Play Games Services plugin. The Android plugin may not be available.");
                _isInitialized = false;
                return;
            }

            this.Log("GodotPlayGameServices plugin initialized");

            // Instance the client nodes into the scene tree
            InstanceClientNodes(sceneTree.Root);

            if (_signInClient == null || _achievementsClient == null)
            {
                this.LogError("Failed to create client nodes");
                _isInitialized = false;
                return;
            }

            // Connect to signals
            ConnectSignals();

            // Connect to tree exiting for cleanup
            if (!sceneTree.IsConnected("tree_exiting", Callable.From(Cleanup)))
            {
                sceneTree.Connect("tree_exiting", Callable.From(Cleanup));
            }

            // Check initial authentication status
            CheckAuthentication();

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
            // Load and instance SignInClient
            var signInScript = GD.Load<Script>("res://addons/GodotPlayGameServices/scripts/sign_in/sign_in_client.gd");
            if (signInScript != null)
            {
                _signInClient = new Node();
                _signInClient.SetScript(signInScript);
                _signInClient.Name = "GooglePlaySignInClient";
                root.AddChild(_signInClient);
                this.Log("SignInClient node created");
            }
            else
            {
                this.LogWarning("Could not load sign_in_client.gd script");
            }

            // Load and instance AchievementsClient
            var achievementsScript = GD.Load<Script>("res://addons/GodotPlayGameServices/scripts/achievements/achievements_client.gd");
            if (achievementsScript != null)
            {
                _achievementsClient = new Node();
                _achievementsClient.SetScript(achievementsScript);
                _achievementsClient.Name = "GooglePlayAchievementsClient";
                root.AddChild(_achievementsClient);
                this.Log("AchievementsClient node created");
            }
            else
            {
                this.LogWarning("Could not load achievements_client.gd script");
            }
        }
        catch (Exception ex)
        {
            this.LogError($"Failed to instance client nodes: {ex.Message}");
        }
    }

    private void ConnectSignals()
    {
        if (_signInClient != null)
        {
            // Connect to user_authenticated signal
            if (!_signInClient.IsConnected("user_authenticated", Callable.From<bool>(OnUserAuthenticated)))
            {
                _signInClient.Connect("user_authenticated", Callable.From<bool>(OnUserAuthenticated));
            }
        }

        if (_achievementsClient != null)
        {
            // Connect to achievement signals
            if (!_achievementsClient.IsConnected("achievement_unlocked", Callable.From<bool, string>(OnAchievementUnlocked)))
            {
                _achievementsClient.Connect("achievement_unlocked", Callable.From<bool, string>(OnAchievementUnlocked));
            }

            if (!_achievementsClient.IsConnected("achievement_revealed", Callable.From<bool, string>(OnAchievementRevealed)))
            {
                _achievementsClient.Connect("achievement_revealed", Callable.From<bool, string>(OnAchievementRevealed));
            }

            // The signal emits Array[PlayGamesAchievement] which becomes Godot.Collections.Array in C#
            if (!_achievementsClient.IsConnected("achievements_loaded", Callable.From<Godot.Collections.Array>(OnAchievementsLoaded)))
            {
                _achievementsClient.Connect("achievements_loaded", Callable.From<Godot.Collections.Array>(OnAchievementsLoaded));
            }
        }
    }

    private void CheckAuthentication()
    {
        if (_signInClient == null) return;

        // The is_authenticated method triggers a check and emits user_authenticated signal
        _signInClient.Call("is_authenticated");
    }

    private void OnUserAuthenticated(bool isAuthenticated)
    {
        _isAuthenticated = isAuthenticated;
        this.Log($"Authentication status changed: {isAuthenticated}");
    }

    private void OnAchievementUnlocked(bool isUnlocked, string achievementId)
    {
        if (_pendingUnlocks.TryGetValue(achievementId, out var tcs))
        {
            tcs.TrySetResult(isUnlocked);
            _pendingUnlocks.Remove(achievementId);
        }
    }

    private void OnAchievementRevealed(bool isRevealed, string achievementId)
    {
        if (_pendingReveals.TryGetValue(achievementId, out var tcs))
        {
            tcs.TrySetResult(isRevealed);
            _pendingReveals.Remove(achievementId);
        }
    }

    private void OnAchievementsLoaded(Godot.Collections.Array achievements)
    {
        if (_pendingLoad == null) return;

        var result = new List<Dictionary<string, object>>();
        foreach (var item in achievements)
        {
            // Each item is a PlayGamesAchievement object (RefCounted with properties)
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

    public bool IsAvailable => _isInitialized && _isAuthenticated && _achievementsClient != null;

    public async Task<AchievementUnlockResult> UnlockAchievement(string achievementId)
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
            // Create a TaskCompletionSource for the async callback
            var tcs = new TaskCompletionSource<bool>();
            _pendingUnlocks[googlePlayId] = tcs;

            // Call unlock_achievement on the GDScript client
            _achievementsClient.Call("unlock_achievement", googlePlayId);

            // Wait for the callback with a timeout
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(10));
            var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

            if (completedTask == timeoutTask)
            {
                _pendingUnlocks.Remove(googlePlayId);
                return AchievementUnlockResult.FailureResult("Unlock operation timed out");
            }

            var success = await tcs.Task;
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

    public async Task<int> GetProgress(string achievementId)
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
            // Load achievements from Google Play to get current progress
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

    public async Task<SyncResult> SetProgress(string achievementId, int currentProgress)
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

        try
        {
            // For incremental achievements, we use increment_achievement
            // Google Play uses absolute step count, so we need to calculate the increment
            int existingProgress = await GetProgress(achievementId);
            int increment = currentProgress - existingProgress;

            if (increment > 0)
            {
                // Create a TaskCompletionSource for the async callback
                var tcs = new TaskCompletionSource<bool>();
                _pendingUnlocks[googlePlayId] = tcs; // increment uses same signal as unlock

                // Call increment_achievement on the GDScript client
                _achievementsClient.Call("increment_achievement", googlePlayId, increment);

                // Wait for the callback with a timeout
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(10));
                var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    _pendingUnlocks.Remove(googlePlayId);
                    this.LogWarning($"Increment operation timed out for {googlePlayId}");
                    // Don't fail - the operation may have succeeded
                }
                else
                {
                    var success = await tcs.Task;
                    this.Log($"Incremented achievement: {googlePlayId} by {increment} (success: {success})");
                }
            }
            else if (increment == 0)
            {
                this.Log($"No progress change needed for {googlePlayId}");
            }
            else
            {
                // Negative increment - Google Play doesn't support decreasing progress
                this.LogWarning($"Cannot decrease progress for {googlePlayId} (requested: {currentProgress}, existing: {existingProgress})");
            }

            return SyncResult.SuccessResult();
        }
        catch (Exception ex)
        {
            _pendingUnlocks.Remove(googlePlayId);
            return SyncResult.FailureResult($"Exception setting progress: {ex.Message}");
        }
    }

    public async Task<SyncResult> ResetAchievement(string achievementId)
    {
        // Google Play Games does not support resetting achievements in production
        // This is only available for testing via the Play Games Console
        this.LogWarning($"ResetAchievement is not supported on Google Play Games (achievement: {achievementId})");
        await Task.CompletedTask;
        return SyncResult.FailureResult("Google Play Games does not support resetting achievements. Use the Play Games Console for testing.");
    }

    public async Task<SyncResult> ResetAllAchievements()
    {
        // Google Play Games does not support resetting achievements in production
        this.LogWarning("ResetAllAchievements is not supported on Google Play Games");
        await Task.CompletedTask;
        return SyncResult.FailureResult("Google Play Games does not support resetting achievements. Use the Play Games Console for testing.");
    }

    /// <summary>
    /// Load all achievements from Google Play Games.
    /// Returns achievement data including id, name, state, type, and progress.
    /// </summary>
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

    /// <summary>
    /// Reveal a hidden achievement. Call this before unlock if the achievement is hidden.
    /// </summary>
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
            var tcs = new TaskCompletionSource<bool>();
            _pendingReveals[googlePlayId] = tcs;

            _achievementsClient!.Call("reveal_achievement", googlePlayId);

            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(10));
            var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

            if (completedTask == timeoutTask)
            {
                _pendingReveals.Remove(googlePlayId);
                return false;
            }

            return await tcs.Task;
        }
        catch (Exception ex)
        {
            _pendingReveals.Remove(googlePlayId);
            this.LogError($"Failed to reveal achievement: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Show the Google Play Games achievements UI.
    /// </summary>
    public void ShowAchievementsUI()
    {
        if (!IsAvailable)
        {
            this.LogWarning("Cannot show achievements UI - provider not available");
            return;
        }

        _achievementsClient!.Call("show_achievements");
    }

    /// <summary>
    /// Request user sign-in to Google Play Games.
    /// </summary>
    public void SignIn()
    {
        if (_signInClient == null)
        {
            this.LogWarning("SignInClient not available");
            return;
        }

        _signInClient.Call("sign_in");
    }

    /// <summary>
    /// Check if the user is currently authenticated.
    /// </summary>
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

        _isInitialized = false;
        _isAuthenticated = false;
    }
}
#endif
