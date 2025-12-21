using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Godot.Achievements.Core;

/// <summary>
/// Main achievement manager singleton - the primary API for achievement operations
/// Automatically registered as an autoload in project settings
/// </summary>
public partial class AchievementManager : Node
{
    public static AchievementManager? Instance { get; private set; }

    [Export] public AchievementDatabase? Database { get; set; }
    [Export] public float SyncRetryInterval { get; set; } = 30f; // seconds

    private LocalAchievementProvider? _localProvider;
    private readonly List<IAchievementProvider> _platformProviders = new();
    private readonly Queue<PendingSync> _syncQueue = new();
    private double _timeSinceLastRetry = 0;

    // Signals
    [Signal] public delegate void AchievementUnlockedEventHandler(string achievementId, Achievement achievement);
    [Signal] public delegate void AchievementProgressChangedEventHandler(string achievementId, float progress);
    [Signal] public delegate void ProviderRegisteredEventHandler(string providerName);

    public override void _EnterTree()
    {
        if (Instance != null)
        {
            GD.PushWarning("Multiple AchievementManager instances detected. Using first instance.");
            QueueFree();
            return;
        }

        Instance = this;
    }

    public override void _Ready()
    {
        if (Database == null)
        {
            GD.PushError("[Achievements] No AchievementDatabase assigned to AchievementManager!");
            return;
        }

        // Validate database
        var errors = Database.Validate();
        if (errors.Length > 0)
        {
            GD.PushError("[Achievements] Database validation failed:");
            foreach (var error in errors)
            {
                GD.PushError($"  - {error}");
            }
            return;
        }

        // Initialize local provider
        _localProvider = new LocalAchievementProvider(Database);
        GD.Print("[Achievements] Initialized LocalAchievementProvider");

        // Sync local achievements to platforms on startup
        CallDeferred(nameof(SyncLocalToPlatforms));
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
            GD.PushWarning($"[Achievements] Provider '{provider.ProviderName}' is already registered");
            return;
        }

        _platformProviders.Add(provider);
        GD.Print($"[Achievements] Registered provider: {provider.ProviderName} (Available: {provider.IsAvailable})");

        EmitSignal(SignalName.ProviderRegistered, provider.ProviderName);

