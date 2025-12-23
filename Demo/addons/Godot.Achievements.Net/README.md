# Godot.Achievements.NET

A cross-platform achievement system for Godot 4 with C#/.NET support.

## Features

- Visual editor for managing achievements
- Built-in toast notification system
- Support for multiple platforms (Steam, Google Play, Game Center)
- Local achievement persistence
- Incremental/progress-based achievements

## Installation

1. Copy the `addons/Godot.Achievements.Net` folder to your project's `addons` directory
2. Enable the plugin in Project > Project Settings > Plugins
3. The `Achievements` and `AchievementToasts` autoloads will be registered automatically

## Basic Usage

### Unlocking Achievements

```csharp
// Get the achievement manager
var achievements = GetNode<AchievementManager>("/root/Achievements");

// Unlock an achievement by its internal ID
await achievements.UnlockAsync("first_kill");

// For incremental achievements, set progress
await achievements.SetProgressAsync("kill_100_enemies", 50);

// Or increment progress
await achievements.IncrementProgressAsync("kill_100_enemies", 1);
```

### Listening for Achievement Events

```csharp
var achievements = GetNode<AchievementManager>("/root/Achievements");

achievements.AchievementUnlocked += (id, achievement) => {
    GD.Print($"Achievement unlocked: {achievement.DisplayName}");
};

achievements.AchievementProgressChanged += (id, current, max) => {
    GD.Print($"Progress: {current}/{max}");
};
```

## Toast Notification System

The plugin includes a configurable toast notification system that displays achievement unlocks.

### Project Settings

Configure toast behavior in **Project > Project Settings > General > Addons > Achievements > Toast**:

| Setting | Description | Default |
|---------|-------------|---------|
| `scene_path` | Path to the toast scene. Set to empty to disable toasts entirely. | Built-in toast scene |
| `position` | Screen position for toasts | TopRight |
| `display_duration` | How long each toast displays (seconds) | 5.0 |

### Position Options

- TopLeft
- TopCenter
- TopRight
- BottomLeft
- BottomCenter
- BottomRight

### Disabling the Toast System

To disable the built-in toast system and handle notifications yourself:

1. Set `scene_path` to an empty string in Project Settings (Addons > Achievements > Toast)
2. Connect to the `AchievementUnlocked` signal on `AchievementManager` to handle notifications manually

```csharp
var achievements = GetNode<AchievementManager>("/root/Achievements");
achievements.AchievementUnlocked += OnAchievementUnlocked;

private void OnAchievementUnlocked(string id, Achievement achievement)
{
    // Your custom notification logic here
}
```

### Custom Toast Scenes

You can create a custom toast scene to match your game's visual style:

1. Create a new scene with a `Control`-based root node (e.g., `PanelContainer`)
2. Add a script with a public `Setup(Achievement achievement)` method
3. Set the scene path in Project Settings

**Example custom toast script:**

```csharp
using Godot;
using Godot.Achievements.Core;

// IMPORTANT: Add [Tool] attribute if you want to preview in the editor
[Tool]
public partial class MyCustomToast : PanelContainer
{
    [Export] private Label TitleLabel;
    [Export] private Label DescriptionLabel;
    [Export] private TextureRect IconRect;

    public void Setup(Achievement achievement)
    {
        TitleLabel.Text = achievement.DisplayName;
        DescriptionLabel.Text = achievement.Description;

        if (achievement.Icon != null)
        {
            IconRect.Texture = achievement.Icon;
            IconRect.Visible = true;
        }
        else
        {
            IconRect.Visible = false;
        }
    }
}
```

> **Note:** The `[Tool]` attribute is required if you want to use the "Visualize Unlock" button in the editor to preview your custom toast. Without it, the toast will only work at runtime.

The plugin handles all animation and lifecycle management:
- Adding toasts to the container
- Fade in/out animations
- Removing toasts after the display duration
- Multiple simultaneous toasts stack in a VBoxContainer

## Editor Features

### Achievement Editor

Access the achievement editor via the "Achievements" tab in the bottom panel. Here you can:

- Create and delete achievements
- Edit achievement properties (name, description, icon)
- Set platform-specific IDs
- Configure incremental achievements with target values

### Visualize Unlock Button

The "Visualize Unlock" button in the details panel lets you preview how the toast notification will appear without actually unlocking the achievement. This is useful for testing your custom toast scenes.

## License

MIT License
