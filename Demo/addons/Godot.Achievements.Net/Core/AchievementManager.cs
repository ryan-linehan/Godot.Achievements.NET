using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot.Achievements.Providers;
using Godot.Achievements.Providers.Local;
using Godot.Achievements.Providers.Steamworks;
using Godot.Achievements.Providers.GooglePlay;
using Godot.Achievements.Providers.GameCenter;

namespace Godot.Achievements.Core;

/// <summary>
/// Main achievement manager singleton - the primary API for achievement operations
/// Automatically registered as an autoload in project settings
/// </summary>
public partial class AchievementManager : Node
{
    public static AchievementManager? Instance { get; private set; }

    /// <summary>
    /// The achievement database. Loaded automatically from project settings, or can be set at runtime using SetDatabase().
    /// </summary>
    [Export] public AchievementDatabase? Database { get; private set; }
    [Export] public float SyncRetryInterval { get; set; } = 30f; // seconds

    /// <summary>
    /// Maximum number of retry attempts before abandoning a sync. 0 = infinite retries.
    /// </summary>
    public int MaxRetryCount { get; private set; } = AchievementSettings.DefaultSyncMaxRetryCount;

    private LocalAchievementProvider? _localProvider;
    private readonly List<IAchievementProvider> _platformProviders = new();
    private readonly Queue<PendingSync> _syncQueue = new();
    private double _timeSinceLastRetry = 0;

    // Signals
    [Signal] public delegate void AchievementUnlockedEventHandler(string achievementId, Achievement achievement);
    [Signal] public delegate void AchievementProgressChangedEventHandler(string achievementId, int currentProgress, int maxProgress);
    [Signal] public delegate void ProviderRegisteredEventHandler(string providerName);
    [Signal] public delegate void ProviderUnregisteredEventHandler(string providerName);
    [Signal] public delegate void DatabaseChangedEventHandler(AchievementDatabase database);
    [Signal] public delegate void SyncAbandonedEventHandler(string achievementId, string providerName, string syncType, int retryCount);

    public override void _EnterTree()
    {
        if (Instance != null)
        {
            AchievementLogger.Warning(AchievementLogger.Areas.Core, "Multiple AchievementManager instances detected. Using first instance.");
            QueueFree();
            return;
        }

        Instance = this;
    }

    public override void _Ready()
    {
        // Load sync settings
        LoadSyncSettings();

        // If no database was set via Export, load from project settings or use default
        if (Database == null)
        {
            Database = LoadDatabaseFromSettings();
        }

        if (Database == null)
        {
            AchievementLogger.Error(AchievementLogger.Areas.Database, "No AchievementDatabase found! Set one via the editor or call SetDatabase() at runtime.");
            return;
        }

        InitializeWithDatabase();
        InitializePlatformProviders();
    }

    /// <summary>
    /// Load sync-related settings from project settings
    /// </summary>
    private void LoadSyncSettings()
    {
        if (ProjectSettings.HasSetting(AchievementSettings.SyncMaxRetryCount))
        {
            MaxRetryCount = ProjectSettings.GetSetting(AchievementSettings.SyncMaxRetryCount).AsInt32();
        }
    }

    /// <summary>
    /// Load the database from project settings, falling back to the default path
    /// </summary>
    private AchievementDatabase? LoadDatabaseFromSettings()
    {
        var path = AchievementSettings.DefaultDatabasePath;

        if (ProjectSettings.HasSetting(AchievementSettings.DatabasePath))
        {
            var settingPath = ProjectSettings.GetSetting(AchievementSettings.DatabasePath).AsString();
            if (!string.IsNullOrEmpty(settingPath))
            {
                path = settingPath;
            }
        }

        if (!ResourceLoader.Exists(path))
        {
            AchievementLogger.Warning(AchievementLogger.Areas.Database, $"Database not found at: {path}");
            return null;
        }

        var database = GD.Load<AchievementDatabase>(path);
        if (database != null)
        {
            AchievementLogger.Log(AchievementLogger.Areas.Database, $"Loaded database from: {path}");
        }

        return database;
    }