        // Sync existing unlocked achievements to new provider
        if (provider.IsAvailable)
        {
            CallDeferred(nameof(SyncLocalToPlatforms));
        }
    }

    /// <summary>
    /// Unlock an achievement (saves locally and syncs to all platforms)
    /// </summary>
    public async Task Unlock(string achievementId)
    {
        if (_localProvider == null)
        {
            GD.PushError("[Achievements] LocalProvider not initialized");
            return;
        }

        // Unlock locally first (source of truth)
        var localResult = await _localProvider.UnlockAchievement(achievementId);
        if (!localResult.Success)
        {
            GD.PushError($"[Achievements] Failed to unlock '{achievementId}' locally: {localResult.Error}");
            return;
        }

        var achievement = await _localProvider.GetAchievement(achievementId);

        // Don't show toast or emit signal if already unlocked
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
    public async Task SetProgress(string achievementId, float progress)
    {
        if (_localProvider == null)
        {
            GD.PushError("[Achievements] LocalProvider not initialized");
            return;
        }

        var oldProgress = await _localProvider.GetProgress(achievementId);
        await _localProvider.SetProgress(achievementId, progress);

        var achievement = await _localProvider.GetAchievement(achievementId);

        // Emit progress changed signal
        EmitSignal(SignalName.AchievementProgressChanged, achievementId, progress);

        // Check if achievement was unlocked by progress reaching 100%
        if (achievement != null && achievement.IsUnlocked && oldProgress < 1.0f)
        {
            EmitSignal(SignalName.AchievementUnlocked, achievementId, achievement);
        }

        // Sync to platform providers
        await SyncProgressToPlatforms(achievementId, progress);
    }

    /// <summary>
    /// Get an achievement by ID
    /// </summary>
    public Achievement? GetAchievement(string achievementId)
    {
        if (_localProvider == null)
            return null;

        return _localProvider.GetAchievement(achievementId).Result;
    }

    /// <summary>
    /// Get all achievements
    /// </summary>
    public Achievement[] GetAllAchievements()
    {
        if (_localProvider == null)
            return Array.Empty<Achievement>();

        return _localProvider.GetAllAchievements().Result;
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
    private async Task SyncProgressToPlatforms(string achievementId, float progress)
    {
        var tasks = new List<Task>();

        foreach (var provider in _platformProviders)
        {
            if (!provider.IsAvailable)
                continue;

            tasks.Add(SyncProgressToProvider(achievementId, progress, provider));
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
                GD.PushWarning($"[Achievements] Failed to sync '{achievementId}' to {provider.ProviderName}: {result.Error}");
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
            GD.PushError($"[Achievements] Exception syncing '{achievementId}' to {provider.ProviderName}: {ex.Message}");
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
    private async Task SyncProgressToProvider(string achievementId, float progress, IAchievementProvider provider)
    {
        try
        {
            await provider.SetProgress(achievementId, progress);
        }
        catch (Exception ex)
        {
            GD.PushError($"[Achievements] Exception syncing progress for '{achievementId}' to {provider.ProviderName}: {ex.Message}");
            QueueSync(new PendingSync
            {
                AchievementId = achievementId,
                Provider = provider,
                Type = SyncType.Progress,
                Progress = progress
            });
        }
    }

    /// <summary>
    /// Sync all locally unlocked achievements to all platforms (called on startup)
    /// </summary>
    private async void SyncLocalToPlatforms()
    {
        if (_localProvider == null || _platformProviders.Count == 0)
            return;

        var allAchievements = await _localProvider.GetAllAchievements();
        var unlockedAchievements = allAchievements.Where(a => a.IsUnlocked).ToArray();

        if (unlockedAchievements.Length == 0)
            return;

        GD.Print($"[Achievements] Syncing {unlockedAchievements.Length} unlocked achievements to {_platformProviders.Count} platform(s)");

        foreach (var achievement in unlockedAchievements)
        {
            await SyncAchievementToPlatforms(achievement.Id);

            if (achievement.Progress > 0 && achievement.Progress < 1.0f)
            {
                await SyncProgressToPlatforms(achievement.Id, achievement.Progress);
            }
        }
    }

    /// <summary>
    /// Add a failed sync to the retry queue
    /// </summary>
    private void QueueSync(PendingSync sync)
    {
        // Don't queue duplicates
        if (_syncQueue.Any(s => s.AchievementId == sync.AchievementId && s.Provider == sync.Provider && s.Type == sync.Type))
            return;

        _syncQueue.Enqueue(sync);
        GD.Print($"[Achievements] Queued {sync.Type} sync for '{sync.AchievementId}' to {sync.Provider.ProviderName} (queue size: {_syncQueue.Count})");
    }

    /// <summary>
    /// Process pending syncs in the retry queue
    /// </summary>
    private async void ProcessSyncQueue()
    {
        if (_syncQueue.Count == 0)
            return;

        GD.Print($"[Achievements] Processing {_syncQueue.Count} pending syncs...");

        var successCount = 0;
        var failCount = 0;
        var batch = new List<PendingSync>();

        // Dequeue all current items (process as a batch)
        while (_syncQueue.Count > 0)
        {
            batch.Add(_syncQueue.Dequeue());
        }

        foreach (var sync in batch)
        {
            if (!sync.Provider.IsAvailable)
            {
                _syncQueue.Enqueue(sync); // Re-queue if provider not available
                continue;
            }

            try
            {
                if (sync.Type == SyncType.Unlock)
                {
                    var result = await sync.Provider.UnlockAchievement(sync.AchievementId);
                    if (result.Success || result.WasAlreadyUnlocked)
                    {
                        successCount++;
                    }
                    else
                    {
                        failCount++;
                        _syncQueue.Enqueue(sync); // Re-queue on failure
                    }
                }
                else if (sync.Type == SyncType.Progress)
                {
                    await sync.Provider.SetProgress(sync.AchievementId, sync.Progress);
                    successCount++;
                }
            }
            catch
            {
                failCount++;
                _syncQueue.Enqueue(sync); // Re-queue on exception
            }
        }

        GD.Print($"[Achievements] Sync complete: {successCount} succeeded, {failCount} failed (queue size: {_syncQueue.Count})");
    }

}
