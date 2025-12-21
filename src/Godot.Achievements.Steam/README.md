# Godot.Achievements.Steam

Steam achievement provider for Godot.Achievements.NET using Steamworks.NET.

## Requirements

- Godot.Achievements.Core
- Steamworks.NET (install separately)
- Steam account with valid App ID
- `steam_appid.txt` in your project root

## Installation

### 1. Install Steamworks.NET

Follow the Steamworks.NET installation guide for Godot 4:
- https://github.com/rlabrecque/Steamworks.NET

### 2. Add to your project

```xml
<ItemGroup>
  <PackageReference Include="Godot.Achievements.Core" Version="1.0.0" />
  <PackageReference Include="Godot.Achievements.Steam" Version="1.0.0"
                    Condition="'$(GodotTargetPlatform)' == 'windows' OR '$(Configuration)' == 'Debug'" />
  <PackageReference Include="Steamworks.NET" Version="20.2.0" />
</ItemGroup>
```

### 3. Configure Steam App ID

Create `steam_appid.txt` in your project root with your Steam App ID:
```
480
```

### 4. Setup Autoload

1. Create a scene with `SteamAchievementAutoload` node
2. Save as `res://addons/godot_achievements_steam/SteamAutoload.tscn`
3. Add to Project → Project Settings → Autoload:
   - **Path**: `res://addons/godot_achievements_steam/SteamAutoload.tscn`
   - **Name**: `SteamAchievements`
   - **Enable**: ✓

**Important**: Add this AFTER the `Achievements` autoload.

## Usage

### Configure Steam IDs in Editor

In the Achievements editor dock, set the Steam ID for each achievement:

```
Achievement: "First Blood"
ID: first_kill
Steam ID: ACH_FIRST_KILL    <-- Set this
```

### Unlock Achievements

The Steam provider automatically syncs when you use the standard API:

```csharp
// This unlocks locally AND on Steam
await AchievementManager.Instance.Unlock("first_kill");
```

### Verify Steam Integration

```csharp
public override void _Ready()
{
    var providers = AchievementManager.Instance.GetRegisteredProviders();
    foreach (var provider in providers)
    {
        GD.Print($"Provider: {provider.ProviderName}, Available: {provider.IsAvailable}");
    }

    // Should show:
    // Provider: Local, Available: true
    // Provider: Steam, Available: true  <-- If Steam is running
}
```

## Steam Achievement Naming

Steam achievement IDs typically use:
- ALL_CAPS_WITH_UNDERSCORES
- Prefix like `ACH_` or `ACHIEVEMENT_`

Examples:
- `ACH_FIRST_KILL`
- `ACH_BOSS_DEFEATED`
- `ACH_SPEEDRUN_COMPLETE`

## Progressive Achievements (Stats-Based)

For progressive achievements (e.g., "Kill 100 enemies"), Steam uses stats:

1. In Steamworks Partner portal, create a stat:
   - Stat name: `ENEMIES_KILLED`
   - Associated achievement: `ACH_KILL_100` (unlocks at 100)

2. In your game:
```csharp
// Update progress (Steam will auto-unlock at 100%)
await AchievementManager.Instance.SetProgress("kill_100_enemies", enemiesKilled / 100f);
```

## Troubleshooting

### Steam provider not available

Check:
- [ ] Steam client is running
- [ ] `steam_appid.txt` exists with valid App ID
- [ ] Steamworks.NET is properly installed
- [ ] You're running on a supported platform (Windows/Linux/macOS)

### Achievements not unlocking on Steam

Check:
- [ ] Steam IDs are correctly configured in achievement database
- [ ] Achievement IDs match exactly in Steamworks Partner portal
- [ ] `SteamUserStats.StoreStats()` is being called
- [ ] Steam overlay is working (Shift+Tab)

## Testing

### Test with Steam's Test App (480)

Use App ID `480` (Spacewar) for testing before you have your own App ID:

```
// steam_appid.txt
480
```

### Reset Achievements

In Steam:
1. Right-click game in library
2. Properties → Local Files → Browse
3. Delete `steam_userdata` folder
4. Or use: `SteamUserStats.ResetAllStats(true)`

## Platform Detection

The provider only compiles and runs on desktop platforms:

```csharp
#if GODOT_PC || GODOT_WINDOWS || GODOT_LINUX || GODOT_MACOS
// Steam code here
#endif
```

## API Reference

See `IAchievementProvider` interface in Godot.Achievements.Core.

## License

MIT License
