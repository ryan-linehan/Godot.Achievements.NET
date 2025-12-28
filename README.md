# Godot.Achievements.NET

A cross-platform achievement system for Godot 4+ with C#/.NET support. Abstracts platform-specific achievement APIs behind a unified interface, making it easy to ship to Steam, iOS, and Android with a single codebase.

## Features

- **Cross-platform abstraction** - Single API for Steam, Game Center, Google Play, and custom providers
- **Visual editor** - Manage achievements from a dedicated editor dock
- **Built-in toast notifications** - Configurable unlock notifications with custom scene support
- **Local-first persistence** - Achievements saved locally, synced to platforms
- **Progress tracking** - Support for incremental/progressive achievements
- **Extensible** - Add custom providers by implementing a simple interface

## Supported Platforms

| Platform | Provider | Required Addon |
|----------|----------|----------------|
| **Steam** | `SteamAchievementProvider` | [GodotSteam](https://github.com/GodotSteam/GodotSteam) or Steamworks.NET |
| **iOS Game Center** | `GameCenterAchievementProvider` | [GodotApplePlugins](https://github.com/migueldeicaza/GodotApplePlugins) |
| **Google Play** | `GooglePlayAchievementProvider` | [godot-play-game-services](https://github.com/Suspended/godot-play-game-services) |
| **Local/Offline** | `LocalAchievementProvider` | Built-in |

## Installation

1. Copy the `Demo/addons/Godot.Achievements.Net` folder to your project's `addons/` directory
2. Enable the plugin in **Project > Project Settings > Plugins**
3. The `Achievements` and `AchievementToasts` autoloads are registered automatically

## Quick Start

### 1. Create an Achievement Database

Use the **Achievements** dock in the bottom panel:

1. Click **New Database** or **Load Database**
2. Click **Add Achievement**
3. Fill in the ID, display name, description, and optional icon
4. Set platform-specific IDs (Steam ID, Game Center ID, Google Play ID)
5. Click **Save**

### 2. Unlock Achievements in Code

```csharp
// Get the manager (registered as autoload)
var achievements = GetNode<AchievementManager>("/root/Achievements");

// Unlock an achievement
achievements.Unlock("first_blood");

// For progressive achievements
achievements.IncrementProgress("kill_100_enemies", 1);

// Or set absolute progress
achievements.SetProgress("collect_coins", 50);
```

### 3. Listen for Events

```csharp
var achievements = GetNode<AchievementManager>("/root/Achievements");

achievements.AchievementUnlocked += (id, achievement) => {
    GD.Print($"Unlocked: {achievement.DisplayName}");
};
```

## Architecture

```
AchievementManager (autoload)
    |
    +-- LocalAchievementProvider (always active, persists to user://)
    |
    +-- Platform Providers (conditional)
        +-- SteamAchievementProvider (GODOT_PC)
        +-- GameCenterAchievementProvider (GODOT_IOS)
        +-- GooglePlayAchievementProvider (GODOT_ANDROID)
```

**Flow:**
1. `Unlock("achievement_id")` called
2. Local provider saves to `user://achievements.json`
3. Platform providers sync to their respective services
4. `AchievementUnlocked` signal emitted
5. Toast notification displayed (if enabled)

## Adding a Custom Provider

Implement `IAchievementProvider` or extend `AchievementProviderBase`:

```csharp
public class MyPlatformProvider : AchievementProviderBase
{
    public override string ProviderName => "MyPlatform";
    public override bool IsAvailable => MyPlatformSDK.IsInitialized;

    public override void UnlockAchievement(string achievementId)
    {
        var achievement = _database.GetById(achievementId);
        var platformId = achievement?.MyPlatformId;
        MyPlatformSDK.Unlock(platformId);
    }

    // Implement other required methods...
}
```

## Project Settings

Configure in **Project > Project Settings > Addons > Achievements**:

| Setting | Description |
|---------|-------------|
| `database_path` | Path to achievement database resource |
| `toast/scene_path` | Custom toast scene (empty = disabled) |
| `toast/position` | Screen position (TopLeft, TopRight, etc.) |
| `toast/display_duration` | Toast display time in seconds |
| `toast/unlock_sound` | Sound to play on unlock |
| `platforms/steam_enabled` | Enable Steam provider |
| `platforms/gamecenter_enabled` | Enable Game Center provider |
| `platforms/googleplay_enabled` | Enable Google Play provider |

## Project Structure

```
addons/Godot.Achievements.Net/
+-- Core/
|   +-- Achievement.cs           # Achievement resource
|   +-- AchievementDatabase.cs   # Collection of achievements
|   +-- AchievementManager.cs    # Main API (autoload)
|   +-- AchievementLogger.cs     # Logging utility
|   +-- AchievementSettings.cs   # Project settings keys
+-- Providers/
|   +-- IAchievementProvider.cs  # Provider interface
|   +-- AchievementProviderBase.cs
|   +-- Local/
|   +-- Steamworks/
|   +-- GameCenter/
|   +-- GooglePlay/
+-- Editor/
|   +-- AchievementEditorDock.cs # Main editor UI
|   +-- ...
+-- Toast/
|   +-- AchievementToastContainer.cs
|   +-- AchievementToastItem.cs
+-- AchievementPlugin.cs         # Plugin entry point
```

## Documentation

- [Plugin README](Demo/addons/Godot.Achievements.Net/README.md) - Detailed usage guide
- [Integration Guide](Demo/INTEGRATION_GUIDE.md) - Platform SDK setup
- [Contributing](Demo/CONTRIBUTING.md) - Development setup
- [Changelog](Demo/CHANGELOG.md) - Version history

## License

MIT License - see LICENSE file for details.
