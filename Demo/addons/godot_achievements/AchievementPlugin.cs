#if TOOLS
namespace Godot.Achievements.Core;

/// <summary>
/// Main achievement plugin that adds editor dock and registers the AchievementManager autoload
/// </summary>
[Tool]
public partial class AchievementPlugin : EditorPlugin
{
    [Export]
    public PackedScene AchievementEditorDockScene = GD.Load<PackedScene>("res://addons/godot_achievements/Editor/AchievementEditorDock.tscn");
    private const string AutoloadName = "Achievements";
    private const string AutoloadPath = "res://addons/godot_achievements/AchievementManager.cs";

    private Editor.AchievementEditorDock? _dock;

    public override void _EnterTree()
    {
        // Create and add the achievement editor dock
        // This runs on every project load to ensure the dock is always available
        _dock = new Editor.AchievementEditorDock();
        _dock.Name = "Achievements";
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
        // Add autoload singleton (only runs once when plugin is first enabled)
        AddAutoloadSingleton(AutoloadName, AutoloadPath);
        GD.Print("[Achievements] Plugin enabled, autoload registered");
    }

    public override void _DisablePlugin()
    {
        // Remove autoload singleton (only runs when plugin is disabled)
        RemoveAutoloadSingleton(AutoloadName);
        GD.Print("[Achievements] Plugin disabled, autoload removed");
    }
}
#endif
