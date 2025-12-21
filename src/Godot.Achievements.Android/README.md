# Godot.Achievements.Android

Google Play Games achievement provider for Godot.Achievements.NET.

## Requirements

- Godot.Achievements.Core
- Android platform target
- Google Play Console account
- Google Play Games Services configured

## Installation

### 1. Add to your project

```xml
<ItemGroup>
  <PackageReference Include="Godot.Achievements.Core" Version="1.0.0" />
  <PackageReference Include="Godot.Achievements.Android" Version="1.0.0"
                    Condition="'$(GodotTargetPlatform)' == 'android'" />
</ItemGroup>
```

### 2. Configure Google Play Games Services

1. Go to https://play.google.com/console
2. Select your app (or create one)
3. Go to **Grow → Play Games Services → Setup and management → Configuration**
4. Create a new game or link existing
5. Note your **Application ID** (e.g., `123456789012`)

### 3. Add Achievements in Play Console

1. In Play Console, go to **Achievements**
2. Click **Add Achievement**
3. Configure:
   - **Name**: Display name (e.g., "First Blood")
   - **Description**: What the player needs to do
   - **Icon**: 512x512 PNG
   - **Initial State**: Hidden or Visible
   - **Type**: Standard or Incremental
   - **Points**: 5-1000 (increment of 5)
4. Note the **Achievement ID** (e.g., `CgkI...`)

### 4. Configure Android Export

In Godot export settings:

1. **Project → Export → Android**
2. Add to **Custom Template → AndroidManifest.xml**:

```xml
<meta-data
    android:name="com.google.android.gms.games.APP_ID"
    android:value="@string/app_id" />
```

3. Add to **res/values/strings.xml**:

```xml
<resources>
    <string name="app_id">123456789012</string>
</resources>
```

### 5. Setup Autoload

1. Create a scene with `GooglePlayAchievementAutoload` node
2. Save as `res://addons/godot_achievements_android/GooglePlayAutoload.tscn`
3. Add to Project → Project Settings → Autoload:
   - **Path**: `res://addons/godot_achievements_android/GooglePlayAutoload.tscn`
   - **Name**: `GooglePlayAchievements`
   - **Enable**: ✓

**Important**: Add this AFTER the `Achievements` autoload.

## Usage

### Configure Google Play IDs in Editor

In the Achievements editor dock, set the Google Play ID for each achievement:

```
Achievement: "First Blood"
ID: first_kill
Google Play ID: CgkI7ea...first_kill    <-- Set this (from Play Console)
```

### Unlock Achievements

The Google Play provider automatically syncs when you use the standard API:

```csharp
// This unlocks locally AND on Google Play Games
await AchievementManager.Instance.Unlock("first_kill");
```

### Check Sign-In Status

```csharp
public override void _Ready()
{
    var playProvider = AchievementManager.Instance.GetProvider("Google Play Games");
    if (playProvider != null && playProvider.IsAvailable)
    {
        GD.Print("Signed into Google Play Games!");
    }
    else
    {
        GD.Print("Not signed in - achievements will sync when user signs in");
    }
}
```

## Google Play Achievement Types

### Standard Achievements
One-time unlock:

```csharp
await AchievementManager.Instance.Unlock("first_kill");
```

### Incremental Achievements
Progress-based (e.g., "Kill 100 enemies"):

1. In Play Console, set:
   - **Type**: Incremental
   - **Steps to unlock**: 100

2. In your game:
```csharp
// Update progress (0.0 to 1.0)
await AchievementManager.Instance.SetProgress("kill_100_enemies", 0.5f);

// Google Play will show "50/100 enemies killed"
```

## Achievement ID Format

Google Play generates achievement IDs automatically:
- Format: `CgkI[random_string]`
- Example: `CgkI7ea1q6IOEAIQBw`

**Important**: Copy the exact ID from Play Console.

## Testing

### Test with Test Accounts

1. In Play Console, go to **Testing → License testing**
2. Add test Google accounts
3. Build and install your app (debug or internal test track)
4. Sign in with test account
5. Achievements will work in test mode

### View Achievements in App

Show Google Play Games UI:

```csharp
// Real implementation with Play Games plugin:
// Social.ShowAchievementsUI();
```

### Reset Achievements (Testing)

Use Play Games Services API:

```csharp
// Real implementation:
// PlayGamesPlatform.Instance.ResetAllAchievements((success) =>
// {
//     if (success)
//     {
//         GD.Print("Achievements reset");
//     }
// });
```

**Note**: Only works in test mode, not production.

## Sign-In Flow

Google Play Games handles authentication automatically:

1. App launches
2. Play Games checks for signed-in user
3. If signed in: Achievements sync immediately
4. If not signed in: Achievements queue locally until sign-in
5. User can sign in via Play Games UI

Silent sign-in example:

```csharp
public partial class PlayGamesHandler : Node
{
    public override void _Ready()
    {
        // Listen for authentication
        AchievementManager.Instance.ProviderRegistered += OnProviderRegistered;
    }

    private void OnProviderRegistered(string providerName)
    {
        if (providerName == "Google Play Games")
        {
            var playProvider = AchievementManager.Instance.GetProvider("Google Play Games");
            if (playProvider?.IsAvailable == true)
            {
                GD.Print("Connected to Google Play Games!");
            }
        }
    }

    private void ShowSignInButton()
    {
        // Real implementation:
        // Social.localUser.Authenticate((success) => { ... });
    }
}
```

## Troubleshooting

### "Google Play Games not available"

Check:
- [ ] Running on Android device with Play Games app installed
- [ ] App signed with correct keystore (release or debug)
- [ ] Application ID matches in AndroidManifest.xml
- [ ] Google Play Games Services is configured in Play Console

### Achievements not unlocking

Check:
- [ ] Achievement IDs match exactly from Play Console
- [ ] App is published to Internal/Closed/Open testing track
- [ ] Using a test account added in Play Console
- [ ] User is signed into Google Play Games

### "Sign-in failed"

- Verify SHA-1 certificate fingerprint in Play Console matches your keystore
- Check OAuth client is configured
- Ensure Play Games Services is enabled for your app

### Get SHA-1 fingerprint

For debug keystore:
```bash
keytool -list -v -keystore ~/.android/debug.keystore -alias androiddebugkey -storepass android -keypass android
```

For release keystore:
```bash
keytool -list -v -keystore /path/to/release.keystore -alias your_alias
```

Add SHA-1 to Play Console:
**Setup and management → Configuration → OAuth → Add SHA-1**

## Platform Detection

The provider only compiles and runs on Android:

```csharp
#if GODOT_ANDROID
// Android Google Play Games code here
#endif
```

## Required Android Plugin

This provider requires Google Play Games Services integration:
- Unity plugin: `com.google.play.games`
- Or custom Godot Android plugin

You'll need to integrate via Godot's Android plugin system.

## API Reference

See `IAchievementProvider` interface in Godot.Achievements.Core.

## License

MIT License