    /// <summary>
    /// Initialize the manager with the current database
    /// </summary>
    private bool InitializeWithDatabase()
    {
        if (Database == null)
        {
            return false;
        }

        // Duplicate the database so runtime changes don't affect the original resource
        // This prevents Godot from prompting to reload scenes when achievements are unlocked
        Database = (AchievementDatabase)Database.Duplicate(true);

        // Validate database
        var errors = Database.Validate();
        if (errors.Length > 0)
        {
            AchievementLogger.Error(AchievementLogger.Areas.Database, "Database validation failed:");
            foreach (var error in errors)
            {
                GD.PushError($"  - {error}");
            }
            return false;
        }

        // Initialize local provider
        _localProvider = new LocalAchievementProvider(Database);
        AchievementLogger.Log(AchievementLogger.Areas.Core, "Initialized LocalAchievementProvider");

        // Sync local achievements to platforms on startup
        CallDeferred(nameof(SyncLocalToPlatforms));

        return true;
    }

    /// <summary>
    /// Initialize platform providers based on project settings
    /// </summary>
    private void InitializePlatformProviders()
    {
        if (Database == null) return;

        if (SteamAchievementProvider.IsPlatformSupported && GetPlatformSetting(AchievementSettings.SteamEnabled))
        {
            var steamProvider = new SteamAchievementProvider(Database);
            RegisterProvider(steamProvider);
            AchievementLogger.Log(AchievementLogger.Areas.Sync, "Steam provider initialized from settings");
        }

        if (GameCenterAchievementProvider.IsPlatformSupported && GetPlatformSetting(AchievementSettings.GameCenterEnabled))
        {
            var gameCenterProvider = new GameCenterAchievementProvider(Database);
            RegisterProvider(gameCenterProvider);
            AchievementLogger.Log(AchievementLogger.Areas.Sync, "Game Center provider initialized from settings");
        }

        if (GooglePlayAchievementProvider.IsPlatformSupported && GetPlatformSetting(AchievementSettings.GooglePlayEnabled))
        {
            var googlePlayProvider = new GooglePlayAchievementProvider(Database);
            RegisterProvider(googlePlayProvider);
            AchievementLogger.Log(AchievementLogger.Areas.Sync, "Google Play provider initialized from settings");
        }
    }

    private static bool GetPlatformSetting(string settingKey)
    {
        if (ProjectSettings.HasSetting(settingKey))
        {
            return ProjectSettings.GetSetting(settingKey).AsBool();
        }
        return false;
    }

    /// <summary>
    /// Set or swap the achievement database at runtime.
    /// This reinitializes the local provider and syncs to platforms.
    /// </summary>
    /// <param name="database">The new database to use</param>
    /// <returns>True if the database was set successfully</returns>
    public bool SetDatabase(AchievementDatabase database)
    {
        if (database == null)
        {
            AchievementLogger.Error(AchievementLogger.Areas.Database, "Cannot set null database");
            return false;
        }

        Database = database;

        if (!InitializeWithDatabase())
        {
            return false;
        }

        EmitSignal(SignalName.DatabaseChanged, database);
        AchievementLogger.Log(AchievementLogger.Areas.Database, "Database changed at runtime");

        return true;
    }

    public override void _Process(double delta)
    {
        // Process sync retry queue
        if (_syncQueue.Count > 0)
        {
            _timeSinceLastRetry += delta;

            if (_timeSinceLastRetry >= SyncRetryInterval)
            {
                _timeSinceLastRetry = 0;
                ProcessSyncQueue();
            }
        }
    }

    public override void _ExitTree()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// Register a platform-specific achievement provider (Steam, Game Center, etc.)
    /// Called automatically by platform autoload nodes
    /// </summary>
    public void RegisterProvider(IAchievementProvider provider)
    {
        if (_platformProviders.Any(p => p.ProviderName == provider.ProviderName))
        {
            AchievementLogger.Warning(AchievementLogger.Areas.Core, $"Provider '{provider.ProviderName}' is already registered");
            return;
        }

        _platformProviders.Add(provider);
        AchievementLogger.Log(AchievementLogger.Areas.Sync, $"Registered provider: {provider.ProviderName} (Available: {provider.IsAvailable})");

        EmitSignal(SignalName.ProviderRegistered, provider.ProviderName);

        // Sync existing unlocked achievements to new provider
        if (provider.IsAvailable)
        {
            CallDeferred(nameof(SyncLocalToPlatforms));
        }
    }

    /// <summary>
    /// Unregister a platform-specific achievement provider
    /// Called automatically by platform autoload nodes when removed
    /// </summary>
    public void UnregisterProvider(IAchievementProvider provider)
    {
        if (provider == null) return;

        if (_platformProviders.Remove(provider))
        {
            AchievementLogger.Log(AchievementLogger.Areas.Sync, $"Unregistered provider: {provider.ProviderName}");
            EmitSignal(SignalName.ProviderUnregistered, provider.ProviderName);
        }
    }

