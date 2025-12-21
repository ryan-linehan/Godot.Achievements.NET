# Godot.Achievements.NET - Design Document

## 🎯 Overview

A C#-focused achievements plugin for Godot 4+ that provides:
- Editor-based achievement setup
- Platform-agnostic abstraction layer
- Multiple achievement system integrations
- Local-first sync strategy
- Default toast notification system
- AOT-compatible architecture

## 📦 NuGet Package Structure

### Core Package
**Godot.Achievements.Core**
- Achievement definition models (Resources)
- `IAchievementProvider` interface
- `AchievementManager` singleton
- Local achievement provider (user:// file persistence)
- Editor plugin & dock UI
- Default toast notification system

### Platform Packages
**Godot.Achievements.Steam**
- Steam achievement provider
- Dependencies: `Steamworks.NET` via Godot.Steamworks.NET
- Conditional compilation: `#if GODOT_PC || GODOT_WINDOWS || GODOT_LINUX || GODOT_MACOS`

**Godot.Achievements.iOS**
- Game Center achievement provider
- Dependencies: Swift Godot bindings (GodotApplePlugins)
- Conditional compilation: `#if GODOT_IOS`

**Godot.Achievements.Android**
- Google Play Games achievement provider
- Dependencies: Android-specific bindings
- Conditional compilation: `#if GODOT_ANDROID`

### User Installation Example
```xml
<ItemGroup>
  <!-- Always required -->
  <PackageReference Include="Godot.Achievements.Core" Version="1.0.0" />

  <!-- Platform-specific (only installed when needed) -->
  <PackageReference Include="Godot.Achievements.Steam" Version="1.0.0"
                    Condition="'$(GodotTargetPlatform)' == 'windows' OR '$(Configuration)' == 'Debug'" />
  <PackageReference Include="Godot.Achievements.iOS" Version="1.0.0"
                    Condition="'$(GodotTargetPlatform)' == 'ios'" />
  <PackageReference Include="Godot.Achievements.Android" Version="1.0.0"
                    Condition="'$(GodotTargetPlatform)' == 'android'" />
</ItemGroup>
```

## 🏗️ Architecture

### AOT-Compatible Design

**No reflection** - all platform registrations use compile-time patterns:
- Autoload node registration
- Conditional compilation symbols
- Static interface implementations
- Concrete types (no dynamic invocation)

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

    // Platform ID mappings
    [Export] public string SteamId { get; set; }              // "ACH_BOSS_DEFEATED"
    [Export] public string GameCenterId { get; set; }         // "com.game.boss_defeated"
    [Export] public string GooglePlayId { get; set; }         // "CgkI...boss_defeated"

    // Runtime state (managed by LocalProvider)
    public bool IsUnlocked { get; set; }
    public DateTime? UnlockedAt { get; set; }
    public float Progress { get; set; }                        // 0.0 to 1.0
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

```csharp
public class AchievementSaveData
{
    public bool IsUnlocked { get; set; }
    public DateTime UnlockedAt { get; set; }
    public float Progress { get; set; }
}

// Dictionary<string, AchievementSaveData>
// Saved to: user://achievements.json
```

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
    private Dictionary<string, AchievementSaveData> _savedData;

    public string ProviderName => "Local";
    public bool IsAvailable => true; // Always available

    public async Task<AchievementUnlockResult> UnlockAchievement(string achievementId)
    {
        bool wasUnlocked = _savedData.ContainsKey(achievementId)
            && _savedData[achievementId].IsUnlocked;

        _savedData[achievementId] = new()
        {
            IsUnlocked = true,
            UnlockedAt = DateTime.UtcNow,
            Progress = 1.0f
        };

        SaveToDisk();

        return new() { Success = true, WasAlreadyUnlocked = wasUnlocked };
    }

    private void SaveToDisk()
    {
        var json = JsonSerializer.Serialize(_savedData);
        using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write);
        file.StoreString(json);
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
