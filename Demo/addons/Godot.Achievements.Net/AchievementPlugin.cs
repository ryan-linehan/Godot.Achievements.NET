#if TOOLS
namespace Godot.Achievements.Core;

/// <summary>
/// Main achievement plugin that adds editor dock and registers the AchievementManager autoload
/// </summary>
[Tool]
public partial class AchievementPlugin : EditorPlugin
{
    /// <summary>
    /// Gets the EditorUndoRedoManager for history-aware operations in the editor
    /// </summary>
    public EditorUndoRedoManager UndoRedoManager => GetUndoRedo();

    public PackedScene AchievementEditorDockScene = GD.Load<PackedScene>("res://addons/Godot.Achievements.Net/Editor/AchievementsEditorDock.tscn");
    private const string AutoloadName = "Achievements";
    private const string AutoloadPath = "res://addons/Godot.Achievements.Net/AchievementManager.cs";
    private const string ToastAutoloadName = "AchievementToasts";
    private const string ToastAutoloadPath = "res://addons/Godot.Achievements.Net/AchievementToastContainer.tscn";

    // Database settings
    private const string SettingDatabasePath = "addons/achievements/database_path";
    private const string DefaultDatabasePath = "res://addons/Godot.Achievements.Net/_achievements/_achievements.tres";

    // Platform settings (grouped under "Platforms" header)
    private const string SettingSteamEnabled = "addons/achievements/platforms/steam_enabled";
    private const string SettingGameCenterEnabled = "addons/achievements/platforms/gamecenter_enabled";
    private const string SettingGooglePlayEnabled = "addons/achievements/platforms/googleplay_enabled";

    // Toast settings (grouped under "Toast" header)
    private const string SettingToastScenePath = "addons/achievements/toast/scene_path";
    private const string SettingToastPosition = "addons/achievements/toast/position";
    private const string SettingToastDisplayDuration = "addons/achievements/toast/display_duration";
    private const string SettingUnlockSound = "addons/achievements/toast/unlock_sound";
    private const string DefaultToastScenePath = "res://addons/Godot.Achievements.Net/AchievementToastItem.tscn";

    private Editor.AchievementEditorDock? _dock;

    public override void _EnterTree()
    {
        // Register all settings (ensures property info is available on every load)
        RegisterSettings();

        // Create and add the achievement editor dock
        _dock = AchievementEditorDockScene.Instantiate<Editor.AchievementEditorDock>();
        _dock.Name = "Achievements";
        _dock.SetUndoRedoManager(UndoRedoManager);
        AddControlToBottomPanel(_dock, "Achievements");
    }

    public override void _ExitTree()
    {
        // Remove and cleanup dock when editor closes
        if (_dock != null)
        {
            RemoveControlFromBottomPanel(_dock);
            _dock.QueueFree();
            _dock = null;
        }
    }

    public override void _EnablePlugin()
    {
        // Register all settings with defaults
        RegisterSettings();

        // Add autoload singletons (only runs once when plugin is first enabled)
        AddAutoloadSingleton(AutoloadName, AutoloadPath);
        AddAutoloadSingleton(ToastAutoloadName, ToastAutoloadPath);
        GD.Print("[Achievements] Plugin enabled, autoloads registered");
    }