    /// <summary>
    /// Unregister a provider by name
    /// </summary>
    public void UnregisterProvider(string providerName)
    {
        var provider = _platformProviders.FirstOrDefault(p => p.ProviderName == providerName);
        if (provider != null)
        {
            UnregisterProvider(provider);
        }
    }

    /// <summary>
    /// Unlock an achievement (saves locally and syncs to all platforms)
    /// </summary>
    public async Task Unlock(string achievementId)
    {
        if (_localProvider == null)
        {
            AchievementLogger.Error(AchievementLogger.Areas.Core, "LocalProvider not initialized");
            return;
        }

        // Unlock locally first (source of truth)
        var localResult = await _localProvider.UnlockAchievement(achievementId);
        if (!localResult.Success)
        {
            AchievementLogger.Error(AchievementLogger.Areas.Core, $"Failed to unlock '{achievementId}' locally: {localResult.Error}");
            return;
        }

        var achievement = Database?.GetById(achievementId);

        // Suppress signals and UI notifications for achievements that were already unlocked
        // This prevents duplicate toasts when syncing across platforms or reloading state
        if (localResult.WasAlreadyUnlocked)
        {
            return;
        }

        // Emit signal
        if (achievement != null)
        {
            EmitSignal(SignalName.AchievementUnlocked, achievementId, achievement);
        }

        // Sync to platform providers
        await SyncAchievementToPlatforms(achievementId);
    }

    /// <summary>
    /// Set progress for a progressive achievement
    /// </summary>
    public async Task SetProgress(string achievementId, int currentProgress)
    {
        if (_localProvider == null)
        {
            AchievementLogger.Error(AchievementLogger.Areas.Core, "LocalProvider not initialized");
            return;
        }

        var oldProgress = await _localProvider.GetProgress(achievementId);
        await _localProvider.SetProgress(achievementId, currentProgress);

        var achievement = Database?.GetById(achievementId);

        // Emit progress changed signal
        if (achievement != null)
        {
            EmitSignal(SignalName.AchievementProgressChanged, achievementId, currentProgress, achievement.MaxProgress);
        }

        // Check if this progress update caused the achievement to auto-unlock
        // Only emit unlock signal if progress just reached max (prevents duplicate signals)
        if (achievement != null && achievement.IsUnlocked && oldProgress < achievement.MaxProgress)
        {
            EmitSignal(SignalName.AchievementUnlocked, achievementId, achievement);
        }

        // Sync to platform providers
        await SyncProgressToPlatforms(achievementId, currentProgress);
    }

    /// <summary>
    /// Get an achievement by ID
    /// </summary>
    public Achievement? GetAchievement(string achievementId)
    {
        return Database?.GetById(achievementId);
    }

    /// <summary>
    /// Get all achievements
    /// </summary>
    public Achievement[] GetAllAchievements()
    {
        if (Database == null)
            return Array.Empty<Achievement>();

        return Database.Achievements.ToArray();
    }

    /// <summary>
    /// Get all registered providers
    /// </summary>
    public IReadOnlyList<IAchievementProvider> GetRegisteredProviders()
    {
        var all = new List<IAchievementProvider>();
        if (_localProvider != null)
        {
            all.Add(_localProvider);
        }
        all.AddRange(_platformProviders);
        return all.AsReadOnly();
    }

