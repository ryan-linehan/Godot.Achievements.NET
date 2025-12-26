# Platform Integration Guide

Complete guide for integrating Godot.Achievements.NET with platform-specific SDKs.

## Overview

Godot.Achievements.NET uses a plugin architecture that works with official platform SDKs:

| Platform | Plugin Repository | Maintainer |
|----------|------------------|------------|
| **Steam** | [Godot.Steamworks.NET](https://github.com/ryan-linehan/Godot.Steamworks.NET) | ryan-linehan |
| **iOS** | [GodotApplePlugins](https://github.com/migueldeicaza/GodotApplePlugins) | Miguel de Icaza |
| **Android** | [godot-play-game-services](https://github.com/godot-sdk-integrations/godot-play-game-services) | Godot SDK Integrations |

## Installation Order

1. ✅ Copy core plugin (`addons/godot_achievements`) to your project
2. ✅ Install platform SDKs (Steamworks, GameKit, Play Games Services)
3. ✅ Copy platform provider folders (optional, as needed)
4. ✅ Configure autoloads
5. ✅ Configure platform IDs in achievement database

## Steam Integration

### 1. Install Godot.Steamworks.NET

```bash
# Clone the repository
git clone https://github.com/ryan-linehan/Godot.Steamworks.NET.git

# Or add as submodule
git submodule add https://github.com/ryan-linehan/Godot.Steamworks.NET.git addons/steamworks
```

Follow the setup instructions in the Godot.Steamworks.NET repository.

### 2. Copy Steam Achievement Provider

```bash
# Copy the Steam provider folder to your project
cp -r path/to/Godot.Achievements.NET/addons/godot_achievements_steam your-project/addons/
```

### 3. Configure Autoload

1. Create scene with `SteamAchievementAutoload` node
2. Save as `res://addons/godot_achievements_steam/SteamAutoload.tscn`
3. Add to autoloads **AFTER** `Achievements`:

```
Project → Project Settings → Autoload
- Path: res://addons/godot_achievements_steam/SteamAutoload.tscn
- Name: SteamAchievements
```

### 4. Configure Steam App ID

Create `steam_appid.txt`:
```
480
```

### 5. Set Achievement IDs

In achievement editor, set Steam IDs:
```
Achievement: First Kill
Steam ID: ACH_FIRST_KILL
```

## iOS Integration

### 1. Install GodotApplePlugins (GameKit)

```bash
# Clone the repository
git clone https://github.com/migueldeicaza/GodotApplePlugins.git

# Copy GameKit plugin to your project
cp -r GodotApplePlugins/GameKit addons/
```

Enable in **Project → Project Settings → Plugins**.

### 2. Copy iOS Achievement Provider

```bash
# Copy the iOS provider folder to your project
cp -r path/to/Godot.Achievements.NET/addons/godot_achievements_ios your-project/addons/
```

### 3. Configure Autoload

1. Create scene with `GameCenterAchievementAutoload` node
2. Save as `res://addons/godot_achievements_ios/GameCenterAutoload.tscn`
3. Add to autoloads **AFTER** `Achievements`:

```
Project → Project Settings → Autoload
- Path: res://addons/godot_achievements_ios/GameCenterAutoload.tscn
- Name: GameCenterAchievements
```

### 4. Configure Game Center Entitlements

In Godot iOS export settings, add to **Additional Plist Content**:

```xml
<key>UIRequiredDeviceCapabilities</key>
<array>
    <string>gamekit</string>
</array>
```

### 5. Set Achievement IDs

In App Store Connect, create achievements and note IDs.

In achievement editor, set Game Center IDs:
```
Achievement: First Kill
Game Center ID: com.yourcompany.yourgame.first_kill
```

## Android Integration

### 1. Install godot-play-game-services

Download from [releases](https://github.com/godot-sdk-integrations/godot-play-game-services/releases):

```bash
# Extract to your project
unzip godot-play-game-services-v1.0.0.zip -d addons/
```

Enable in **Project → Project Settings → Plugins**.

**Important:** The godot-play-game-services plugin provides:
- `GodotPlayGameServices` singleton - Main plugin entry point
- `SignInClient` autoload - Handles player authentication
- `AchievementsClient` autoload - Handles achievement operations

### 2. Configure Google Play Console

1. Create a new game in [Google Play Console](https://play.google.com/console)
2. Enable Play Games Services for your game
3. Create achievements and note their IDs (format: `CgkI...`)
4. Configure OAuth 2.0 credentials:
   - Add your app's package name
   - Add SHA-1 certificate fingerprint (debug and release)

### 3. Configure Plugin in Godot

The godot-play-game-services plugin adds an editor dock to configure your Game ID:

1. Open the **Play Game Services** dock in the editor
2. Enter your Game ID from Google Play Console
3. Save the project

### 4. Enable Google Play Provider

In **Project → Project Settings**:
1. Navigate to `Addons → Achievements → Platforms`
2. Enable **Google Play Enabled**

The `GooglePlayAchievementProvider` will automatically initialize when:
- Running on Android (`GODOT_ANDROID` is defined)
- The setting is enabled
- The godot-play-game-services plugin is installed

### 5. Set Achievement IDs

In Google Play Console, create achievements and copy IDs.

In achievement editor, set Google Play IDs:
```
Achievement: First Kill
Google Play ID: CgkI7ea1q6IOEAIQBw
```

### 6. Authentication

The godot-play-game-services plugin handles authentication automatically. The `GooglePlayAchievementProvider`:
- Initializes the plugin on startup
- Listens for authentication state changes
- Only syncs achievements when the user is authenticated

If you need manual sign-in control, you can access the provider directly:

```csharp
var googlePlayProvider = AchievementManager.Instance.GetProvider("Google Play Games")
    as GooglePlayAchievementProvider;

if (googlePlayProvider != null)
{
    // Check authentication status
    if (!googlePlayProvider.IsAuthenticated)
    {
        // Request manual sign-in
        googlePlayProvider.SignIn();
    }

    // Show Google Play achievements UI
    googlePlayProvider.ShowAchievementsUI();
}
```

### 7. Incremental Achievements

For achievements that track progress (e.g., "Kill 100 enemies"):

1. Create an **Incremental** achievement in Google Play Console
2. Set the number of steps required (e.g., 100)
3. Use `SetProgress` in your code - the provider handles increment calculation:

```csharp
// The provider calculates the increment automatically
await AchievementManager.Instance.SetProgress("kill_enemies", currentKillCount);
```

**Note:** Google Play does not support decreasing progress. If you try to set a lower value, the provider will log a warning and skip the operation.

### 8. Hidden Achievements

To reveal a hidden achievement before unlocking:

```csharp
var googlePlayProvider = AchievementManager.Instance.GetProvider("Google Play Games")
    as GooglePlayAchievementProvider;

// Reveal the achievement (makes it visible to the player)
await googlePlayProvider?.RevealAchievementAsync("secret_achievement");

// Later, unlock it
await AchievementManager.Instance.Unlock("secret_achievement");
```

**Note:** Unlocking a hidden achievement automatically reveals it.

### 9. Limitations

- **Reset not supported:** Google Play Games does not support resetting achievements in production. Use the Play Games Console for testing.
- **Progress only increases:** You cannot decrease achievement progress.
- **Requires authentication:** Achievements only sync when the user is signed in to Google Play Games.

## Complete Autoload Configuration

Final autoload order in **Project → Project Settings → Autoload**:

```
1. Achievements (res://addons/godot_achievements/AchievementManager.tscn)
2. SteamAchievements (res://addons/godot_achievements_steam/SteamAutoload.tscn) [if using Steam]
3. GameCenterAchievements (res://addons/godot_achievements_ios/GameCenterAutoload.tscn) [if using iOS]
4. GooglePlayAchievements (res://addons/godot_achievements_android/GooglePlayAutoload.tscn) [if using Android]
```

**Important:** `Achievements` must load first!

## Multi-Platform Project Setup

For a project targeting all platforms, copy all provider folders:

```bash
# Copy core plugin
cp -r path/to/Godot.Achievements.NET/addons/godot_achievements your-project/addons/

# Copy all platform providers
cp -r path/to/Godot.Achievements.NET/addons/godot_achievements_steam your-project/addons/
cp -r path/to/Godot.Achievements.NET/addons/godot_achievements_ios your-project/addons/
cp -r path/to/Godot.Achievements.NET/addons/godot_achievements_android your-project/addons/
```

Thanks to preprocessor directives, platform-specific code only compiles for the target platform:
- Steam provider only compiles on PC platforms (`GODOT_PC`, `GODOT_WINDOWS`, etc.)
- iOS provider only compiles on iOS (`GODOT_IOS`)
- Android provider only compiles on Android (`GODOT_ANDROID`)

## Achievement Configuration

Example achievement with all platform IDs:

```
ID: first_kill
Display Name: First Blood
Description: Defeat your first enemy

Platform IDs:
- Steam ID: ACH_FIRST_KILL
- Game Center ID: com.example.mygame.first_kill
- Google Play ID: CgkI7ea1q6IOEAIQBw
```

The system automatically syncs to all available platforms:

```csharp
// Unlocks on Steam, Game Center, AND Google Play (if available)
await AchievementManager.Instance.Unlock("first_kill");
```

## Verification

Check all providers are registered:

```csharp
public override void _Ready()
{
    var providers = AchievementManager.Instance.GetRegisteredProviders();
    foreach (var provider in providers)
    {
        GD.Print($"Provider: {provider.ProviderName}, Available: {provider.IsAvailable}");
    }
}
```

Expected output:
```
Provider: Local, Available: true
Provider: Steam, Available: true
Provider: Game Center, Available: true
Provider: Google Play Games, Available: true
```

## Troubleshooting

### Provider not registering

- Check autoload order (`Achievements` must be first)
- Verify platform SDK is installed and enabled
- Check platform-specific configuration (App IDs, entitlements, etc.)

### Achievements not syncing

- Verify platform IDs are set in achievement database
- Check platform SDK authentication (user signed in?)
- Look for errors in Godot console
- Check retry queue: `AchievementManager.Instance` logs pending syncs

### Build errors

- Ensure conditional compilation is correct
- Platform packages should only build for their target platform
- Check SDK dependencies are installed

## Platform SDK Documentation

- **Steamworks:** https://github.com/ryan-linehan/Godot.Steamworks.NET
- **GameKit:** https://github.com/migueldeicaza/GodotApplePlugins/tree/main/GameKit
- **Play Games:** https://github.com/godot-sdk-integrations/godot-play-game-services

## Support

For integration issues:
- Check platform SDK documentation first
- Review this guide and platform-specific READMEs
- Open an issue: https://github.com/ryan-linehan/Godot.Achievements.NET/issues
