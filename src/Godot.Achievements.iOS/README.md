# Godot.Achievements.iOS

iOS Game Center achievement provider for Godot.Achievements.NET.

## Requirements

- Godot.Achievements.Core
- iOS platform target
- Apple Developer account
- Game Center entitlements configured

## Installation

### 1. Add to your project

```xml
<ItemGroup>
  <PackageReference Include="Godot.Achievements.Core" Version="1.0.0" />
  <PackageReference Include="Godot.Achievements.iOS" Version="1.0.0"
                    Condition="'$(GodotTargetPlatform)' == 'ios'" />
</ItemGroup>
```

### 2. Configure Game Center in App Store Connect

1. Go to https://appstoreconnect.apple.com
2. Select your app
3. Go to **Services → Game Center**
4. Add achievements with:
   - **Reference Name**: Internal name
   - **Achievement ID**: e.g., `com.yourcompany.yourgame.first_kill`
   - **Points**: 10-100
   - **Hidden**: Yes/No

### 3. Add Game Center Entitlement

In your Godot export settings (iOS):

1. Go to **Project → Export → iOS**
2. Add to **Additional Plist Content**:

```xml
<key>UIRequiredDeviceCapabilities</key>
<array>
    <string>gamekit</string>
</array>
```

### 4. Setup Autoload

1. Create a scene with `GameCenterAchievementAutoload` node
2. Save as `res://addons/godot_achievements_ios/GameCenterAutoload.tscn`
3. Add to Project → Project Settings → Autoload:
   - **Path**: `res://addons/godot_achievements_ios/GameCenterAutoload.tscn`
   - **Name**: `GameCenterAchievements`
   - **Enable**: ✓

**Important**: Add this AFTER the `Achievements` autoload.

## Usage

### Configure Game Center IDs in Editor

In the Achievements editor dock, set the Game Center ID for each achievement:

```
Achievement: "First Blood"
ID: first_kill
Game Center ID: com.yourcompany.yourgame.first_kill    <-- Set this
```

### Unlock Achievements

The Game Center provider automatically syncs when you use the standard API:

```csharp
// This unlocks locally AND on Game Center
await AchievementManager.Instance.Unlock("first_kill");
```

### Check Authentication

```csharp
public override void _Ready()
{
    var gcProvider = AchievementManager.Instance.GetProvider("Game Center");
    if (gcProvider != null && gcProvider.IsAvailable)
    {
        GD.Print("Game Center is authenticated!");
    }
    else
    {
        GD.Print("Game Center not available - user may need to sign in");
    }
}
```

## Game Center Achievement ID Format

Apple recommends reverse-DNS format:
- `com.yourcompany.yourgame.achievement_name`

Examples:
- `com.example.mygame.first_kill`
- `com.example.mygame.boss_defeated`
- `com.example.mygame.speedrun_complete`

## Progressive Achievements

Game Center supports progress tracking:

```csharp
// Update progress (0.0 to 1.0)
await AchievementManager.Instance.SetProgress("kill_100_enemies", 0.5f);

// Game Center will show "50% complete"
// Achievement will unlock when progress reaches 1.0
```

## Testing

### Test with Sandbox Account

1. In iOS Settings → Game Center, sign out
2. Run your app
3. When prompted, sign in with a Sandbox Apple ID
4. Create a sandbox account at: https://appstoreconnect.apple.com/access/testers

### View Achievements

Show Game Center UI in your game:

```csharp
// Real implementation with iOS bindings:
// var gcViewController = new GKGameCenterViewController();
// gcViewController.ViewState = GKGameCenterViewControllerState.Achievements;
// PresentViewController(gcViewController, true, null);
```

### Reset Achievements (Testing)

```csharp
// Real implementation with iOS bindings:
// GKAchievement.ResetAchievements((error) =>
// {
//     if (error != null)
//     {
//         GD.PushError($"Failed to reset achievements: {error}");
//     }
// });
```

## Authentication Flow

Game Center automatically prompts for authentication:

1. App launches
2. Game Center checks if user is signed in
3. If not signed in, shows sign-in banner
4. User taps banner to authenticate
5. Once authenticated, `IsAvailable` returns `true`

Handle authentication in your game:

```csharp
public partial class GameCenterHandler : Node
{
    public override void _Ready()
    {
        // Listen for authentication changes
        AchievementManager.Instance.ProviderRegistered += OnProviderRegistered;
    }

    private void OnProviderRegistered(string providerName)
    {
        if (providerName == "Game Center")
        {
            var gcProvider = AchievementManager.Instance.GetProvider("Game Center");
            if (gcProvider?.IsAvailable == true)
            {
                GD.Print("Welcome back! Game Center connected.");
            }
        }
    }
}
```

## Troubleshooting

### Game Center not available

Check:
- [ ] Running on actual iOS device (not simulator without Game Center)
- [ ] User is signed into Game Center in iOS Settings
- [ ] Game Center entitlement is configured
- [ ] App ID matches in App Store Connect

### Achievements not appearing in Game Center

Check:
- [ ] Achievement IDs match exactly in App Store Connect
- [ ] Achievements are published (not in draft)
- [ ] Using correct bundle identifier
- [ ] Sandbox account is configured (for testing)

### "Game Center Unavailable" error

- Use a real iOS device (some simulators don't support Game Center)
- Sign into Game Center in iOS Settings
- Check network connection

## Platform Detection

The provider only compiles and runs on iOS:

```csharp
#if GODOT_IOS
// iOS Game Center code here
#endif
```

## Required iOS Bindings

This provider requires iOS platform bindings for:
- `GKLocalPlayer` (authentication)
- `GKAchievement` (achievement reporting)
- `GKGameCenterViewController` (UI)

You'll need to integrate these via Godot's iOS plugin system or a third-party binding library.

## API Reference

See `IAchievementProvider` interface in Godot.Achievements.Core.

## License

MIT License
