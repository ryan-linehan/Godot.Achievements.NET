# Godot.Achievements.NET - Design Document

## 🎯 Overview

A C#-focused achievements plugin for Godot 4+ that provides:
- Editor-based achievement setup
- Platform-agnostic abstraction layer
- Multiple achievement system integrations
- Local-first sync strategy
- Default toast notification system
- AOT-compatible architecture

## 📦 Addons Folder Structure

### Core Plugin
**addons/godot_achievements/**
- Achievement definition models (Resources)
- `IAchievementProvider` interface
- `AchievementManager` singleton
- Local achievement provider (user:// file persistence)
- Editor plugin & dock UI
- Default toast notification system

```
addons/godot_achievements/
├── plugin.cfg
├── Achievement.cs
├── AchievementDatabase.cs
├── AchievementManager.cs
├── AchievementToast.cs
├── IAchievementProvider.cs
├── LocalAchievementProvider.cs
├── PendingSync.cs
├── SyncType.cs
└── Editor/
    ├── AchievementEditorPlugin.cs
    └── AchievementEditorDock.cs
```

### Platform Plugins (Optional)
**addons/godot_achievements_steam/**
- Steam achievement provider
- Dependencies: `Steamworks.NET` via Godot.Steamworks.NET
- Conditional compilation: `#if GODOT_PC || GODOT_WINDOWS || GODOT_LINUX || GODOT_MACOS || GODOT_X11 || GODOT_OSX`

**addons/godot_achievements_ios/**
- Game Center achievement provider
- Dependencies: Swift Godot bindings (GodotApplePlugins)
- Conditional compilation: `#if GODOT_IOS`

**addons/godot_achievements_android/**
- Google Play Games achievement provider
- Dependencies: Android-specific bindings
- Conditional compilation: `#if GODOT_ANDROID`

### Installation
Users copy the folders they need into their project's `addons/` directory. Platform-specific providers are compiled only when targeting the respective platform thanks to preprocessor directives.

## 🏗️ Architecture

### AOT-Compatible Design

**No reflection** - all platform registrations use compile-time patterns:
- Autoload node registration
- Conditional compilation symbols
- Static interface implementations
- Concrete types (no dynamic invocation)
- **Godot's `Json` class** instead of `System.Text.Json.JsonSerializer` for full AOT compatibility
- **Godot.Collections.Dictionary** instead of C# POCO classes for serialization

### Core Interface

```csharp
public interface IAchievementProvider
{
    string ProviderName { get; }
    bool IsAvailable { get; }

    Task<AchievementUnlockResult> UnlockAchievement(string achievementId);
    Task<Achievement?> GetAchievement(string achievementId);
    Task<Achievement[]> GetAllAchievements();
    Task<float> GetProgress(string achievementId);
    Task SetProgress(string achievementId, float progress);
}

public readonly struct AchievementUnlockResult
{
    public bool Success { get; init; }
    public string Error { get; init; }
    public bool WasAlreadyUnlocked { get; init; }
}
```

### Achievement Manager

```csharp
public partial class AchievementManager : Node
{
    public static AchievementManager Instance { get; private set; }

    private LocalAchievementProvider _localProvider;
    private List<IAchievementProvider> _platformProviders = new();

    // Simple API
    public async Task Unlock(string achievementId);
    public async Task SetProgress(string achievementId, float progress);
    public Achievement GetAchievement(string achievementId);
    public Achievement[] GetAllAchievements();

    // Provider registration
    public void RegisterProvider(IAchievementProvider provider);
}
```

### Platform Registration Pattern

Each platform package ships with an autoload node:

```csharp
// In Godot.Achievements.Steam package
#if GODOT_PC || GODOT_WINDOWS || GODOT_LINUX || GODOT_MACOS
public partial class SteamAchievementAutoload : Node
{
    public override void _Ready()
    {
        var manager = GetNode<AchievementManager>("/root/Achievements");
        if (SteamAPI.IsSteamRunning())
        {
            manager.RegisterProvider(new SteamAchievementProvider());
        }
    }
}
#endif
```

**project.godot setup:**
```ini
[autoload]
Achievements="*res://addons/godot_achievements/AchievementManager.tscn"
SteamAchievements="*res://addons/godot_achievements_steam/SteamAutoload.tscn"
```

## 🔄 Sync Strategy: Local as Source of Truth

### Key Principles
1. **Local first**: All unlocks write to `user://achievements.json` immediately
2. **Platform sync**: Local state syncs to all registered platforms (Steam, Game Center, etc.)
3. **Offline queue**: Failed syncs retry automatically every 30 seconds
4. **Startup sync**: On game start, local achievements sync to platforms (catches missed syncs)

### Unlock Flow

```
User calls: AchievementManager.Instance.Unlock("boss_defeated")
    ↓
1. Unlock in LocalProvider (writes to user://achievements.json)
    ↓
2. Sync to all platform providers in parallel
    ├─→ Steam (if available)
    ├─→ Game Center (if available)
    └─→ Google Play (if available)
    ↓
3. Failed syncs → Retry queue (30s interval)
    ↓
4. Show toast (if newly unlocked)
```

### Startup Sync Flow

```
Game starts
    ↓
AchievementManager._Ready()
    ↓
Load local achievements from user://achievements.json
    ↓
For each unlocked local achievement:
    ├─→ Check if Steam has it → Sync if missing
    ├─→ Check if Game Center has it → Sync if missing
    └─→ Check if Google Play has it → Sync if missing
```

### Benefits
- ✅ Achievements never lost (local persistence)
- ✅ Platform APIs can fail without breaking gameplay
- ✅ Works offline (syncs when connection restored)
- ✅ Cross-platform achievement portability
- ✅ Automatic conflict resolution (local wins)

## 📐 Data Models

### Achievement Definition (Resource)

```csharp
[GlobalClass]
public partial class Achievement : Resource
{
    // Display info
    [Export] public string Id { get; set; }                    // "boss_defeated"
    [Export] public string DisplayName { get; set; }           // "Dragon Slayer"
    [Export] public string Description { get; set; }           // "Defeat the final boss"
    [Export] public Texture2D Icon { get; set; }              // Icon image
    [Export] public bool Hidden { get; set; }                  // Hide until unlocked

    // Platform ID mappings (built-in)
    [Export] public string SteamId { get; set; }              // "ACH_BOSS_DEFEATED"
    [Export] public string GameCenterId { get; set; }         // "com.game.boss_defeated"
    [Export] public string GooglePlayId { get; set; }         // "CgkI...boss_defeated"

    // Custom platform metadata (for third-party providers)
    [Export] public Godot.Collections.Dictionary<string, string> CustomPlatformIds { get; set; } = new();

    // Runtime state (managed by LocalProvider)
    public bool IsUnlocked { get; set; }
    public DateTime? UnlockedAt { get; set; }
    public float Progress { get; set; }                        // 0.0 to 1.0

    // Helper methods for custom platforms
    public string? GetPlatformId(string platform)
    {
        return CustomPlatformIds.TryGetValue(platform, out var id) ? id : null;
    }

    public void SetPlatformId(string platform, string id)
    {
        CustomPlatformIds[platform] = id;
    }
}
```

### Achievement Database (Resource)

```csharp
[GlobalClass]
public partial class AchievementDatabase : Resource
{
    [Export] public Godot.Collections.Array<Achievement> Achievements { get; set; }

    public Achievement GetById(string id) =>
        Achievements.FirstOrDefault(a => a.Id == id);
}
```

Saved as: `res://achievements.tres` (version controlled)

### Local Save Data

```json
// Saved to: user://achievements.json
// Uses Godot.Collections.Dictionary for AOT compatibility
{
    "first_kill": {
        "IsUnlocked": true,
        "UnlockedAt": "2025-12-21T15:30:00.0000000Z",
        "Progress": 1.0
    },
    "kill_100_enemies": {
        "IsUnlocked": false,
        "UnlockedAt": "",
        "Progress": 0.65
    }
}
```

**Note:** Uses `Godot.Collections.Dictionary` instead of C# POCO classes for full AOT compatibility.

## 🎮 User API

### Simple Unlock
```csharp
// In game code
AchievementManager.Instance.Unlock("first_kill");
AchievementManager.Instance.Unlock("speedrun_complete");
```

### Progress Tracking
```csharp
// Incremental achievement (e.g., "Kill 100 enemies")
AchievementManager.Instance.SetProgress("kill_100_enemies", 0.5f); // 50/100
```

### Query State
```csharp
var achievement = AchievementManager.Instance.GetAchievement("boss_defeated");
if (achievement.IsUnlocked)
{
    GD.Print($"Unlocked on: {achievement.UnlockedAt}");
}
```

### Toast Control
```csharp
// Enable/disable toast notifications
AchievementManager.Instance.ShowToasts = true;
```

## 🎨 Toast System

### Default Toast
Built-in toast notification with:
- Slide-in animation (configurable direction)
- Achievement icon + name + description
- Sound effect (optional)
- 3-second display duration (configurable)
- Queue system (multiple unlocks)

### Toast Architecture
```csharp
[GlobalClass]
public partial class AchievementToast : Control
{
    [Export] public float DisplayDuration { get; set; } = 3.0f;
    [Export] public Vector2 SlideDirection { get; set; } = Vector2.Down;
    [Export] public AudioStream UnlockSound { get; set; }

    public void Show(Achievement achievement)
    {
        // Animate in, display, animate out
    }
}
```

### Customization
Users can extend or replace:
```csharp
public partial class MyCustomToast : AchievementToast
{
    // Override visuals, animations, sounds
}

// In game setup
AchievementManager.Instance.ToastScene = GD.Load<PackedScene>("res://my_toast.tscn");
```

## 🛠️ Editor Integration

### Plugin Structure
```
addons/godot_achievements/
├── plugin.cfg
├── AchievementEditorPlugin.cs       # EditorPlugin entry point
├── AchievementEditorDock.cs         # Main dock UI
├── AchievementManager.tscn          # Autoload scene
├── ToastDefault.tscn                # Default toast UI
└── icons/
    └── achievement_icon.svg
```

### Editor Dock Features

**Achievement List Panel**
- Tree view of all achievements
- Add/Remove/Duplicate buttons
- Search/filter bar
- Drag-to-reorder

**Achievement Editor Panel**
- ID field (auto-generated option)
- Display name (translatable)
- Description (translatable)
- Icon selector (drag-drop texture)
- Hidden checkbox
- Platform ID mapping section:
  - Steam ID field
  - Game Center ID field
  - Google Play ID field
- Preview button (shows toast)

**Toolbar Actions**
- "New Achievement" button
- "Import from CSV" button
- "Export to CSV" button
- "Test Unlock" button (editor only)
- "Reset All" button (debug)

**Validation**
- Warns if achievement ID conflicts
- Warns if platform IDs missing before export
- Shows platform availability indicators

### Export Integration

Before export, validate:
- All achievements have platform IDs for target platform
- No duplicate IDs
- All referenced icons exist

## 🔧 Platform Provider Details

### Steam Provider
```csharp
public class SteamAchievementProvider : IAchievementProvider
{
    public string ProviderName => "Steam";
    public bool IsAvailable => SteamAPI.IsSteamRunning();

    private AchievementDatabase _database;

    public async Task<AchievementUnlockResult> UnlockAchievement(string achievementId)
    {
        var achievement = _database.GetById(achievementId);
        var steamId = achievement?.SteamId;

        if (string.IsNullOrEmpty(steamId))
            return new() { Success = false, Error = "No Steam ID mapping" };

        bool wasUnlocked = SteamUserStats.GetAchievement(steamId, out bool achieved) && achieved;

        if (!wasUnlocked)
        {
            SteamUserStats.SetAchievement(steamId);
            SteamUserStats.StoreStats();
        }

        return new() { Success = true, WasAlreadyUnlocked = wasUnlocked };
    }
}
```

### iOS Game Center Provider
```csharp
public class GameCenterAchievementProvider : IAchievementProvider
{
    public string ProviderName => "Game Center";
    public bool IsAvailable => GameCenter.IsAuthenticated();

    public async Task<AchievementUnlockResult> UnlockAchievement(string achievementId)
    {
        var achievement = _database.GetById(achievementId);
        var gameCenterId = achievement?.GameCenterId;

        // Report to Game Center with 100% progress
        await GameCenter.ReportAchievement(gameCenterId, 100.0);

        return new() { Success = true };
    }
}
```

### Android Google Play Provider
```csharp
public class GooglePlayAchievementProvider : IAchievementProvider
{
    public string ProviderName => "Google Play";
    public bool IsAvailable => PlayGamesPlatform.Instance.IsAuthenticated();

    public async Task<AchievementUnlockResult> UnlockAchievement(string achievementId)
    {
        var achievement = _database.GetById(achievementId);
        var googlePlayId = achievement?.GooglePlayId;

        PlayGamesPlatform.Instance.UnlockAchievement(googlePlayId, success =>
        {
            // Callback handling
        });

        return new() { Success = true };
    }
}
```

### Local Provider
```csharp
public class LocalAchievementProvider : IAchievementProvider
{
    private const string SavePath = "user://achievements.json";
    private Godot.Collections.Dictionary<string, Godot.Collections.Dictionary> _savedData;

    public string ProviderName => "Local";
    public bool IsAvailable => true; // Always available

    public LocalAchievementProvider()
    {
        LoadFromDisk();
    }

    public async Task<AchievementUnlockResult> UnlockAchievement(string achievementId)
    {
        bool wasUnlocked = _savedData.ContainsKey(achievementId)
            && (bool)_savedData[achievementId]["IsUnlocked"];

        _savedData[achievementId] = new Godot.Collections.Dictionary
        {
            { "IsUnlocked", true },
            { "UnlockedAt", DateTime.UtcNow.ToString("O") }, // ISO 8601 format
            { "Progress", 1.0f }
        };

        SaveToDisk();

        return new() { Success = true, WasAlreadyUnlocked = wasUnlocked };
    }

    private void SaveToDisk()
    {
        var jsonString = Json.Stringify(_savedData, "\t"); // Pretty print with tabs
        using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write);
        file.StoreString(jsonString);
    }

    private void LoadFromDisk()
    {
        if (!FileAccess.FileExists(SavePath))
        {
            _savedData = new Godot.Collections.Dictionary<string, Godot.Collections.Dictionary>();
            return;
        }

        using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Read);
        var jsonString = file.GetAsText();

        var json = new Json();
        var error = json.Parse(jsonString);

        if (error != Error.Ok)
        {
            GD.PushError($"Failed to parse achievements JSON: {json.GetErrorMessage()}");
            _savedData = new Godot.Collections.Dictionary<string, Godot.Collections.Dictionary>();
            return;
        }

        _savedData = json.Data.AsGodotDictionary<string, Godot.Collections.Dictionary>();
    }
}
```

## 🚀 Advanced Features

### Achievement Groups
```csharp
// Meta-achievement: "Complete all tutorials"
AchievementManager.Instance.OnAchievementUnlocked += (id) =>
{
    if (AllTutorialsComplete())
    {
        AchievementManager.Instance.Unlock("tutorial_master");
    }
};
```

### Progress Events
```csharp
AchievementManager.Instance.OnProgressChanged += (id, progress) =>
{
    UpdateProgressBar(id, progress);
};
```

### Localization
```csharp
// In Achievement resource
[Export] public string DisplayNameKey { get; set; } = "ACH_BOSS_NAME";

// Runtime
string localizedName = Tr(achievement.DisplayNameKey);
```

### Stats Integration
```csharp
// Track stats that contribute to achievements
StatsManager.Instance.IncrementStat("enemies_killed");

// Achievements listen to stats
StatsManager.Instance.OnStatChanged += (stat, value) =>
{
    if (stat == "enemies_killed" && value >= 100)
    {
        AchievementManager.Instance.Unlock("kill_100_enemies");
    }
};
```

### Debug Tools & Runtime Testing

#### Debug Node (Runtime Achievement Tester)

A Control node that can be instanced during development for easy achievement testing:

```csharp
[Tool]
public partial class AchievementDebugPanel : PanelContainer
{
    private VBoxContainer _achievementList;
    private Button _resetAllButton;
    private CheckBox _showToastsCheckbox;

    public override void _Ready()
    {
        BuildUI();
        RefreshAchievements();
    }

    private void BuildUI()
    {
        var vbox = new VBoxContainer();
        AddChild(vbox);

        // Header
        var title = new Label { Text = "🐛 Achievement Debugger",
            HorizontalAlignment = HorizontalAlignment.Center };
        vbox.AddChild(title);

        // Controls
        var controls = new HBoxContainer();
        vbox.AddChild(controls);

        _resetAllButton = new Button { Text = "Reset All" };
        _resetAllButton.Pressed += OnResetAll;
        controls.AddChild(_resetAllButton);

        var refreshButton = new Button { Text = "Refresh" };
        refreshButton.Pressed += RefreshAchievements;
        controls.AddChild(refreshButton);

        _showToastsCheckbox = new CheckBox { Text = "Show Toasts", ButtonPressed = true };
        controls.AddChild(_showToastsCheckbox);

        // Achievement list with unlock buttons
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(0, 300) };
        vbox.AddChild(scroll);

        _achievementList = new VBoxContainer();
        scroll.AddChild(_achievementList);
    }

    private void RefreshAchievements()
    {
        // Clear existing
        foreach (var child in _achievementList.GetChildren())
            child.QueueFree();

        // Add achievement rows
        var achievements = AchievementManager.Instance.GetAllAchievements();
        foreach (var achievement in achievements)
        {
            var row = new HBoxContainer();
            _achievementList.AddChild(row);

            // Icon
            var icon = new TextureRect
            {
                Texture = achievement.Icon,
                CustomMinimumSize = new Vector2(32, 32),
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered
            };
            row.AddChild(icon);

            // Info
            var info = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            row.AddChild(info);

            var nameLabel = new Label
            {
                Text = achievement.DisplayName,
                Theme = new Theme() // Bold
            };
            info.AddChild(nameLabel);

            var statusLabel = new Label
            {
                Text = achievement.IsUnlocked
                    ? $"✓ Unlocked {achievement.UnlockedAt:g}"
                    : "Locked",
                Modulate = achievement.IsUnlocked ? Colors.Green : Colors.Gray
            };
            info.AddChild(statusLabel);

            // Progress bar for incremental achievements
            if (achievement.Progress > 0 && achievement.Progress < 1)
            {
                var progressBar = new ProgressBar
                {
                    Value = achievement.Progress * 100,
                    ShowPercentage = true
                };
                info.AddChild(progressBar);
            }

            // Unlock button
            var unlockBtn = new Button
            {
                Text = achievement.IsUnlocked ? "Unlock Again" : "Unlock",
                Disabled = false
            };
            unlockBtn.Pressed += () => OnUnlockAchievement(achievement.Id);
            row.AddChild(unlockBtn);

            // Reset individual button
            var resetBtn = new Button { Text = "Reset" };
            resetBtn.Pressed += () => OnResetAchievement(achievement.Id);
            row.AddChild(resetBtn);
        }
    }

    private async void OnUnlockAchievement(string id)
    {
        var prevState = AchievementManager.Instance.ShowToasts;
        AchievementManager.Instance.ShowToasts = _showToastsCheckbox.ButtonPressed;

        await AchievementManager.Instance.Unlock(id);

        AchievementManager.Instance.ShowToasts = prevState;
        RefreshAchievements();
    }

    private void OnResetAchievement(string id)
    {
        AchievementManager.Instance.ResetAchievement(id);
        RefreshAchievements();
    }

    private void OnResetAll()
    {
        AchievementManager.Instance.ResetAllAchievements();
        RefreshAchievements();
    }
}
```

**Usage:**
```csharp
// Add to debug menu scene
var debugPanel = new AchievementDebugPanel();
debugMenu.AddChild(debugPanel);

// Or instance the shipped scene
var debugPanel = GD.Load<PackedScene>("res://addons/godot_achievements/DebugPanel.tscn").Instantiate();
AddChild(debugPanel);
```

**Features:**
- ✅ List all achievements with current state
- ✅ Unlock/Reset individual achievements
- ✅ Reset all achievements at once
- ✅ Show progress bars for incremental achievements
- ✅ Toggle toast preview on/off
- ✅ Real-time refresh of achievement state
- ✅ Visual status indicators (locked/unlocked)

#### AchievementManager Debug API

```csharp
public partial class AchievementManager : Node
{
    #if DEBUG || TOOLS

    /// <summary>
    /// Reset a specific achievement (local only, doesn't affect platforms)
    /// </summary>
    public void ResetAchievement(string achievementId)
    {
        _localProvider.Reset(achievementId);
        EmitSignal(SignalName.AchievementReset, achievementId);
    }

    /// <summary>
    /// Reset all achievements (local only)
    /// </summary>
    public void ResetAllAchievements()
    {
        _localProvider.ResetAll();
        EmitSignal(SignalName.AllAchievementsReset);
    }

    /// <summary>
    /// Simulate platform sync for testing (doesn't actually call platform APIs)
    /// </summary>
    public async Task TestPlatformSync(string achievementId)
    {
        await Task.Delay(1000); // Simulate network delay
        GD.Print($"[TEST] Would sync '{achievementId}' to {_platformProviders.Count} platforms");
    }

    #endif
}
```

#### Keyboard Shortcut for Debug Panel

```csharp
// In game's main script
public override void _Input(InputEvent @event)
{
    #if DEBUG
    if (@event is InputEventKey keyEvent && keyEvent.Pressed)
    {
        // Ctrl+Shift+A to toggle achievement debug panel
        if (keyEvent.Keycode == Key.A && keyEvent.CtrlPressed && keyEvent.ShiftPressed)
        {
            ToggleAchievementDebugPanel();
        }
    }
    #endif
}
```

## 🔌 Creating Custom Achievement Providers

The plugin architecture allows third-party developers to create custom achievement platform providers. This enables support for platforms like Epic Games Store, GOG Galaxy, Discord, or any custom backend.

### Custom Provider Interface

All providers implement the same interface:

```csharp
public interface IAchievementProvider
{
    string ProviderName { get; }
    bool IsAvailable { get; }

    Task<AchievementUnlockResult> UnlockAchievement(string achievementId);
    Task<Achievement?> GetAchievement(string achievementId);
    Task<Achievement[]> GetAllAchievements();
    Task<float> GetProgress(string achievementId);
    Task SetProgress(string achievementId, float progress);
}
```

### Example: Epic Games Store Provider

```csharp
// In Godot.Achievements.Epic NuGet package

#if GODOT_PC || GODOT_WINDOWS || GODOT_LINUX || GODOT_MACOS
using Epic.OnlineServices;
using Epic.OnlineServices.Achievements;

namespace Godot.Achievements.Epic;

public class EpicAchievementProvider : IAchievementProvider
{
    private readonly AchievementDatabase _database;
    private readonly AchievementsInterface _epicAchievements;

    public string ProviderName => "Epic Games Store";

    public bool IsAvailable
    {
        get
        {
            // Check if EOS is initialized and user is logged in
            return PlatformInterface.GetPlatformInterface() != null
                && EOS_Platform_GetLoginStatus() == LoginStatus.LoggedIn;
        }
    }

    public EpicAchievementProvider(AchievementDatabase database)
    {
        _database = database;
        var platform = PlatformInterface.GetPlatformInterface();
        _epicAchievements = platform.GetAchievementsInterface();
    }

    public async Task<AchievementUnlockResult> UnlockAchievement(string achievementId)
    {
        var achievement = _database.GetById(achievementId);
        var epicId = achievement?.GetPlatformId("Epic"); // Custom metadata

        if (string.IsNullOrEmpty(epicId))
        {
            return new AchievementUnlockResult
            {
                Success = false,
                Error = "No Epic achievement ID mapping found"
            };
        }

        // Check current state
        var queryOptions = new QueryPlayerAchievementsOptions
        {
            TargetUserId = GetLocalUserId(),
            LocalUserId = GetLocalUserId()
        };

        var tcs = new TaskCompletionSource<bool>();

        _epicAchievements.QueryPlayerAchievements(queryOptions, null, (ref QueryPlayerAchievementsCallbackInfo data) =>
        {
            if (data.ResultCode == Result.Success)
            {
                tcs.SetResult(true);
            }
            else
            {
                tcs.SetResult(false);
            }
        });

        await tcs.Task;

        // Unlock the achievement
        var unlockOptions = new UnlockAchievementsOptions
        {
            UserId = GetLocalUserId(),
            AchievementIds = new[] { epicId }
        };

        var unlockTcs = new TaskCompletionSource<AchievementUnlockResult>();

        _epicAchievements.UnlockAchievements(unlockOptions, null, (ref UnlockAchievementsCallbackInfo data) =>
        {
            unlockTcs.SetResult(new AchievementUnlockResult
            {
                Success = data.ResultCode == Result.Success,
                Error = data.ResultCode != Result.Success ? data.ResultCode.ToString() : null
            });
        });

        return await unlockTcs.Task;
    }

    public async Task<Achievement?> GetAchievement(string achievementId)
    {
        // Implementation...
        await Task.CompletedTask;
        return _database.GetById(achievementId);
    }

    public async Task<Achievement[]> GetAllAchievements()
    {
        // Implementation...
        await Task.CompletedTask;
        return _database.Achievements.ToArray();
    }

    public async Task<float> GetProgress(string achievementId)
    {
        // Query progress from Epic
        await Task.CompletedTask;
        return 0f;
    }

    public async Task SetProgress(string achievementId, float progress)
    {
        // Set progress on Epic (for stat-based achievements)
        await Task.CompletedTask;
    }

    private ProductUserId GetLocalUserId()
    {
        // Get Epic user ID
        return null; // Placeholder
    }
}
#endif
```

### Autoload Registration Pattern

```csharp
// In Godot.Achievements.Epic package
// File: EpicAchievementAutoload.cs

#if GODOT_PC || GODOT_WINDOWS || GODOT_LINUX || GODOT_MACOS
namespace Godot.Achievements.Epic;

public partial class EpicAchievementAutoload : Node
{
    public override void _Ready()
    {
        var manager = GetNode<AchievementManager>("/root/Achievements");
        var database = manager.Database;

        var epicProvider = new EpicAchievementProvider(database);
        manager.RegisterProvider(epicProvider);

        GD.Print("[Epic] Achievement provider registered");
    }
}
#endif
```

### Extended Achievement Model for Custom Platforms

To support custom platform IDs, extend the Achievement resource with metadata:

```csharp
[GlobalClass]
public partial class Achievement : Resource
{
    // Standard fields
    [Export] public string Id { get; set; }
    [Export] public string DisplayName { get; set; }
    [Export] public string Description { get; set; }
    [Export] public Texture2D Icon { get; set; }
    [Export] public bool Hidden { get; set; }

    // Built-in platform IDs
    [Export] public string SteamId { get; set; }
    [Export] public string GameCenterId { get; set; }
    [Export] public string GooglePlayId { get; set; }

    // Custom platform metadata (dictionary for third-party providers)
    [Export] public Godot.Collections.Dictionary<string, string> CustomPlatformIds { get; set; } = new();

    // Runtime state
    public bool IsUnlocked { get; set; }
    public DateTime? UnlockedAt { get; set; }
    public float Progress { get; set; }

    // Helper to get custom platform IDs
    public string? GetPlatformId(string platform)
    {
        return CustomPlatformIds.TryGetValue(platform, out var id) ? id : null;
    }

    public void SetPlatformId(string platform, string id)
    {
        CustomPlatformIds[platform] = id;
    }
}
```

### Editor Extension for Custom Platforms

Custom providers can extend the editor to add their platform ID fields:

```csharp
// In Godot.Achievements.Epic package
#if TOOLS
public partial class EpicEditorExtension : EditorPlugin
{
    public override void _EnterTree()
    {
        // Hook into achievement editor to add Epic ID field
        var achievementEditor = GetNode<AchievementEditorDock>("/root/EditorNode/AchievementEditor");
        achievementEditor?.RegisterPlatformField("Epic", "Epic Games ID");
    }
}
#endif
```

### NuGet Package Structure for Custom Providers

```
Godot.Achievements.Epic/
├── lib/
│   └── netstandard2.1/
│       ├── Godot.Achievements.Epic.dll
│       └── EOS-SDK.dll (Epic dependency)
├── content/
│   └── addons/godot_achievements_epic/
│       ├── EpicAutoload.tscn
│       └── plugin.cfg (optional editor extension)
├── build/
│   └── Godot.Achievements.Epic.props (MSBuild props)
└── Godot.Achievements.Epic.nuspec
```

### NuGet .nuspec Example

```xml
<?xml version="1.0"?>
<package>
  <metadata>
    <id>Godot.Achievements.Epic</id>
    <version>1.0.0</version>
    <authors>YourName</authors>
    <description>Epic Games Store achievement provider for Godot.Achievements.NET</description>
    <dependencies>
      <dependency id="Godot.Achievements.Core" version="1.0.0" />
      <dependency id="EOS-SDK" version="1.15.0" />
    </dependencies>
  </metadata>
  <files>
    <file src="bin/Release/netstandard2.1/Godot.Achievements.Epic.dll" target="lib/netstandard2.1" />
    <file src="content/**" target="content" />
  </files>
</package>
```

### User Installation

```xml
<!-- User's .csproj -->
<ItemGroup>
  <PackageReference Include="Godot.Achievements.Core" Version="1.0.0" />
  <PackageReference Include="Godot.Achievements.Steam" Version="1.0.0" Condition="'$(GodotTargetPlatform)' == 'windows'" />

  <!-- Custom third-party provider -->
  <PackageReference Include="Godot.Achievements.Epic" Version="1.0.0" Condition="'$(GodotTargetPlatform)' == 'windows'" />
</ItemGroup>
```

### Provider Discovery API

```csharp
// In AchievementManager
public partial class AchievementManager : Node
{
    public IReadOnlyList<IAchievementProvider> GetRegisteredProviders()
    {
        var all = new List<IAchievementProvider> { _localProvider };
        all.AddRange(_platformProviders);
        return all.AsReadOnly();
    }

    public IAchievementProvider GetProvider(string providerName)
    {
        return GetRegisteredProviders()
            .FirstOrDefault(p => p.ProviderName.Equals(providerName, StringComparison.OrdinalIgnoreCase));
    }

    // Event for when providers are registered
    [Signal]
    public delegate void ProviderRegisteredEventHandler(string providerName);
}
```

### Testing Custom Providers

```csharp
// In your game's debug menu
public void ListRegisteredProviders()
{
    var providers = AchievementManager.Instance.GetRegisteredProviders();

    GD.Print("=== Registered Achievement Providers ===");
    foreach (var provider in providers)
    {
        GD.Print($"- {provider.ProviderName} (Available: {provider.IsAvailable})");
    }
}

// Test custom provider
public async void TestEpicProvider()
{
    var epicProvider = AchievementManager.Instance.GetProvider("Epic Games Store");
    if (epicProvider != null && epicProvider.IsAvailable)
    {
        var result = await epicProvider.UnlockAchievement("first_kill");
        GD.Print($"Epic unlock result: {result.Success}");
    }
}
```

### Custom Provider Checklist

When creating a custom achievement provider:

- [ ] Implement `IAchievementProvider` interface
- [ ] Handle platform-specific authentication/initialization
- [ ] Map local achievement IDs to platform IDs (via Achievement.GetPlatformId())
- [ ] Implement async unlock/progress methods
- [ ] Add `IsAvailable` check (SDK initialized, user logged in, etc.)
- [ ] Create autoload node for registration
- [ ] Use conditional compilation for platform-specific code
- [ ] Add NuGet package dependencies
- [ ] Document platform ID format for users
- [ ] (Optional) Create editor extension for platform ID fields
- [ ] Test with debug panel
- [ ] Document installation in README

### Example Custom Providers

**Potential community providers:**
- `Godot.Achievements.Epic` - Epic Games Store
- `Godot.Achievements.GOG` - GOG Galaxy
- `Godot.Achievements.Discord` - Discord Rich Presence activities
- `Godot.Achievements.Xbox` - Xbox Live (via GDK)
- `Godot.Achievements.PlayStation` - PlayStation Network
- `Godot.Achievements.Nintendo` - Nintendo Switch
- `Godot.Achievements.ItchIO` - Itch.io achievements
- `Godot.Achievements.Firebase` - Custom backend via Firebase
- `Godot.Achievements.PlayFab` - Azure PlayFab

### Publishing Guidelines

**For community providers:**

1. **Naming Convention**: `Godot.Achievements.[Platform]`
2. **Namespace**: Match package name (e.g., `Godot.Achievements.Epic`)
3. **Dependencies**: Reference `Godot.Achievements.Core` only
4. **Documentation**: Include setup guide with platform API key instructions
5. **Samples**: Provide example achievement setup
6. **License**: Use MIT or compatible open-source license
7. **Repository**: Link to source code in NuGet description
8. **Testing**: Include unit tests for provider logic

## 📋 Implementation Checklist

### Phase 1: Core Package
- [ ] `IAchievementProvider` interface
- [ ] `Achievement` resource class
- [ ] `AchievementDatabase` resource class
- [ ] `AchievementManager` singleton
- [ ] `LocalAchievementProvider` implementation
- [ ] JSON save/load for local data
- [ ] Default toast system
- [ ] Editor plugin infrastructure
- [ ] `AchievementDebugPanel` runtime debug node

### Phase 2: Editor Integration
- [ ] Achievement editor dock UI
- [ ] Achievement list view
- [ ] Achievement property editor
- [ ] Platform ID mapping UI
- [ ] Import/export CSV
- [ ] Test unlock in editor
- [ ] Export validation

### Phase 3: Platform Packages
- [ ] Steam provider + autoload
- [ ] iOS Game Center provider + autoload
- [ ] Android Google Play provider + autoload
- [ ] Conditional compilation setup
- [ ] Platform detection helpers

### Phase 4: Sync System
- [ ] Unlock flow (local → platforms)
- [ ] Startup sync (local → platforms)
- [ ] Retry queue for failed syncs
- [ ] Conflict resolution
- [ ] Progress tracking sync

### Phase 5: NuGet Packaging
- [ ] Core package .nuspec
- [ ] Steam package .nuspec
- [ ] iOS package .nuspec
- [ ] Android package .nuspec
- [ ] Documentation
- [ ] Sample project

## 🎯 Design Goals Summary

✅ **C# focused** - No GDScript required
✅ **Platform agnostic** - Abstraction layer for all platforms
✅ **Editor-driven** - Configure achievements without code
✅ **Local-first** - Achievements never lost
✅ **AOT compatible** - Works on iOS, console platforms
✅ **Offline support** - Auto-sync when connection restored
✅ **NuGet distribution** - Easy installation per platform
✅ **Extensible** - Custom providers, custom toasts
✅ **Production ready** - Retry logic, error handling, validation
