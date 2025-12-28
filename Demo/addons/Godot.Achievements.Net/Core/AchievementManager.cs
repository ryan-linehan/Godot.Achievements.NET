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
/// Main achievement manager singleton - the primary API for achievement operations.
/// Automatically registered as an autoload in project settings.
///
/// Sync methods are preferred for gameplay code (no frame blocking).
/// Async methods are available when you need to wait for the operation to complete.
/// </summary>
public partial class AchievementManager : Node
{
    public static AchievementManager? Instance { get; private set; }

    [Export] public AchievementDatabase? Database { get; private set; }

    private LocalAchievementProvider? _localProvider;
    private readonly List<IAchievementProvider> _platformProviders = new();

    // Signals
    [Signal] public delegate void AchievementUnlockedEventHandler(string achievementId, Achievement achievement);
    [Signal] public delegate void AchievementProgressChangedEventHandler(string achievementId, int currentProgress, int maxProgress);
    [Signal] public delegate void ProviderRegisteredEventHandler(string providerName);
    [Signal] public delegate void ProviderUnregisteredEventHandler(string providerName);
    [Signal] public delegate void DatabaseChangedEventHandler(AchievementDatabase database);

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
        if (Database == null)
        {
            Database = LoadDatabaseFromSettings();
        }

        if (Database == null)
        {
            AchievementLogger.Error(AchievementLogger.Areas.Database, "No AchievementDatabase found!");
            return;
        }