    /// <summary>
    /// Get a specific provider by name
    /// </summary>
    public IAchievementProvider? GetProvider(string providerName)
    {
        return GetRegisteredProviders()
            .FirstOrDefault(p => p.ProviderName.Equals(providerName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Reset a specific achievement on all providers (for testing)
    /// </summary>
    public async Task<SyncResult> ResetAchievement(string achievementId)
    {
        if (_localProvider == null)
        {
            var error = "LocalProvider not initialized";
            AchievementLogger.Error(AchievementLogger.Areas.Core, error);
            return SyncResult.FailureResult(error);
        }

        // Reset locally first
        var localResult = await _localProvider.ResetAchievement(achievementId);
        if (!localResult)
        {
            AchievementLogger.Warning(AchievementLogger.Areas.Core, $"Failed to reset '{achievementId}' locally: {localResult.Message}");
            return localResult;
        }

        // Reset on all platform providers
        var tasks = new List<Task<SyncResult>>();
        foreach (var provider in _platformProviders)
        {
            if (provider.IsAvailable)
            {
                tasks.Add(provider.ResetAchievement(achievementId));
            }
        }

        await Task.WhenAll(tasks);

        AchievementLogger.Log(AchievementLogger.Areas.Core, $"Reset achievement: {achievementId}");
        return SyncResult.SuccessResult();
    }

    /// <summary>
    /// Reset all achievements on all providers (for testing)
    /// </summary>
    public async Task<SyncResult> ResetAllAchievements()
    {
        if (_localProvider == null)
        {
            var error = "LocalProvider not initialized";
            AchievementLogger.Error(AchievementLogger.Areas.Core, error);
            return SyncResult.FailureResult(error);
        }

        // Reset locally first
        var localResult = await _localProvider.ResetAllAchievements();
        if (!localResult)
        {
            AchievementLogger.Warning(AchievementLogger.Areas.Core, $"Failed to reset all achievements locally: {localResult.Message}");
            return localResult;
        }

        // Reset on all platform providers
        var tasks = new List<Task<SyncResult>>();
        foreach (var provider in _platformProviders)
        {
            if (provider.IsAvailable)
            {
                tasks.Add(provider.ResetAllAchievements());
            }
        }

        await Task.WhenAll(tasks);

        AchievementLogger.Log(AchievementLogger.Areas.Core, "Reset all achievements");
        return SyncResult.SuccessResult();
    }

    /// <summary>
    /// Sync a single achievement to all platform providers
    /// </summary>
    private async Task SyncAchievementToPlatforms(string achievementId)
    {
        var tasks = new List<Task>();

        foreach (var provider in _platformProviders)
        {
            if (!provider.IsAvailable)
                continue;

            tasks.Add(SyncAchievementToProvider(achievementId, provider));
        }

        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Sync progress to all platform providers
    /// </summary>
    private async Task SyncProgressToPlatforms(string achievementId, int currentProgress)
    {
        var tasks = new List<Task>();

        foreach (var provider in _platformProviders)
        {
            if (!provider.IsAvailable)
                continue;

            tasks.Add(SyncProgressToProvider(achievementId, currentProgress, provider));
        }

        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Sync achievement unlock to a specific provider with retry on failure
    /// </summary>
    private async Task SyncAchievementToProvider(string achievementId, IAchievementProvider provider)
    {
        try
        {
            var result = await provider.UnlockAchievement(achievementId);
            if (!result.Success && !result.WasAlreadyUnlocked)
            {
                AchievementLogger.Warning(AchievementLogger.Areas.Sync, $"Failed to sync '{achievementId}' to {provider.ProviderName}: {result.Error}");
                QueueSync(new PendingSync
                {
                    AchievementId = achievementId,
                    Provider = provider,
                    Type = SyncType.Unlock
                });
            }
        }
        catch (Exception ex)
        {
            AchievementLogger.Error(AchievementLogger.Areas.Sync, $"Exception syncing '{achievementId}' to {provider.ProviderName}: {ex.Message}");
            QueueSync(new PendingSync
            {
                AchievementId = achievementId,
                Provider = provider,
                Type = SyncType.Unlock
            });
        }
    }

    /// <summary>
    /// Sync progress to a specific provider with retry on failure
    /// </summary>
    private async Task SyncProgressToProvider(string achievementId, int currentProgress, IAchievementProvider provider)
    {
        try
        {
            var result = await provider.SetProgress(achievementId, currentProgress);
            if (!result)
            {
                AchievementLogger.Warning(AchievementLogger.Areas.Sync, $"Failed to sync progress for '{achievementId}' to {provider.ProviderName}: {result.Message}");
                QueueSync(new PendingSync
                {
                    AchievementId = achievementId,
                    Provider = provider,
                    Type = SyncType.Progress,
                    CurrentProgress = currentProgress
                });
            }
        }
        catch (Exception ex)
        {
            AchievementLogger.Error(AchievementLogger.Areas.Sync, $"Exception syncing progress for '{achievementId}' to {provider.ProviderName}: {ex.Message}");
            QueueSync(new PendingSync
            {
                AchievementId = achievementId,
                Provider = provider,
                Type = SyncType.Progress,
                CurrentProgress = currentProgress
            });
        }
    }

    /// <summary>
    /// Sync all locally unlocked achievements to all platforms (called on startup)
    /// </summary>
    private async void SyncLocalToPlatforms()
    {
        if (Database == null || _platformProviders.Count == 0)
            return;

        var unlockedAchievements = Database.Achievements.Where(a => a.IsUnlocked).ToArray();

        if (unlockedAchievements.Length == 0)
            return;

        AchievementLogger.Log(AchievementLogger.Areas.Sync, $"Syncing {unlockedAchievements.Length} unlocked achievements to {_platformProviders.Count} platform(s)");

        foreach (var achievement in unlockedAchievements)
        {
            await SyncAchievementToPlatforms(achievement.Id);

            if (achievement.CurrentProgress > 0 && achievement.CurrentProgress < achievement.MaxProgress)
            {
                await SyncProgressToPlatforms(achievement.Id, achievement.CurrentProgress);
            }
        }
    }

    /// <summary>
    /// Add a failed sync to the retry queue
    /// Prevents duplicate queue entries for the same achievement+provider+action combination
    /// </summary>
    private void QueueSync(PendingSync sync)
    {
        // Prevent queue bloat by deduplicating identical pending syncs
        // This avoids repeatedly retrying the same operation multiple times
        if (_syncQueue.Any(s => s.AchievementId == sync.AchievementId && s.Provider == sync.Provider && s.Type == sync.Type))
            return;

        _syncQueue.Enqueue(sync);
        AchievementLogger.Log(AchievementLogger.Areas.Sync, $"Queued {sync.Type} sync for '{sync.AchievementId}' to {sync.Provider.ProviderName} (queue size: {_syncQueue.Count})");
    }

    /// <summary>
    /// Process pending syncs in the retry queue
    /// Uses a batch processing strategy to avoid modifying the queue while iterating
    /// Failed syncs are automatically re-queued for retry on the next interval
    /// </summary>
    private async void ProcessSyncQueue()
    {
        if (_syncQueue.Count == 0)
            return;

        AchievementLogger.Log(AchievementLogger.Areas.Sync, $"Processing {_syncQueue.Count} pending syncs...");

        var successCount = 0;
        var failCount = 0;
        var abandonedCount = 0;
        var batch = new List<PendingSync>();

        // Dequeue all items into a batch to avoid collection modification during iteration
        // This allows us to safely re-queue failures without concurrent modification issues
        while (_syncQueue.Count > 0)
        {
            batch.Add(_syncQueue.Dequeue());
        }

        foreach (var sync in batch)
        {
            // Skip unavailable providers (e.g., Steam not running) and re-queue for later
            // Don't count this as a retry attempt since we didn't actually try
            if (!sync.Provider.IsAvailable)
            {
                _syncQueue.Enqueue(sync);
                continue;
            }

            var succeeded = false;

            try
            {
                if (sync.Type == SyncType.Unlock)
                {
                    var result = await sync.Provider.UnlockAchievement(sync.AchievementId);

                    // Consider both Success and WasAlreadyUnlocked as successful syncs
                    succeeded = result.Success || result.WasAlreadyUnlocked;
                }
                else if (sync.Type == SyncType.Progress)
                {
                    var result = await sync.Provider.SetProgress(sync.AchievementId, sync.CurrentProgress);
                    succeeded = result;
                }
            }
            catch
            {
                // Exception counts as a failure
                succeeded = false;
            }

            if (succeeded)
            {
                successCount++;
            }
            else
            {
                sync.RetryCount++;
                failCount++;

                // Check if we've exceeded the max retry count (0 = infinite retries)
                if (MaxRetryCount > 0 && sync.RetryCount >= MaxRetryCount)
                {
                    abandonedCount++;
                    AchievementLogger.Warning(AchievementLogger.Areas.Sync, $"Abandoning {sync.Type} sync for '{sync.AchievementId}' to {sync.Provider.ProviderName} after {sync.RetryCount} attempts");
                    EmitSignal(SignalName.SyncAbandoned, sync.AchievementId, sync.Provider.ProviderName, sync.Type.ToString(), sync.RetryCount);
                }
                else
                {
                    // Re-queue for retry
                    _syncQueue.Enqueue(sync);
                }
            }
        }

        AchievementLogger.Log(AchievementLogger.Areas.Sync, $"Sync complete: {successCount} succeeded, {failCount} failed, {abandonedCount} abandoned (queue size: {_syncQueue.Count})");
    }

}
