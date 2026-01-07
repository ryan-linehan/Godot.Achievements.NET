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

    public PackedScene AchievementEditorDockScene = GD.Load<PackedScene>("res://addons/Godot.Achievements.Net/Editor/AchievementEditorDock.tscn");
    private const string AutoloadName = "Achievements";
    private const string AutoloadPath = "res://addons/Godot.Achievements.Net/Core/AchievementManager.cs";
    private const string ToastAutoloadName = "AchievementToasts";
    private const string ToastAutoloadPath = "res://addons/Godot.Achievements.Net/Toast/AchievementToastContainer.tscn";

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
    
    public override void _SaveExternalData()
    {
        _dock?.SaveDatabase();
    }

    public override void _EnablePlugin()
    {
        // Register all settings with defaults
        RegisterSettings();

        // Add autoload singletons (only runs once when plugin is first enabled)
        AddAutoloadSingleton(AutoloadName, AutoloadPath);
        AddAutoloadSingleton(ToastAutoloadName, ToastAutoloadPath);
        AchievementLogger.Log("Plugin enabled, autoloads registered");
    }

    private void RegisterSettings()
    {
        // Database path (requires restart so autoload reloads from new path)
        if (!ProjectSettings.HasSetting(AchievementSettings.DatabasePath))
        {
            ProjectSettings.SetSetting(AchievementSettings.DatabasePath, AchievementSettings.DefaultDatabasePath);
        }
        ProjectSettings.SetInitialValue(AchievementSettings.DatabasePath, AchievementSettings.DefaultDatabasePath);
        ProjectSettings.AddPropertyInfo(new Godot.Collections.Dictionary
        {
            { "name", AchievementSettings.DatabasePath },
            { "type", (int)Variant.Type.String },
            { "hint", (int)PropertyHint.File },
            { "hint_string", "*.tres" }
        });

        // Platform: Steam enabled
        if (!ProjectSettings.HasSetting(AchievementSettings.SteamEnabled))
        {
            ProjectSettings.SetSetting(AchievementSettings.SteamEnabled, false);
        }
        ProjectSettings.SetInitialValue(AchievementSettings.SteamEnabled, false);
        ProjectSettings.AddPropertyInfo(new Godot.Collections.Dictionary
        {
            { "name", AchievementSettings.SteamEnabled },
            { "type", (int)Variant.Type.Bool }
        });

        // Platform: Game Center enabled
        if (!ProjectSettings.HasSetting(AchievementSettings.GameCenterEnabled))
        {
            ProjectSettings.SetSetting(AchievementSettings.GameCenterEnabled, false);
        }
        ProjectSettings.SetInitialValue(AchievementSettings.GameCenterEnabled, false);
        ProjectSettings.AddPropertyInfo(new Godot.Collections.Dictionary
        {
            { "name", AchievementSettings.GameCenterEnabled },
            { "type", (int)Variant.Type.Bool }
        });

        // Platform: Google Play enabled
        if (!ProjectSettings.HasSetting(AchievementSettings.GooglePlayEnabled))
        {
            ProjectSettings.SetSetting(AchievementSettings.GooglePlayEnabled, false);
        }
        ProjectSettings.SetInitialValue(AchievementSettings.GooglePlayEnabled, false);
        ProjectSettings.AddPropertyInfo(new Godot.Collections.Dictionary
        {
            { "name", AchievementSettings.GooglePlayEnabled },
            { "type", (int)Variant.Type.Bool }
        });

        // Toast scene path (default: built-in, empty = disabled)
        if (!ProjectSettings.HasSetting(AchievementSettings.ToastScenePath) ||
            ProjectSettings.GetSetting(AchievementSettings.ToastScenePath).VariantType == Variant.Type.Nil)
        {
            ProjectSettings.SetSetting(AchievementSettings.ToastScenePath, AchievementSettings.DefaultToastScenePath);
        }
        ProjectSettings.SetInitialValue(AchievementSettings.ToastScenePath, AchievementSettings.DefaultToastScenePath);
        ProjectSettings.AddPropertyInfo(new Godot.Collections.Dictionary
        {
            { "name", AchievementSettings.ToastScenePath },
            { "type", (int)Variant.Type.String },
            { "hint", (int)PropertyHint.File },
            { "hint_string", "*.tscn" }
        });

        // Toast position (default: TopRight = 2)
        if (!ProjectSettings.HasSetting(AchievementSettings.ToastPosition) ||
            ProjectSettings.GetSetting(AchievementSettings.ToastPosition).VariantType == Variant.Type.Nil)
        {
            ProjectSettings.SetSetting(AchievementSettings.ToastPosition, (int)Core.ToastPosition.TopRight);
        }
        ProjectSettings.SetInitialValue(AchievementSettings.ToastPosition, (int)Core.ToastPosition.TopRight);
        ProjectSettings.AddPropertyInfo(new Godot.Collections.Dictionary
        {
            { "name", AchievementSettings.ToastPosition },
            { "type", (int)Variant.Type.Int },
            { "hint", (int)PropertyHint.Enum },
            { "hint_string", "TopLeft,TopCenter,TopRight,BottomLeft,BottomCenter,BottomRight" }
        });

        // Toast display duration (default: 5.0 seconds)
        if (!ProjectSettings.HasSetting(AchievementSettings.ToastDisplayDuration) ||
            ProjectSettings.GetSetting(AchievementSettings.ToastDisplayDuration).VariantType == Variant.Type.Nil)
        {
            ProjectSettings.SetSetting(AchievementSettings.ToastDisplayDuration, AchievementSettings.DefaultToastDisplayDuration);
        }
        ProjectSettings.SetInitialValue(AchievementSettings.ToastDisplayDuration, AchievementSettings.DefaultToastDisplayDuration);
        ProjectSettings.AddPropertyInfo(new Godot.Collections.Dictionary
        {
            { "name", AchievementSettings.ToastDisplayDuration },
            { "type", (int)Variant.Type.Float },
            { "hint", (int)PropertyHint.Range },
            { "hint_string", "0.5,30.0,0.5" }
        });

        // Unlock sound (default: empty = no sound)
        if (!ProjectSettings.HasSetting(AchievementSettings.ToastUnlockSound))
        {
            ProjectSettings.SetSetting(AchievementSettings.ToastUnlockSound, "");
        }
        ProjectSettings.SetInitialValue(AchievementSettings.ToastUnlockSound, "");
        ProjectSettings.AddPropertyInfo(new Godot.Collections.Dictionary
        {
            { "name", AchievementSettings.ToastUnlockSound },
            { "type", (int)Variant.Type.String },
            { "hint", (int)PropertyHint.File },
            { "hint_string", "*.wav,*.ogg,*.mp3" }
        });

        // Log level (default: Info = show all messages)
        if (!ProjectSettings.HasSetting(AchievementSettings.LogLevel))
        {
            ProjectSettings.SetSetting(AchievementSettings.LogLevel, (int)AchievementSettings.DefaultLogLevel);
        }
        ProjectSettings.SetInitialValue(AchievementSettings.LogLevel, (int)AchievementSettings.DefaultLogLevel);
        ProjectSettings.AddPropertyInfo(new Godot.Collections.Dictionary
        {
            { "name", AchievementSettings.LogLevel },
            { "type", (int)Variant.Type.Int },
            { "hint", (int)PropertyHint.Enum },
            { "hint_string", "Info,Warning,Error,None" }
        });

        // Code Generation: Auto-generate constants on save
        if (!ProjectSettings.HasSetting(AchievementSettings.ConstantsAutoGenerate))
        {
            ProjectSettings.SetSetting(AchievementSettings.ConstantsAutoGenerate, false);
        }
        ProjectSettings.SetInitialValue(AchievementSettings.ConstantsAutoGenerate, false);
        ProjectSettings.AddPropertyInfo(new Godot.Collections.Dictionary
        {
            { "name", AchievementSettings.ConstantsAutoGenerate },
            { "type", (int)Variant.Type.Bool }
        });

        // Code Generation: Output path for constants file
        if (!ProjectSettings.HasSetting(AchievementSettings.ConstantsOutputPath))
        {
            ProjectSettings.SetSetting(AchievementSettings.ConstantsOutputPath, AchievementSettings.DefaultConstantsOutputPath);
        }
        ProjectSettings.SetInitialValue(AchievementSettings.ConstantsOutputPath, AchievementSettings.DefaultConstantsOutputPath);
        ProjectSettings.AddPropertyInfo(new Godot.Collections.Dictionary
        {
            { "name", AchievementSettings.ConstantsOutputPath },
            { "type", (int)Variant.Type.String },
            { "hint", (int)PropertyHint.SaveFile },
            { "hint_string", "*.cs" }
        });

        // Code Generation: Class name for generated constants
        if (!ProjectSettings.HasSetting(AchievementSettings.ConstantsClassName))
        {
            ProjectSettings.SetSetting(AchievementSettings.ConstantsClassName, AchievementSettings.DefaultConstantsClassName);
        }
        ProjectSettings.SetInitialValue(AchievementSettings.ConstantsClassName, AchievementSettings.DefaultConstantsClassName);
        ProjectSettings.AddPropertyInfo(new Godot.Collections.Dictionary
        {
            { "name", AchievementSettings.ConstantsClassName },
            { "type", (int)Variant.Type.String }
        });

        // Code Generation: Namespace for generated constants (optional)
        if (!ProjectSettings.HasSetting(AchievementSettings.ConstantsNamespace))
        {
            ProjectSettings.SetSetting(AchievementSettings.ConstantsNamespace, "");
        }
        ProjectSettings.SetInitialValue(AchievementSettings.ConstantsNamespace, "");
        ProjectSettings.AddPropertyInfo(new Godot.Collections.Dictionary
        {
            { "name", AchievementSettings.ConstantsNamespace },
            { "type", (int)Variant.Type.String }
        });

        ProjectSettings.Save();
    }

    public override void _DisablePlugin()
    {
        // Remove core autoload singletons
        RemoveAutoloadSingleton(AutoloadName);
        RemoveAutoloadSingleton(ToastAutoloadName);

        AchievementLogger.Log("Plugin disabled, autoloads removed");
    }
}
#endif
