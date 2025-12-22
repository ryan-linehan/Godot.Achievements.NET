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
    // Top Bar Controls
    [Export]
    private Button ChangeDatabaseButton = null!;
    [Export]
    private CheckBox SteamCheckbox = null!;
    [Export]
    private CheckBox GameCenterCheckbox = null!;
    [Export]
    private CheckBox GooglePlayCheckbox = null!;

    // List Panel Controls
    [Export]
    private LineEdit SearchLineEdit = null!;
    [Export]
    private Button AddAchievementButton = null!;
    [Export]
    private Button RemoveButton = null!;
    [Export]
    private Button DuplicateButton = null!;
    [Export]
    private Control NoItemsControl = null!;
    [Export]
    private ItemList ItemList = null!;

    // Details Panel Component
    [Export]
    private AchievementsEditorDetailsPanel DetailsPanel = null!;
    [Export]
    private ScrollContainer NoItemSelectedScroll = null!;

    // Bottom Bar Controls
    [Export]
    private Label DatabasePathLabel = null!;
    [Export]
    private Button ImportCSVButton = null!;
    [Export]
    private Button ExportCSVButton = null!;

}
#endif
