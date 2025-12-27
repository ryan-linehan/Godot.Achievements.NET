#if TOOLS
namespace Godot.IAP.Core;

/// <summary>
/// Main IAP plugin that adds editor dock and registers the IAPManager autoload
/// </summary>
[Tool]
public partial class IAPPlugin : EditorPlugin
{
    /// <summary>
    /// Gets the EditorUndoRedoManager for history-aware operations in the editor
    /// </summary>
    public EditorUndoRedoManager UndoRedoManager => GetUndoRedo();

    public PackedScene IAPEditorDockScene = GD.Load<PackedScene>("res://addons/Godot.IAP.Net/Editor/IAPEditorDock.tscn");
    private const string AutoloadName = "IAP";
    private const string AutoloadPath = "res://addons/Godot.IAP.Net/Core/IAPManager.cs";

    private Editor.IAPEditorDock? _dock;

    public override void _EnterTree()
    {
        // Register all settings (ensures property info is available on every load)
        RegisterSettings();

        // Create and add the IAP editor dock
        _dock = IAPEditorDockScene.Instantiate<Editor.IAPEditorDock>();
        _dock.Name = "Products";
        _dock.SetUndoRedoManager(UndoRedoManager);
        AddControlToBottomPanel(_dock, "Products");
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
        _dock?.SaveCatalog();
    }

    public override void _EnablePlugin()
    {
        // Register all settings with defaults
        RegisterSettings();

        // Add autoload singleton (only runs once when plugin is first enabled)
        AddAutoloadSingleton(AutoloadName, AutoloadPath);
        IAPLogger.Log("Plugin enabled, autoload registered");
    }

    private void RegisterSettings()
    {
        // Catalog path (requires restart so autoload reloads from new path)
        if (!ProjectSettings.HasSetting(IAPSettings.CatalogPath))
        {
            ProjectSettings.SetSetting(IAPSettings.CatalogPath, IAPSettings.DefaultCatalogPath);
        }
        ProjectSettings.SetInitialValue(IAPSettings.CatalogPath, IAPSettings.DefaultCatalogPath);
        ProjectSettings.AddPropertyInfo(new Godot.Collections.Dictionary
        {
            { "name", IAPSettings.CatalogPath },
            { "type", (int)Variant.Type.String },
            { "hint", (int)PropertyHint.File },
            { "hint_string", "*.tres" }
        });

        // Platform: Apple enabled
        if (!ProjectSettings.HasSetting(IAPSettings.AppleEnabled))
        {
            ProjectSettings.SetSetting(IAPSettings.AppleEnabled, false);
        }
        ProjectSettings.SetInitialValue(IAPSettings.AppleEnabled, false);
        ProjectSettings.AddPropertyInfo(new Godot.Collections.Dictionary
        {
            { "name", IAPSettings.AppleEnabled },
            { "type", (int)Variant.Type.Bool }
        });

        // Platform: Google Play enabled
        if (!ProjectSettings.HasSetting(IAPSettings.GooglePlayEnabled))
        {
            ProjectSettings.SetSetting(IAPSettings.GooglePlayEnabled, false);
        }
        ProjectSettings.SetInitialValue(IAPSettings.GooglePlayEnabled, false);
        ProjectSettings.AddPropertyInfo(new Godot.Collections.Dictionary
        {
            { "name", IAPSettings.GooglePlayEnabled },
            { "type", (int)Variant.Type.Bool }
        });

        // Log level (default: Info = show all messages)
        if (!ProjectSettings.HasSetting(IAPSettings.LogLevel))
        {
            ProjectSettings.SetSetting(IAPSettings.LogLevel, (int)IAPSettings.DefaultLogLevel);
        }
        ProjectSettings.SetInitialValue(IAPSettings.LogLevel, (int)IAPSettings.DefaultLogLevel);
        ProjectSettings.AddPropertyInfo(new Godot.Collections.Dictionary
        {
            { "name", IAPSettings.LogLevel },
            { "type", (int)Variant.Type.Int },
            { "hint", (int)PropertyHint.Enum },
            { "hint_string", "Info,Warning,Error,None" }
        });

        ProjectSettings.Save();
    }

    public override void _DisablePlugin()
    {
        // Remove autoload singleton
        RemoveAutoloadSingleton(AutoloadName);
        IAPLogger.Log("Plugin disabled, autoload removed");
    }
}
#endif
