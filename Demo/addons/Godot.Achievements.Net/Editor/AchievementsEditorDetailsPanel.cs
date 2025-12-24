#if TOOLS
using Godot;
using Godot.Achievements.Core;
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
    private Control IconPickerContainer = null!;
    [Export]
    private LineEdit NameLineEdit = null!;
    [Export]
    private LineEdit InternalIDLineEdit = null!;
    [Export]
    private TextEdit DescriptionTextBox = null!;

    // Progress Tracking
    [Export]
    private CheckBox TrackProgressCheckBox = null!;
    [Export]
    private SpinBox TargetValueSpinBox = null!;

    // Platform Identifiers
    [Export]
    private FoldableContainer PlatformsContainer = null!;
    [Export]
    public VBoxContainer SteamVBox = null!;
    [Export]
    private LineEdit SteamIDLineEdit = null!;
    [Export]
    public VBoxContainer GooglePlayVBox = null!;
    [Export]
    private LineEdit GooglePlayIDLineEdit = null!;
    [Export]
    public VBoxContainer GameCenterVBox = null!;
    [Export]
    private LineEdit GameCenterIDLineEdit = null!;

    // Custom Properties
    [Export]
    private FoldableContainer CustomPropertiesContainer = null!;

    // Visualize Unlock
    [Export]
    private Button VisualizeUnlockButton = null!;

    // Private Fields
    private Achievement? _currentAchievement;
    private bool _isUpdating = false;
    private EditorResourcePicker? _iconPicker;
    private string _previousId = string.Empty;
    private string _idBeforeEdit = string.Empty;
    private EditorToastPreview? _editorToastPreview;

    // Signals
    [Signal]
    public delegate void AchievementIdChangedEventHandler(Achievement achievement, string oldId, string newId);

    [Signal]
    public delegate void AchievementDisplayNameChangedEventHandler(Achievement achievement);

    [Signal]
    public delegate void AchievementChangedEventHandler();

    public Achievement? CurrentAchievement
    {
        get => _currentAchievement;
        set
        {
            // Skip reload if setting the same achievement (prevents cursor jump while typing)
            if (_currentAchievement == value)
                return;

            _currentAchievement = value;
            _previousId = value?.Id ?? string.Empty;
            LoadAchievementData();
        }
    }

    public override void _Ready()
    {
        // Create and setup icon picker
        _iconPicker = new EditorResourcePicker();
        _iconPicker.BaseType = "Texture2D";
        _iconPicker.ResourceChanged += OnIconResourceChanged;

        // Add icon picker to the container
        if (IconPickerContainer != null)
        {
            IconPickerContainer.AddChild(_iconPicker);
        }

        // Connect field signals
        if (NameLineEdit != null)
            NameLineEdit.TextChanged += OnNameChanged;
        if (InternalIDLineEdit != null)
        {
            InternalIDLineEdit.TextChanged += OnIdTextChanged;
            InternalIDLineEdit.FocusEntered += OnIdFocusEntered;
            InternalIDLineEdit.FocusExited += OnIdFocusExited;
        }
        if (DescriptionTextBox != null)
            DescriptionTextBox.TextChanged += OnDescriptionChanged;
        if (TrackProgressCheckBox != null)
            TrackProgressCheckBox.Toggled += OnTrackProgressToggled;
        if (TargetValueSpinBox != null)
            TargetValueSpinBox.ValueChanged += OnTargetValueChanged;
        if (SteamIDLineEdit != null)
            SteamIDLineEdit.TextChanged += OnSteamIdChanged;
        if (GooglePlayIDLineEdit != null)
            GooglePlayIDLineEdit.TextChanged += OnGooglePlayIdChanged;
        if (GameCenterIDLineEdit != null)
            GameCenterIDLineEdit.TextChanged += OnGameCenterIdChanged;
        if (VisualizeUnlockButton != null)
            VisualizeUnlockButton.Pressed += OnVisualizeUnlockPressed;
    }

    private void OnNameChanged(string newName)
    {
        if (_isUpdating || _currentAchievement == null) return;

        _currentAchievement.DisplayName = newName;
        SaveCurrentAchievement();
        EmitSignal(SignalName.AchievementDisplayNameChanged, _currentAchievement);
        EmitSignal(SignalName.AchievementChanged);
    }

    private void OnIdFocusEntered()
    {
        if (_currentAchievement == null) return;
        _idBeforeEdit = _currentAchievement.Id;
    }

    private void OnIdTextChanged(string newId)
    {
        if (_isUpdating || _currentAchievement == null) return;

        _currentAchievement.Id = newId;
        SaveCurrentAchievement();
        _previousId = newId;
        EmitSignal(SignalName.AchievementChanged);
    }

    private void OnIdFocusExited()
    {
        if (_currentAchievement == null) return;

        // Only trigger rename if the ID actually changed
        if (_idBeforeEdit != _currentAchievement.Id)
        {
            EmitSignal(SignalName.AchievementIdChanged, _currentAchievement, _idBeforeEdit, _currentAchievement.Id);
        }
    }

    private void OnDescriptionChanged()
    {
        if (_isUpdating || _currentAchievement == null || DescriptionTextBox == null) return;

        _currentAchievement.Description = DescriptionTextBox.Text;
        SaveCurrentAchievement();
        EmitSignal(SignalName.AchievementChanged);
    }

    private void OnSteamIdChanged(string text)
    {
        if (_isUpdating || _currentAchievement == null) return;

        _currentAchievement.SteamId = text;
        SaveCurrentAchievement();
        EmitSignal(SignalName.AchievementChanged);
    }

    private void OnGooglePlayIdChanged(string text)
    {
        if (_isUpdating || _currentAchievement == null) return;

        _currentAchievement.GooglePlayId = text;
        SaveCurrentAchievement();
        EmitSignal(SignalName.AchievementChanged);
    }

    private void OnGameCenterIdChanged(string text)
    {
        if (_isUpdating || _currentAchievement == null) return;

        _currentAchievement.GameCenterId = text;
        SaveCurrentAchievement();
        EmitSignal(SignalName.AchievementChanged);
    }

    private void OnTrackProgressToggled(bool enabled)
    {
        if (_isUpdating || _currentAchievement == null) return;

        _currentAchievement.IsIncremental = enabled;
        if (TargetValueSpinBox != null)
            TargetValueSpinBox.Editable = enabled;
        SaveCurrentAchievement();
        EmitSignal(SignalName.AchievementChanged);
    }

    private void OnTargetValueChanged(double value)
    {
        if (_isUpdating || _currentAchievement == null) return;

        _currentAchievement.MaxProgress = (int)value;
        SaveCurrentAchievement();
        EmitSignal(SignalName.AchievementChanged);
    }

    private void OnVisualizeUnlockPressed()
    {
        if (_currentAchievement == null) return;

        // Create editor toast preview if it doesn't exist
        if (_editorToastPreview == null || !IsInstanceValid(_editorToastPreview))
        {
            _editorToastPreview = new EditorToastPreview();

            // Add to editor's base control
            var baseControl = EditorInterface.Singleton.GetBaseControl();
            baseControl.AddChild(_editorToastPreview);
        }

        // Show the toast
        _editorToastPreview.ShowToast(_currentAchievement);
    }

    public override void _ExitTree()
    {
        // Clean up editor toast preview
        if (_editorToastPreview != null && IsInstanceValid(_editorToastPreview))
        {
            _editorToastPreview.QueueFree();
            _editorToastPreview = null;
        }

        if (_iconPicker != null)
        {
            _iconPicker.ResourceChanged -= OnIconResourceChanged;
            _iconPicker.QueueFree();
        }

        // Disconnect signals
        if (NameLineEdit != null)
            NameLineEdit.TextChanged -= OnNameChanged;
        if (InternalIDLineEdit != null)
        {
            InternalIDLineEdit.TextChanged -= OnIdTextChanged;
            InternalIDLineEdit.FocusEntered -= OnIdFocusEntered;
            InternalIDLineEdit.FocusExited -= OnIdFocusExited;
        }
        if (DescriptionTextBox != null)
            DescriptionTextBox.TextChanged -= OnDescriptionChanged;
        if (TrackProgressCheckBox != null)
            TrackProgressCheckBox.Toggled -= OnTrackProgressToggled;
        if (TargetValueSpinBox != null)
            TargetValueSpinBox.ValueChanged -= OnTargetValueChanged;
        if (SteamIDLineEdit != null)
            SteamIDLineEdit.TextChanged -= OnSteamIdChanged;
        if (GooglePlayIDLineEdit != null)
            GooglePlayIDLineEdit.TextChanged -= OnGooglePlayIdChanged;
        if (GameCenterIDLineEdit != null)
            GameCenterIDLineEdit.TextChanged -= OnGameCenterIdChanged;
        if (VisualizeUnlockButton != null)
            VisualizeUnlockButton.Pressed -= OnVisualizeUnlockPressed;
    }

    private void LoadAchievementData()
    {
        if (_currentAchievement == null)
        {
            ClearFields();
            return;
        }

        _isUpdating = true;

        if (NameLineEdit != null)
            NameLineEdit.Text = _currentAchievement.DisplayName ?? string.Empty;
        if (InternalIDLineEdit != null)
        {
            InternalIDLineEdit.Text = _currentAchievement.Id ?? string.Empty;
            _idBeforeEdit = _currentAchievement.Id ?? string.Empty;
        }
        if (DescriptionTextBox != null)
            DescriptionTextBox.Text = _currentAchievement.Description ?? string.Empty;

        if (TrackProgressCheckBox != null)
        {
            TrackProgressCheckBox.ButtonPressed = _currentAchievement.IsIncremental;
        }
        if (TargetValueSpinBox != null)
        {
            TargetValueSpinBox.Value = _currentAchievement.MaxProgress;
            TargetValueSpinBox.Editable = _currentAchievement.IsIncremental;
        }

        if (_currentAchievement.Icon != null)
        {
            if (AchievementIconButton != null)
                AchievementIconButton.TextureNormal = _currentAchievement.Icon;
            if (_iconPicker != null)
                _iconPicker.EditedResource = _currentAchievement.Icon;
        }
        else
        {
            if (AchievementIconButton != null)
                AchievementIconButton.TextureNormal = null;
            if (_iconPicker != null)
                _iconPicker.EditedResource = null;
        }

        if (SteamIDLineEdit != null)
            SteamIDLineEdit.Text = _currentAchievement.SteamId ?? string.Empty;
        if (GooglePlayIDLineEdit != null)
            GooglePlayIDLineEdit.Text = _currentAchievement.GooglePlayId ?? string.Empty;
        if (GameCenterIDLineEdit != null)
            GameCenterIDLineEdit.Text = _currentAchievement.GameCenterId ?? string.Empty;

        _isUpdating = false;
    }

    private void ClearFields()
    {
        _isUpdating = true;

        if (NameLineEdit != null)
            NameLineEdit.Text = string.Empty;
        if (InternalIDLineEdit != null)
            InternalIDLineEdit.Text = string.Empty;
        if (DescriptionTextBox != null)
            DescriptionTextBox.Text = string.Empty;
        if (TrackProgressCheckBox != null)
            TrackProgressCheckBox.ButtonPressed = false;
        if (TargetValueSpinBox != null)
        {
            TargetValueSpinBox.Value = 1;
            TargetValueSpinBox.Editable = false;
        }
        if (AchievementIconButton != null)
            AchievementIconButton.TextureNormal = null;
        if (_iconPicker != null)
            _iconPicker.EditedResource = null;
        if (SteamIDLineEdit != null)
            SteamIDLineEdit.Text = string.Empty;
        if (GooglePlayIDLineEdit != null)
            GooglePlayIDLineEdit.Text = string.Empty;
        if (GameCenterIDLineEdit != null)
            GameCenterIDLineEdit.Text = string.Empty;

        _isUpdating = false;
    }

    private void SaveCurrentAchievement()
    {
        // Achievements are stored inline in the database, no individual file saving needed
        // The achievement object is already being modified in the database's Achievements array
        // Database will be saved when user explicitly saves or through auto-save
    }

    private void OnIconResourceChanged(Resource resource)
    {
        if (_isUpdating || _currentAchievement == null)
            return;

        var texture = resource as Texture2D;
        _currentAchievement.Icon = texture;
        AchievementIconButton.TextureNormal = texture;
        SaveCurrentAchievement();
        EmitSignal(SignalName.AchievementDisplayNameChanged, _currentAchievement);
        EmitSignal(SignalName.AchievementChanged);
    }
}
#endif
