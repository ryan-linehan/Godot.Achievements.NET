# Godot.Achievements.NET

A C#-focused achievements plugin for Godot 4+ that provides editor-based achievement setup, platform-agnostic abstraction, and local-first sync strategy.

## Features

- 🎯 **Editor-based achievement setup** - Configure achievements without code
- 🌐 **Platform-agnostic** - Abstraction layer for Steam, Game Center, Google Play, and custom providers
- 💾 **Local-first sync** - Achievements never lost, auto-sync to platforms
- 🔔 **Default toast notifications** - Built-in achievement unlock notifications
- ⚡ **AOT compatible** - Works on iOS and console platforms
- 🔌 **Extensible** - Custom providers and toast systems
- 🔄 **Automatic retry** - Failed syncs retry automatically

## Installation

### Method 1: Git Submodule (Recommended)

```bash
# Add as a submodule to your Godot project
cd your-godot-project
git submodule add https://github.com/ryan-linehan/Godot.Achievements.NET.git addons/godot_achievements
```

### Method 2: Manual Installation

1. Download or clone this repository
2. Copy the `addons/godot_achievements` folder to your Godot project's `addons/` directory
3. (Optional) Copy platform-specific folders if needed:
   - `addons/godot_achievements_steam` for Steam support
   - `addons/godot_achievements_ios` for iOS Game Center support
   - `addons/godot_achievements_android` for Android Google Play support

### Method 3: Godot Asset Library (Coming Soon)

Search for "Godot Achievements" in the Godot Asset Library and download directly from the editor.

## Platform SDK Integration

This package works with official platform SDKs:

| Platform | SDK Required | Repository |
|----------|-------------|------------|
| **Steam** | Godot.Steamworks.NET | https://github.com/ryan-linehan/Godot.Steamworks.NET |
| **iOS** | GodotApplePlugins (GameKit) | https://github.com/migueldeicaza/GodotApplePlugins |
| **Android** | godot-play-game-services | https://github.com/godot-sdk-integrations/godot-play-game-services |

**See [INTEGRATION_GUIDE.md](INTEGRATION_GUIDE.md) for complete setup instructions.**

## Quick Start

### 1. Enable the Plugin

In Godot editor:
1. Go to **Project → Project Settings → Plugins**
2. Enable "Godot Achievements"

### 2. Create Achievement Database

1. In Godot, create a new resource: **FileSystem** (right-click) → **New Resource**
2. Search for and select **AchievementDatabase**
3. Save as `res://achievements.tres`

### 3. Add Achievements

You can add achievements in two ways:

#### Option A: Via Editor Dock (Recommended)
1. Open the **Achievements** dock (should appear in bottom panel after enabling plugin)
2. Click **Select Database...** and choose `res://achievements.tres`
3. Click **Add Achievement**
4. Fill in the fields:
   - **ID**: `first_kill` (unique identifier, used in code)
   - **Display Name**: `First Blood` (shown to player)
   - **Description**: `Defeat your first enemy`
   - **Icon**: Drag a texture (optional)
   - **Hidden**: Check if achievement should be hidden until unlocked
   - **Max Progress**: `1` for standard achievements, higher for progressive (e.g., `100` for "Kill 100 enemies")
5. (Optional) Set platform-specific IDs:
   - **Steam ID**: `ACH_FIRST_KILL`
   - **Game Center ID**: `com.yourcompany.game.first_kill`
   - **Google Play ID**: `CgkI7ea1q6IOEAIQBw`
6. Click **Save Changes**

#### Option B: Programmatically
```csharp
var database = GD.Load<AchievementDatabase>("res://achievements.tres");
var achievement = new Achievement
{
    Id = "first_kill",
    DisplayName = "First Blood",
    Description = "Defeat your first enemy",
    MaxProgress = 1,
    SteamId = "ACH_FIRST_KILL"
};
database.Achievements.Add(achievement);
ResourceSaver.Save(database, "res://achievements.tres");
```