    private void RegisterSettings()
    {
        // Database path (requires restart so autoload reloads from new path)
        if (!ProjectSettings.HasSetting(SettingDatabasePath))
        {
            ProjectSettings.SetSetting(SettingDatabasePath, DefaultDatabasePath);
        }
        ProjectSettings.SetInitialValue(SettingDatabasePath, DefaultDatabasePath);
        ProjectSettings.AddPropertyInfo(new Godot.Collections.Dictionary
        {
            { "name", SettingDatabasePath },
            { "type", (int)Variant.Type.String },
            { "hint", (int)PropertyHint.File },
            { "hint_string", "*.tres" }
        });

        // Platform: Steam enabled
        if (!ProjectSettings.HasSetting(SettingSteamEnabled))
        {
            ProjectSettings.SetSetting(SettingSteamEnabled, false);
        }
        ProjectSettings.SetInitialValue(SettingSteamEnabled, false);
        ProjectSettings.AddPropertyInfo(new Godot.Collections.Dictionary
        {
            { "name", SettingSteamEnabled },
            { "type", (int)Variant.Type.Bool }
        });

        // Platform: Game Center enabled
        if (!ProjectSettings.HasSetting(SettingGameCenterEnabled))
        {
            ProjectSettings.SetSetting(SettingGameCenterEnabled, false);
        }
        ProjectSettings.SetInitialValue(SettingGameCenterEnabled, false);
        ProjectSettings.AddPropertyInfo(new Godot.Collections.Dictionary
        {
            { "name", SettingGameCenterEnabled },
            { "type", (int)Variant.Type.Bool }
        });

        // Platform: Google Play enabled
        if (!ProjectSettings.HasSetting(SettingGooglePlayEnabled))
        {
            ProjectSettings.SetSetting(SettingGooglePlayEnabled, false);
        }
        ProjectSettings.SetInitialValue(SettingGooglePlayEnabled, false);
        ProjectSettings.AddPropertyInfo(new Godot.Collections.Dictionary
        {
            { "name", SettingGooglePlayEnabled },
            { "type", (int)Variant.Type.Bool }
        });

        // Toast scene path (default: built-in, empty = disabled)
        if (!ProjectSettings.HasSetting(SettingToastScenePath) ||
            ProjectSettings.GetSetting(SettingToastScenePath).VariantType == Variant.Type.Nil)
        {
            ProjectSettings.SetSetting(SettingToastScenePath, DefaultToastScenePath);
        }
        ProjectSettings.SetInitialValue(SettingToastScenePath, DefaultToastScenePath);
        ProjectSettings.AddPropertyInfo(new Godot.Collections.Dictionary
        {
            { "name", SettingToastScenePath },
            { "type", (int)Variant.Type.String },
            { "hint", (int)PropertyHint.File },
            { "hint_string", "*.tscn" }
        });

        // Toast position (default: TopRight = 2)
        if (!ProjectSettings.HasSetting(SettingToastPosition) ||
            ProjectSettings.GetSetting(SettingToastPosition).VariantType == Variant.Type.Nil)
        {
            ProjectSettings.SetSetting(SettingToastPosition, (int)ToastPosition.TopRight);
        }
        ProjectSettings.SetInitialValue(SettingToastPosition, (int)ToastPosition.TopRight);
        ProjectSettings.AddPropertyInfo(new Godot.Collections.Dictionary
        {
            { "name", SettingToastPosition },
            { "type", (int)Variant.Type.Int },
            { "hint", (int)PropertyHint.Enum },
            { "hint_string", "TopLeft,TopCenter,TopRight,BottomLeft,BottomCenter,BottomRight" }
        });

        // Toast display duration (default: 5.0 seconds)
        if (!ProjectSettings.HasSetting(SettingToastDisplayDuration) ||
            ProjectSettings.GetSetting(SettingToastDisplayDuration).VariantType == Variant.Type.Nil)
        {
            ProjectSettings.SetSetting(SettingToastDisplayDuration, 5.0f);
        }
        ProjectSettings.SetInitialValue(SettingToastDisplayDuration, 5.0f);
        ProjectSettings.AddPropertyInfo(new Godot.Collections.Dictionary
        {
            { "name", SettingToastDisplayDuration },
            { "type", (int)Variant.Type.Float },
            { "hint", (int)PropertyHint.Range },
            { "hint_string", "0.5,30.0,0.5" }
        });

        // Unlock sound (default: empty = no sound)
        if (!ProjectSettings.HasSetting(SettingUnlockSound))
        {
            ProjectSettings.SetSetting(SettingUnlockSound, "");
        }
        ProjectSettings.SetInitialValue(SettingUnlockSound, "");
        ProjectSettings.AddPropertyInfo(new Godot.Collections.Dictionary
        {
            { "name", SettingUnlockSound },
            { "type", (int)Variant.Type.String },
            { "hint", (int)PropertyHint.File },
            { "hint_string", "*.wav,*.ogg,*.mp3" }
        });

        ProjectSettings.Save();
    }

    public override void _DisablePlugin()
    {
        // Remove core autoload singletons
        RemoveAutoloadSingleton(AutoloadName);
        RemoveAutoloadSingleton(ToastAutoloadName);

        GD.Print("[Achievements] Plugin disabled, autoloads removed");
    }
}
#endif
