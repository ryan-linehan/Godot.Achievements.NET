#if TOOLS
using Godot;
using System;

namespace Godot.Achievements.Core.Editor;

/// <summary>
/// Details panel for editing individual achievement properties
/// </summary>
[Tool]
public partial class AchievementsEditorDetailsPanel : PanelContainer
{
    // Basic Info Controls
    [Export]
    private TextureButton AchievementIconButton = null!;
    [Export]
    private Button SelectIconButton = null!;
    [Export]
    private LineEdit NameLineEdit = null!;
    [Export]
    private LineEdit InternalIDLineEdit = null!;
    [Export]
    private TextEdit DescriptionTextBox = null!;

    // Platform Identifiers
    [Export]
    private FoldableContainer PlatformsContainer = null!;
    [Export]
    private VBoxContainer SteamVBox = null!;
    [Export]
    private LineEdit SteamIDLineEdit = null!;
    [Export]
    private VBoxContainer GooglePlayVBox = null!;
    [Export]
    private LineEdit GooglePlayIDLineEdit = null!;
    [Export]
    private VBoxContainer GameCenterVBox = null!;
    [Export]
    private LineEdit GameCenterIDLineEdit = null!;

    // Custom Properties
    [Export]
    private FoldableContainer CustomPropertiesContainer = null!;
}
#endif