### 4. Setup Autoload

Add the AchievementManager to your project autoloads:

1. **Create the Manager Scene:**
   - Create new scene: **Scene → New Scene**
   - Add root node of type **AchievementManager**
   - In the Inspector, set **Database** property to `res://achievements.tres`
   - (Optional) Configure **Show Toasts** (default: true)
   - (Optional) Set **Toast Duration** (default: 3 seconds)
   - Save scene as `res://autoloads/AchievementManager.tscn`

2. **Add to Autoloads:**
   - Go to **Project → Project Settings → Autoload**
   - Click the folder icon, select `res://autoloads/AchievementManager.tscn`
   - Set **Node Name** to `Achievements`
   - Check **Enable**
   - Click **Add**

3. **Verify Setup:**
   - Run your project
   - Check the Output console for: `[Achievements] AchievementManager initialized with X achievements`

### 5. Unlock Achievements in Code

```csharp
using Godot.Achievements.Core;

public partial class Player : CharacterBody2D
{
    public async void OnEnemyKilled()
    {
        // Simple unlock - fires and forgets
        await AchievementManager.Instance.Unlock("first_kill");
    }

    public async void OnBossDefeated(string bossName)
    {
        // Conditional unlock based on game state
        if (bossName == "FinalBoss")
        {
            await AchievementManager.Instance.Unlock("beat_final_boss");
        }
    }

    public async void OnProgressMade(int enemiesKilled)
    {
        // Progressive achievement with integer current/max values
        // MaxProgress set to 100 in achievement database
        await AchievementManager.Instance.SetProgress("kill_100_enemies", enemiesKilled);

        // Achievement auto-unlocks when current >= max
    }

    public async void OnCoinsCollected(int totalCoins)
    {
        // Multiple progressive achievements
        await AchievementManager.Instance.SetProgress("collect_100_coins", totalCoins);
        await AchievementManager.Instance.SetProgress("collect_1000_coins", totalCoins);
        await AchievementManager.Instance.SetProgress("collect_10000_coins", totalCoins);
    }

    public void DisplayAllAchievements()
    {
        var achievements = AchievementManager.Instance.GetAllAchievements();
        foreach (var achievement in achievements)
        {
            var status = achievement.IsUnlocked ? "✓" : "✗";
            var progress = achievement.MaxProgress > 1
                ? $" ({achievement.CurrentProgress}/{achievement.MaxProgress})"
                : "";
            GD.Print($"{status} {achievement.DisplayName}{progress}");
        }
    }

    public void CheckSpecificAchievement()
    {
        var achievement = AchievementManager.Instance.GetAchievement("first_kill");
        if (achievement != null && !achievement.IsUnlocked)
        {
            GD.Print($"Still need to unlock: {achievement.DisplayName}");
        }
    }
}
```

## Architecture

### Local-First Sync Strategy

1. **Local Storage** - All unlocks save to `user://achievements.json` immediately
2. **Platform Sync** - Local state syncs to all registered platforms (Steam, Game Center, etc.)
3. **Offline Queue** - Failed syncs retry automatically every 30 seconds
4. **Startup Sync** - On game start, local achievements sync to platforms

### Achievement Flow

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

## Platform Providers

### Creating Custom Providers

Implement the `IAchievementProvider` interface:

```csharp
public class MyCustomProvider : IAchievementProvider
{
    public string ProviderName => "My Platform";
    public bool IsAvailable => CheckSDKInitialized();

    public async Task<AchievementUnlockResult> UnlockAchievement(string achievementId)
    {
        // Map local ID to platform ID
        var achievement = _database.GetById(achievementId);
        var platformId = achievement?.GetPlatformId("MyPlatform");

        // Call platform SDK
        var success = await MyPlatformSDK.UnlockAsync(platformId);

        return success
            ? AchievementUnlockResult.SuccessResult()
            : AchievementUnlockResult.FailureResult("SDK error");
    }

    // Implement other interface methods...
}
```

Register your provider via an autoload:

