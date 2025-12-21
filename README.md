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

### From Source (Development)

1. Clone this repository
2. Add reference to your Godot project's `.csproj`:

```xml
<ItemGroup>
  <ProjectReference Include="path/to/Godot.Achievements.NET/src/Godot.Achievements.Core/Godot.Achievements.Core.csproj" />
</ItemGroup>
```

### From NuGet (Coming Soon)

```xml
<ItemGroup>
  <PackageReference Include="Godot.Achievements.Core" Version="1.0.0" />
  <!-- Platform-specific packages -->
  <PackageReference Include="Godot.Achievements.Steam" Version="1.0.0" Condition="'$(GodotTargetPlatform)' == 'windows'" />
</ItemGroup>
```

## Quick Start

### 1. Enable the Plugin

In Godot editor:
1. Go to **Project → Project Settings → Plugins**
2. Enable "Godot Achievements"

### 2. Create Achievement Database

1. Open the **Achievements** dock (bottom panel)
2. Click **Select Database...**
3. Create a new `AchievementDatabase` resource and save as `res://achievements.tres`

### 3. Add Achievements

In the Achievements dock:
1. Click **Add** to create a new achievement
2. Fill in:
   - **ID**: Unique identifier (e.g., `first_kill`)
   - **Display Name**: User-friendly name (e.g., "First Blood")
   - **Description**: What the player needs to do
   - **Platform IDs**: Steam ID, Game Center ID, etc.
3. Click **Save Changes**

### 4. Setup Autoload

Add the AchievementManager to your project autoloads:

1. Create a new scene with an `AchievementManager` node
2. Assign your `achievements.tres` database to it
3. Save as `res://addons/godot_achievements/AchievementManager.tscn`
4. Go to **Project → Project Settings → Autoload**
5. Add autoload:
   - **Path**: `res://addons/godot_achievements/AchievementManager.tscn`
   - **Name**: `Achievements`
   - **Enable**: ✓

### 5. Unlock Achievements in Code

```csharp
using Godot.Achievements.Core;

public partial class Player : CharacterBody2D
{
    public async void OnEnemyKilled()
    {
        // Unlock achievement
        await AchievementManager.Instance.Unlock("first_kill");
    }

    public async void OnProgressMade(int enemiesKilled)
    {
        // Update progressive achievement
        float progress = enemiesKilled / 100f; // 100 enemies total
        await AchievementManager.Instance.SetProgress("kill_100_enemies", progress);
    }

    public void DisplayAllAchievements()
    {
        var achievements = AchievementManager.Instance.GetAllAchievements();
        foreach (var achievement in achievements)
        {
            GD.Print($"{achievement.DisplayName}: {(achievement.IsUnlocked ? "✓" : "✗")}");
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
- [ ] Full editor UI implementation
- [ ] Steam provider package
- [ ] iOS Game Center provider package
- [ ] Android Google Play provider package
- [ ] NuGet packaging
- [ ] Sample project

## License

MIT License - see LICENSE file for details

## Contributing

Contributions welcome! Please open an issue or PR on GitHub.

## Support

For issues and questions, please use the GitHub issue tracker.
