#if TOOLS
namespace Godot.Achievements.Core.Editor;

/// <summary>
/// Editor plugin that adds the achievement editor dock to Godot
/// </summary>
[Tool]
public partial class AchievementEditorPlugin : EditorPlugin
{
    private AchievementEditorDock? _dock;

    public override void _EnterTree()
    {
        // Create and add the achievement editor dock
        _dock = new AchievementEditorDock();
        _dock.Name = "Achievements";

        // Add to the bottom panel
        AddControlToBottomPanel(_dock, "Achievements");

        GD.Print("[AchievementEditor] Plugin loaded");
    }

    public override void _ExitTree()
    {
        if (_dock != null)
        {
            RemoveControlFromBottomPanel(_dock);
            _dock.QueueFree();
            _dock = null;
        }

        GD.Print("[AchievementEditor] Plugin unloaded");
    }
}
#endif