```csharp
public partial class MyPlatformAutoload : Node
{
    public override void _Ready()
    {
        var manager = GetNode<AchievementManager>("/root/Achievements");
        manager.RegisterProvider(new MyCustomProvider());
    }
}
```

## Signals

Connect to achievement events:

```csharp
public override void _Ready()
{
    AchievementManager.Instance.AchievementUnlocked += OnAchievementUnlocked;
    AchievementManager.Instance.AchievementProgressChanged += OnProgressChanged;
    AchievementManager.Instance.ProviderRegistered += OnProviderRegistered;
}

private void OnAchievementUnlocked(string id, Achievement achievement)
{
    GD.Print($"Achievement unlocked: {achievement.DisplayName}");
}

private void OnProgressChanged(string id, float progress)
{
    GD.Print($"Achievement {id} progress: {progress * 100}%");
}

private void OnProviderRegistered(string providerName)
{
    GD.Print($"Provider registered: {providerName}");
}
```

## Custom Toast Notifications

Disable default toasts and implement your own:

```csharp
// In AchievementManager node properties
ShowToasts = false;

// In your code
AchievementManager.Instance.AchievementUnlocked += OnAchievementUnlocked;

private void OnAchievementUnlocked(string id, Achievement achievement)
{
    // Show your custom notification
    MyCustomNotificationSystem.Show(achievement.DisplayName, achievement.Description);
}
```

## Usage Examples

### Progressive Achievements

For achievements that track progress over time:

```csharp
// Achievement setup in database:
// ID: master_collector
// MaxProgress: 1000

public partial class GameManager : Node
{
    private int _totalItemsCollected = 0;

    public async void OnItemCollected()
    {
        _totalItemsCollected++;

        // Update progress - achievement unlocks automatically when >= MaxProgress
        await AchievementManager.Instance.SetProgress("master_collector", _totalItemsCollected);
    }

    public override void _Ready()
    {
        // Load saved progress from previous session
        var achievement = AchievementManager.Instance.GetAchievement("master_collector");
        if (achievement != null)
        {
            _totalItemsCollected = achievement.CurrentProgress;
        }
    }
}
```

### Testing Achievements

Reset achievements during development:

```csharp
public partial class DebugMenu : Control
{
    public async void OnResetAchievementsPressed()
    {
        // Reset single achievement
        await AchievementManager.Instance.GetProvider("Local")?.ResetAchievement("first_kill");

        // Reset all achievements (local only)
        var localProvider = AchievementManager.Instance.GetProvider("Local");
        if (localProvider != null)
        {
            await localProvider.ResetAllAchievements();
        }

        GD.Print("Achievements reset!");
    }

    public void OnUnlockAllPressed()
    {
        // For testing - unlock everything
        var achievements = AchievementManager.Instance.GetAllAchievements();
        foreach (var achievement in achievements)
        {
            _ = AchievementManager.Instance.Unlock(achievement.Id);
        }
    }
}
```

### Listening to Events

React to achievement unlocks in real-time:

```csharp
public partial class AchievementUI : Control
{
    public override void _Ready()
    {
        // Subscribe to events
        AchievementManager.Instance.AchievementUnlocked += OnAchievementUnlocked;
        AchievementManager.Instance.AchievementProgressChanged += OnProgressChanged;
    }

    private void OnAchievementUnlocked(string id, Achievement achievement)
    {
        GD.Print($"🏆 {achievement.DisplayName} unlocked!");

        // Show custom animation, play sound, etc.
        ShowCustomUnlockAnimation(achievement);

        // Track analytics
        AnalyticsManager.TrackAchievement(id);
    }

    private void OnProgressChanged(string id, int currentProgress)
    {
        var achievement = AchievementManager.Instance.GetAchievement(id);
        if (achievement != null && achievement.MaxProgress > 1)
        {
            float percentage = (float)currentProgress / achievement.MaxProgress * 100f;
            GD.Print($"{achievement.DisplayName}: {percentage:F1}% ({currentProgress}/{achievement.MaxProgress})");

            // Update UI progress bar
            UpdateProgressBar(id, percentage);
        }
    }

    public override void _ExitTree()
    {
        // Unsubscribe to prevent memory leaks
        if (AchievementManager.Instance != null)
        {
            AchievementManager.Instance.AchievementUnlocked -= OnAchievementUnlocked;
            AchievementManager.Instance.AchievementProgressChanged -= OnProgressChanged;
        }
    }
}
```

