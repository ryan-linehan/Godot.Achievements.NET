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

    public override void _EnablePlugin()
    {
        // Add autoload singleton
        AddAutoloadSingleton(AutoloadName, AutoloadPath);

        // Create and add the achievement editor dock
        _dock = new Editor.AchievementEditorDock();
        _dock.Name = "Achievements";
        AddControlToBottomPanel(_dock, "Achievements");

        GD.Print("[Achievements] Plugin enabled, autoload registered");
    }

    public override void _DisablePlugin()
    {
        // Remove autoload singleton
        RemoveAutoloadSingleton(AutoloadName);

        // Remove and cleanup dock
        if (_dock != null)
        {
            RemoveControlFromBottomPanel(_dock);
            _dock.QueueFree();
            _dock = null;
        }

        GD.Print("[Achievements] Plugin disabled, autoload removed");
    }
}
#endif
