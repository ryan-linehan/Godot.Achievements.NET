#if TOOLS
using System;
using System.Linq;

namespace Godot.Achievements.Core.Editor;

/// <summary>
/// Editor dock for managing achievements in the Godot editor
/// </summary>
[Tool]
public partial class AchievementEditorDock : Control
{    
    [Export]
    private Button AddButton = null!;
    [Export]
    private Button RemoveButton = null!;
    [Export]
    private Button DuplicateButton = null!;

}
#endif