### Building an Achievement Screen

Display all achievements with unlock status:

```csharp
public partial class AchievementScreen : Control
{
    [Export] public PackedScene AchievementCardScene;
    [Export] public VBoxContainer AchievementContainer;

    public override void _Ready()
    {
        PopulateAchievements();
    }

    private void PopulateAchievements()
    {
        var achievements = AchievementManager.Instance.GetAllAchievements();

        foreach (var achievement in achievements)
        {
            // Skip hidden achievements that aren't unlocked
            if (achievement.Hidden && !achievement.IsUnlocked)
                continue;

            var card = AchievementCardScene.Instantiate<AchievementCard>();
            card.SetAchievement(achievement);
            AchievementContainer.AddChild(card);
        }
    }
}

public partial class AchievementCard : PanelContainer
{
    [Export] public TextureRect IconRect;
    [Export] public Label TitleLabel;
    [Export] public Label DescriptionLabel;
    [Export] public ProgressBar ProgressBar;

    public void SetAchievement(Achievement achievement)
    {
        TitleLabel.Text = achievement.DisplayName;
        DescriptionLabel.Text = achievement.Description;

        if (achievement.Icon != null)
        {
            IconRect.Texture = achievement.Icon;
        }

        // Show progress for progressive achievements
        if (achievement.MaxProgress > 1)
        {
            ProgressBar.Visible = true;
            ProgressBar.MaxValue = achievement.MaxProgress;
            ProgressBar.Value = achievement.CurrentProgress;
        }
        else
        {
            ProgressBar.Visible = false;
        }

        // Gray out locked achievements
        Modulate = achievement.IsUnlocked ? Colors.White : new Color(0.5f, 0.5f, 0.5f);
    }
}
```

### Checking Provider Status

Verify platform integrations are working:

```csharp
public partial class MainMenu : Control
{
    public override void _Ready()
    {
        CheckPlatformIntegrations();
    }

    private void CheckPlatformIntegrations()
    {
        var providers = AchievementManager.Instance.GetRegisteredProviders();

        GD.Print($"=== Achievement Providers ({providers.Count}) ===");
        foreach (var provider in providers)
        {
            var status = provider.IsAvailable ? "✓ Active" : "✗ Unavailable";
            GD.Print($"{provider.ProviderName}: {status}");
        }

        // Check specific platform
        var steamProvider = AchievementManager.Instance.GetProvider("Steam");
        if (steamProvider != null && steamProvider.IsAvailable)
        {
            GD.Print("Steam integration active!");
        }
    }
}
```

## Troubleshooting

### Plugin Not Appearing

**Problem:** "Godot Achievements" doesn't show up in the Plugins list.

**Solutions:**
- Verify `addons/godot_achievements/plugin.cfg` exists
- Check that `AchievementEditorPlugin.cs` is in `addons/godot_achievements/Editor/`
- Restart Godot editor
- Check console for C# compilation errors

### AchievementManager.Instance is Null

**Problem:** `AchievementManager.Instance` returns null when accessed.

**Solutions:**
- Verify the AchievementManager is added to autoloads (**Project → Project Settings → Autoload**)
- Ensure autoload is enabled (checkbox checked)
- Check that you're not accessing it before `_Ready()` is called
- Verify the scene contains an AchievementManager node

### Achievements Not Syncing to Platform

**Problem:** Achievements unlock locally but don't sync to Steam/Game Center/Google Play.