        InitializeWithDatabase();
        // Defer platform provider initialization to ensure other autoloads (Steam, GameCenter, etc.) are ready
        CallDeferred(nameof(InitializePlatformProviders));
    }

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

    private bool InitializeWithDatabase()
    {
        if (Database == null) return false;

        Database = (AchievementDatabase)Database.Duplicate(true);

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

        _localProvider = new LocalAchievementProvider(Database);
        AchievementLogger.Log(AchievementLogger.Areas.Core, "Initialized LocalAchievementProvider");

        CallDeferred(nameof(SyncLocalToPlatforms));

        return true;
    }

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

    public override void _ExitTree()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

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

        if (provider.IsAvailable)
        {
            CallDeferred(nameof(SyncLocalToPlatforms));
        }
    }

    public void UnregisterProvider(IAchievementProvider provider)
    {
        if (provider == null) return;

        if (_platformProviders.Remove(provider))
        {
            AchievementLogger.Log(AchievementLogger.Areas.Sync, $"Unregistered provider: {provider.ProviderName}");
            EmitSignal(SignalName.ProviderUnregistered, provider.ProviderName);
        }
    }

    public void UnregisterProvider(string providerName)
    {
        var provider = _platformProviders.FirstOrDefault(p => p.ProviderName == providerName);
        if (provider != null)
        {
            UnregisterProvider(provider);
        }
    }

    #region Sync Methods
    /// <summary>
    /// Unlock an achievement (sync). Saves locally and syncs to all platforms.
    /// </summary>
    public void Unlock(string achievementId)
    {
        if (_localProvider == null)
        {
            AchievementLogger.Error(AchievementLogger.Areas.Core, "LocalProvider not initialized");
            return;
        }

        var achievement = Database?.GetById(achievementId);
        if (achievement == null)
        {
            AchievementLogger.Error(AchievementLogger.Areas.Core, $"Achievement '{achievementId}' not found in database");
            return;
        }

        // Check if already unlocked before calling
        bool wasAlreadyUnlocked = achievement.IsUnlocked;

        // Unlock locally first (source of truth) - fire-and-forget
        _localProvider.UnlockAchievement(achievementId);

        // Suppress signals for already unlocked achievements
        if (wasAlreadyUnlocked)
        {
            return;
        }

        // Emit signal (LocalProvider updates the achievement object synchronously)
        if (achievement.IsUnlocked)
        {
            EmitSignal(SignalName.AchievementUnlocked, achievementId, achievement);
        }

        // Sync to platform providers (fire-and-forget)
        SyncAchievementToPlatforms(achievementId);
    }

    /// <summary>
    /// Increment progress for a progressive achievement (sync).
    /// </summary>
    public void IncrementProgress(string achievementId, int amount = 1)
    {
        if (_localProvider == null)
        {
            AchievementLogger.Error(AchievementLogger.Areas.Core, "LocalProvider not initialized");
            return;
        }

        var achievement = Database?.GetById(achievementId);
        if (achievement == null)
        {
            AchievementLogger.Error(AchievementLogger.Areas.Core, $"Achievement '{achievementId}' not found in database");
            return;
        }

        bool wasUnlocked = achievement.IsUnlocked;

        // Increment progress locally (fire-and-forget)
        _localProvider.IncrementProgress(achievementId, amount);

        // Emit progress changed signal
        EmitSignal(SignalName.AchievementProgressChanged, achievementId, achievement.CurrentProgress, achievement.MaxProgress);

        // Check if this progress update caused the achievement to auto-unlock
        if (achievement.IsUnlocked && !wasUnlocked)
        {
            EmitSignal(SignalName.AchievementUnlocked, achievementId, achievement);
        }

        // Sync to platform providers (fire-and-forget)
        SyncIncrementToPlatforms(achievementId, amount);
    }

    /// <summary>
    /// Reset a specific achievement on all providers (sync, for testing).
    /// </summary>
    public void ResetAchievement(string achievementId)
    {
        if (_localProvider == null)
        {
            AchievementLogger.Error(AchievementLogger.Areas.Core, "LocalProvider not initialized");
            return;
        }

        // Reset locally first (fire-and-forget)
        _localProvider.ResetAchievement(achievementId);

        // Reset on all platform providers (fire-and-forget)
        foreach (var provider in _platformProviders)
        {
            if (provider.IsAvailable)
            {
                provider.ResetAchievement(achievementId);
            }
        }

        AchievementLogger.Log(AchievementLogger.Areas.Core, $"Reset achievement: {achievementId}");
    }

    /// <summary>
    /// Reset all achievements on all providers (sync, for testing).
    /// </summary>
    public void ResetAllAchievements()
    {
        if (_localProvider == null)
        {
            AchievementLogger.Error(AchievementLogger.Areas.Core, "LocalProvider not initialized");
            return;
        }

        // Reset locally first (fire-and-forget)
        _localProvider.ResetAllAchievements();

        // Reset on all platform providers (fire-and-forget)
        foreach (var provider in _platformProviders)
        {
            if (provider.IsAvailable)
            {
                provider.ResetAllAchievements();
            }
        }

        AchievementLogger.Log(AchievementLogger.Areas.Core, "Reset all achievements");
    }
    #endregion
    #region Async Methods
    /// <summary>
    /// Unlock an achievement (async). Saves locally and syncs to all platforms.
    /// </summary>
    public Task UnlockAsync(string achievementId)
    {
        Unlock(achievementId);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Increment progress for a progressive achievement (async).
    /// </summary>
    public Task IncrementProgressAsync(string achievementId, int amount = 1)
    {
        IncrementProgress(achievementId, amount);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Reset a specific achievement on all providers (async, for testing).
    /// </summary>
    public Task ResetAchievementAsync(string achievementId)
    {
        ResetAchievement(achievementId);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Reset all achievements on all providers (async, for testing).
    /// </summary>
    public Task ResetAllAchievementsAsync()
    {
        ResetAllAchievements();
        return Task.CompletedTask;
    }
    #endregion
    #region Query Methods

    public Achievement? GetAchievement(string achievementId)
    {
        return Database?.GetById(achievementId);
    }

    public Achievement[] GetAllAchievements()
    {
        if (Database == null)
            return Array.Empty<Achievement>();

        return Database.Achievements.ToArray();
    }

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

    public IAchievementProvider? GetProvider(string providerName)
    {
        return GetRegisteredProviders()
            .FirstOrDefault(p => p.ProviderName.Equals(providerName, StringComparison.OrdinalIgnoreCase));
    }    
    #endregion

    private void SyncAchievementToPlatforms(string achievementId)
    {
        foreach (var provider in _platformProviders)
        {
            if (!provider.IsAvailable)
                continue;

            try
            {
                provider.UnlockAchievement(achievementId);
            }
            catch (Exception ex)
            {
                AchievementLogger.Error(AchievementLogger.Areas.Sync, $"Exception syncing '{achievementId}' to {provider.ProviderName}: {ex.Message}");
            }
        }
    }

    private void SyncIncrementToPlatforms(string achievementId, int amount)
    {
        foreach (var provider in _platformProviders)
        {
            if (!provider.IsAvailable)
                continue;

            try
            {
                provider.IncrementProgress(achievementId, amount);
            }
            catch (Exception ex)
            {
                AchievementLogger.Error(AchievementLogger.Areas.Sync, $"Exception syncing progress for '{achievementId}' to {provider.ProviderName}: {ex.Message}");
            }
        }
    }

    private void SyncLocalToPlatforms()
    {
        if (Database == null || _platformProviders.Count == 0)
            return;

        var unlockedAchievements = Database.Achievements.Where(a => a.IsUnlocked).ToArray();

        if (unlockedAchievements.Length == 0)
            return;

        AchievementLogger.Log(AchievementLogger.Areas.Sync, $"Syncing {unlockedAchievements.Length} unlocked achievements to {_platformProviders.Count} platform(s)");

        foreach (var achievement in unlockedAchievements)
        {
            SyncAchievementToPlatforms(achievement.Id);
        }
    }
}