**Solutions:**
- Check provider is registered: `AchievementManager.Instance.GetRegisteredProviders()`
- Verify platform SDK is initialized (Steam must be running, user must be signed into Game Center, etc.)
- Check platform IDs are set in achievement database
- Look for error messages in console (search for `[Steam]`, `[GameCenter]`, `[GooglePlay]`)
- Verify platform-specific autoloads are added AFTER the main Achievements autoload

### Platform-Specific Code Not Compiling

**Problem:** Build fails with errors in Steam/iOS/Android provider code.

**Solutions:**
- Platform code uses preprocessor directives and only compiles for target platforms
- When building for Windows/PC, only Steam code compiles
- When building for iOS, only iOS code compiles
- For development on non-target platforms, wrap test code in the same directives:
  ```csharp
  #if GODOT_ANDROID
  // Android-specific test code
  #endif
  ```

### Toast Notifications Not Showing

**Problem:** Achievements unlock but no toast appears.

**Solutions:**
- Check **Show Toasts** is enabled in AchievementManager inspector
- Verify achievement is newly unlocked (toasts only show on first unlock)
- Check that you haven't disabled toasts in code: `AchievementManager.Instance.ShowToasts = true`
- Toast may be rendering behind other UI - check the scene tree

### Can't Find Achievement by ID

**Problem:** `GetAchievement("my_id")` returns null.

**Solutions:**
- Verify achievement ID spelling (case-sensitive)
- Check achievement exists in database: `GD.Load<AchievementDatabase>("res://achievements.tres")`
- Ensure database is assigned to AchievementManager
- Check for duplicate IDs in database

### Progress Not Saving Between Sessions

**Problem:** Achievement progress resets when game restarts.

**Solutions:**
- Local achievements save to `user://achievements.json` automatically
- Check file permissions for write access to user directory
- Verify `LocalAchievementProvider` is registered (it should be automatically)
- Check console for save/load errors
- Test by checking if file exists: `FileAccess.FileExists("user://achievements.json")`

## API Reference

### AchievementManager

```csharp
// Unlock achievement
await AchievementManager.Instance.Unlock(string achievementId);

// Set progress (0.0 to 1.0)
await AchievementManager.Instance.SetProgress(string achievementId, float progress);

// Get achievement by ID
Achievement achievement = AchievementManager.Instance.GetAchievement(string achievementId);

// Get all achievements
Achievement[] all = AchievementManager.Instance.GetAllAchievements();

// Get registered providers
IReadOnlyList<IAchievementProvider> providers = AchievementManager.Instance.GetRegisteredProviders();

// Get specific provider
IAchievementProvider steam = AchievementManager.Instance.GetProvider("Steam");
```

### Achievement

```csharp
public class Achievement : Resource
{
    public string Id { get; set; }
    public string DisplayName { get; set; }
    public string Description { get; set; }
    public Texture2D Icon { get; set; }
    public bool Hidden { get; set; }

    // Platform IDs
    public string SteamId { get; set; }
    public string GameCenterId { get; set; }
    public string GooglePlayId { get; set; }
    public string CustomPlatformIds { get; set; } // JSON

    // Runtime state
    public bool IsUnlocked { get; set; }
    public DateTime? UnlockedAt { get; set; }
    public float Progress { get; set; }

    // Helpers
    public string GetPlatformId(string platform);
    public void SetPlatformId(string platform, string id);
}
```

## Roadmap

- [x] Core achievement system
- [x] Local provider with JSON persistence
- [x] Achievement manager singleton
- [x] Default toast notifications
- [x] Editor plugin infrastructure
- [x] Steam provider implementation
- [x] iOS Game Center provider implementation
- [x] Android Google Play provider implementation
- [ ] Full editor UI implementation
- [ ] Godot Asset Library submission
- [ ] Sample project
- [ ] Video tutorials

## License

MIT License - see LICENSE file for details

## Contributing

Contributions welcome! Please open an issue or PR on GitHub.

## Support

For issues and questions, please use the GitHub issue tracker.
