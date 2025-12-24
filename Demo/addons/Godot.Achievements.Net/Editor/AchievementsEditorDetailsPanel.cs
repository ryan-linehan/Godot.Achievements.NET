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
    private EditorUndoRedoManager? _undoRedoManager;

    // Track old values for undo/redo
    private string _nameBeforeEdit = string.Empty;
    private string _descriptionBeforeEdit = string.Empty;
    private string _steamIdBeforeEdit = string.Empty;
    private string _googlePlayIdBeforeEdit = string.Empty;
    private string _gameCenterIdBeforeEdit = string.Empty;

    // Validation labels
    private Label? _internalIdErrorLabel;
    private Label? _steamWarningLabel;
    private Label? _googlePlayWarningLabel;
    private Label? _gameCenterWarningLabel;

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

    /// <summary>
    /// Sets the undo/redo manager for editor history support
    /// </summary>
    public void SetUndoRedoManager(EditorUndoRedoManager undoRedoManager)
    {
        _undoRedoManager = undoRedoManager;
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
        {
            NameLineEdit.TextChanged += OnNameChanged;
            NameLineEdit.FocusEntered += OnNameFocusEntered;
            NameLineEdit.FocusExited += OnNameFocusExited;
        }
        if (InternalIDLineEdit != null)
        {
            InternalIDLineEdit.TextChanged += OnIdTextChanged;
            InternalIDLineEdit.FocusEntered += OnIdFocusEntered;
            InternalIDLineEdit.FocusExited += OnIdFocusExited;
        }
        if (DescriptionTextBox != null)
        {
            DescriptionTextBox.TextChanged += OnDescriptionChanged;
            DescriptionTextBox.FocusEntered += OnDescriptionFocusEntered;
            DescriptionTextBox.FocusExited += OnDescriptionFocusExited;
        }
        if (TrackProgressCheckBox != null)
            TrackProgressCheckBox.Toggled += OnTrackProgressToggled;
        if (TargetValueSpinBox != null)
            TargetValueSpinBox.ValueChanged += OnTargetValueChanged;
        if (SteamIDLineEdit != null)
        {
            SteamIDLineEdit.TextChanged += OnSteamIdChanged;
            SteamIDLineEdit.FocusEntered += OnSteamIdFocusEntered;
            SteamIDLineEdit.FocusExited += OnSteamIdFocusExited;
        }
        if (GooglePlayIDLineEdit != null)
        {
            GooglePlayIDLineEdit.TextChanged += OnGooglePlayIdChanged;
            GooglePlayIDLineEdit.FocusEntered += OnGooglePlayIdFocusEntered;
            GooglePlayIDLineEdit.FocusExited += OnGooglePlayIdFocusExited;
        }
        if (GameCenterIDLineEdit != null)
        {
            GameCenterIDLineEdit.TextChanged += OnGameCenterIdChanged;
            GameCenterIDLineEdit.FocusEntered += OnGameCenterIdFocusEntered;
            GameCenterIDLineEdit.FocusExited += OnGameCenterIdFocusExited;
        }
        if (VisualizeUnlockButton != null)
            VisualizeUnlockButton.Pressed += OnVisualizeUnlockPressed;

        // Create validation labels
        _internalIdErrorLabel = CreateValidationLabel(InternalIDLineEdit, isError: true);
        _steamWarningLabel = CreateValidationLabel(SteamIDLineEdit, isError: false);
        _googlePlayWarningLabel = CreateValidationLabel(GooglePlayIDLineEdit, isError: false);
        _gameCenterWarningLabel = CreateValidationLabel(GameCenterIDLineEdit, isError: false);
    }

    private Label? CreateValidationLabel(Control? siblingControl, bool isError)
    {
        if (siblingControl == null) return null;

        var label = new Label();
        label.AddThemeColorOverride("font_color", isError ? new Color(1, 0.4f, 0.4f) : new Color(1, 0.75f, 0.3f));
        label.Visible = false;

        // Add after the sibling control
        var parent = siblingControl.GetParent();
        if (parent != null)
        {
            var siblingIndex = siblingControl.GetIndex();
            parent.AddChild(label);
            parent.MoveChild(label, siblingIndex + 1);
        }

        return label;
    }

    /// <summary>
    /// Updates validation display for the current achievement
    /// </summary>
    public void UpdateValidation(AchievementValidationResult? validationResult, System.Collections.Generic.List<string>? duplicateInternalIds)
    {
        // Update internal ID error label
        if (_internalIdErrorLabel != null)
        {
            var isMissing = _currentAchievement != null
                && string.IsNullOrWhiteSpace(_currentAchievement.Id);

            var hasDuplicateId = _currentAchievement != null
                && duplicateInternalIds != null
                && !string.IsNullOrWhiteSpace(_currentAchievement.Id)
                && duplicateInternalIds.Contains(_currentAchievement.Id);

            _internalIdErrorLabel.Visible = isMissing || hasDuplicateId;
            _internalIdErrorLabel.Text = isMissing ? "\u274c Required" : (hasDuplicateId ? "\u274c Duplicate" : string.Empty);
        }

        // Update platform warning labels based on validation result
        UpdatePlatformWarningLabel(_steamWarningLabel, validationResult, "Steam");
        UpdatePlatformWarningLabel(_googlePlayWarningLabel, validationResult, "Google Play");
        UpdatePlatformWarningLabel(_gameCenterWarningLabel, validationResult, "Game Center");
    }

    private void UpdatePlatformWarningLabel(Label? label, AchievementValidationResult? validationResult, string platformName)
    {
        if (label == null) return;

        string? warningText = null;
        if (validationResult != null)
        {
            foreach (var warning in validationResult.Warnings)
            {
                if (warning.Contains(platformName))
                {
                    warningText = warning.Contains("missing") ? "\u26a0 Missing" : "\u26a0 Duplicate";
                    break;
                }
            }
        }

        label.Visible = warningText != null;
        label.Text = warningText ?? string.Empty;
    }

    /// <summary>
    /// Clears all validation labels
    /// </summary>
    public void ClearValidation()
    {
        if (_internalIdErrorLabel != null)
        {
            _internalIdErrorLabel.Visible = false;
            _internalIdErrorLabel.Text = string.Empty;
        }
        if (_steamWarningLabel != null)
        {
            _steamWarningLabel.Visible = false;
            _steamWarningLabel.Text = string.Empty;
        }
        if (_googlePlayWarningLabel != null)
        {
            _googlePlayWarningLabel.Visible = false;
            _googlePlayWarningLabel.Text = string.Empty;
        }
        if (_gameCenterWarningLabel != null)
        {
            _gameCenterWarningLabel.Visible = false;
            _gameCenterWarningLabel.Text = string.Empty;
        }
    }

    #region Name Field Handlers

    private void OnNameFocusEntered()
    {
        if (_currentAchievement == null) return;
        _nameBeforeEdit = _currentAchievement.DisplayName ?? string.Empty;
    }

    private void OnNameChanged(string newName)
    {
        if (_isUpdating || _currentAchievement == null) return;

        _currentAchievement.DisplayName = newName;
        EmitSignal(SignalName.AchievementDisplayNameChanged, _currentAchievement);
        EmitSignal(SignalName.AchievementChanged);
    }

    private void OnNameFocusExited()
    {
        if (_currentAchievement == null) return;

        var newName = _currentAchievement.DisplayName ?? string.Empty;
        if (_nameBeforeEdit != newName && _undoRedoManager != null)
        {
            var achievement = _currentAchievement;
            var oldValue = _nameBeforeEdit;

            _undoRedoManager.CreateAction("Change Achievement Name");
            _undoRedoManager.AddDoMethod(Callable.From(() => SetAchievementName(achievement, newName)));
            _undoRedoManager.AddUndoMethod(Callable.From(() => SetAchievementName(achievement, oldValue)));
            _undoRedoManager.CommitAction(false);
        }
    }

    private void SetAchievementName(Achievement achievement, string name)
    {
        achievement.DisplayName = name;
        if (_currentAchievement == achievement)
        {
            _isUpdating = true;
            if (NameLineEdit != null)
                NameLineEdit.Text = name;
            _isUpdating = false;
        }
        EmitSignal(SignalName.AchievementDisplayNameChanged, achievement);
        EmitSignal(SignalName.AchievementChanged);
    }

    #endregion

    #region Internal ID Field Handlers

    private void OnIdFocusEntered()
    {
        if (_currentAchievement == null) return;
        _idBeforeEdit = _currentAchievement.Id ?? string.Empty;
    }

    private void OnIdTextChanged(string newId)
    {
        if (_isUpdating || _currentAchievement == null) return;

        _currentAchievement.Id = newId;
        _previousId = newId;
        EmitSignal(SignalName.AchievementChanged);
    }

    private void OnIdFocusExited()
    {
        if (_currentAchievement == null) return;

        var newId = _currentAchievement.Id ?? string.Empty;
        if (_idBeforeEdit != newId)
        {
            if (_undoRedoManager != null)
            {
                var achievement = _currentAchievement;
                var oldValue = _idBeforeEdit;

                _undoRedoManager.CreateAction("Change Achievement ID");
                _undoRedoManager.AddDoMethod(Callable.From(() => SetAchievementId(achievement, newId)));
                _undoRedoManager.AddUndoMethod(Callable.From(() => SetAchievementId(achievement, oldValue)));
                _undoRedoManager.CommitAction(false);
            }

            EmitSignal(SignalName.AchievementIdChanged, _currentAchievement, _idBeforeEdit, newId);
        }
    }

    private void SetAchievementId(Achievement achievement, string id)
    {
        achievement.Id = id;
        if (_currentAchievement == achievement)
        {
            _isUpdating = true;
            if (InternalIDLineEdit != null)
                InternalIDLineEdit.Text = id;
            _isUpdating = false;
        }
        _previousId = id;
        EmitSignal(SignalName.AchievementChanged);
    }

    #endregion

    #region Description Field Handlers

    private void OnDescriptionFocusEntered()
    {
        if (_currentAchievement == null) return;
        _descriptionBeforeEdit = _currentAchievement.Description ?? string.Empty;
    }

    private void OnDescriptionChanged()
    {
        if (_isUpdating || _currentAchievement == null || DescriptionTextBox == null) return;

        _currentAchievement.Description = DescriptionTextBox.Text;
        EmitSignal(SignalName.AchievementChanged);
    }

    private void OnDescriptionFocusExited()
    {
        if (_currentAchievement == null || DescriptionTextBox == null) return;

        var newDescription = _currentAchievement.Description ?? string.Empty;
        if (_descriptionBeforeEdit != newDescription && _undoRedoManager != null)
        {
            var achievement = _currentAchievement;
            var oldValue = _descriptionBeforeEdit;

            _undoRedoManager.CreateAction("Change Achievement Description");
            _undoRedoManager.AddDoMethod(Callable.From(() => SetAchievementDescription(achievement, newDescription)));
            _undoRedoManager.AddUndoMethod(Callable.From(() => SetAchievementDescription(achievement, oldValue)));
            _undoRedoManager.CommitAction(false);
        }
    }

    private void SetAchievementDescription(Achievement achievement, string description)
    {
        achievement.Description = description;
        if (_currentAchievement == achievement)
        {
            _isUpdating = true;
            if (DescriptionTextBox != null)
                DescriptionTextBox.Text = description;
            _isUpdating = false;
        }
        EmitSignal(SignalName.AchievementChanged);
    }

    #endregion

    #region Platform ID Field Handlers

    private void OnSteamIdFocusEntered()
    {
        if (_currentAchievement == null) return;
        _steamIdBeforeEdit = _currentAchievement.SteamId ?? string.Empty;
    }

    private void OnSteamIdChanged(string text)
    {
        if (_isUpdating || _currentAchievement == null) return;

        _currentAchievement.SteamId = text;
        EmitSignal(SignalName.AchievementChanged);
    }

    private void OnSteamIdFocusExited()
    {
        if (_currentAchievement == null) return;

        var newSteamId = _currentAchievement.SteamId ?? string.Empty;
        if (_steamIdBeforeEdit != newSteamId && _undoRedoManager != null)
        {
            var achievement = _currentAchievement;
            var oldValue = _steamIdBeforeEdit;

            _undoRedoManager.CreateAction("Change Steam ID");
            _undoRedoManager.AddDoMethod(Callable.From(() => SetAchievementSteamId(achievement, newSteamId)));
            _undoRedoManager.AddUndoMethod(Callable.From(() => SetAchievementSteamId(achievement, oldValue)));
            _undoRedoManager.CommitAction(false);
        }
    }

    private void SetAchievementSteamId(Achievement achievement, string steamId)
    {
        achievement.SteamId = steamId;
        if (_currentAchievement == achievement)
        {
            _isUpdating = true;
            if (SteamIDLineEdit != null)
                SteamIDLineEdit.Text = steamId;
            _isUpdating = false;
        }
        EmitSignal(SignalName.AchievementChanged);
    }

    private void OnGooglePlayIdFocusEntered()
    {
        if (_currentAchievement == null) return;
        _googlePlayIdBeforeEdit = _currentAchievement.GooglePlayId ?? string.Empty;
    }

    private void OnGooglePlayIdChanged(string text)
    {
        if (_isUpdating || _currentAchievement == null) return;

        _currentAchievement.GooglePlayId = text;
        EmitSignal(SignalName.AchievementChanged);
    }

    private void OnGooglePlayIdFocusExited()
    {
        if (_currentAchievement == null) return;

        var newGooglePlayId = _currentAchievement.GooglePlayId ?? string.Empty;
        if (_googlePlayIdBeforeEdit != newGooglePlayId && _undoRedoManager != null)
        {
            var achievement = _currentAchievement;
            var oldValue = _googlePlayIdBeforeEdit;

            _undoRedoManager.CreateAction("Change Google Play ID");
            _undoRedoManager.AddDoMethod(Callable.From(() => SetAchievementGooglePlayId(achievement, newGooglePlayId)));
            _undoRedoManager.AddUndoMethod(Callable.From(() => SetAchievementGooglePlayId(achievement, oldValue)));
            _undoRedoManager.CommitAction(false);
        }
    }

    private void SetAchievementGooglePlayId(Achievement achievement, string googlePlayId)
    {
        achievement.GooglePlayId = googlePlayId;
        if (_currentAchievement == achievement)
        {
            _isUpdating = true;
            if (GooglePlayIDLineEdit != null)
                GooglePlayIDLineEdit.Text = googlePlayId;
            _isUpdating = false;
        }
        EmitSignal(SignalName.AchievementChanged);
    }

    private void OnGameCenterIdFocusEntered()
    {
        if (_currentAchievement == null) return;
        _gameCenterIdBeforeEdit = _currentAchievement.GameCenterId ?? string.Empty;
    }

    private void OnGameCenterIdChanged(string text)
    {
        if (_isUpdating || _currentAchievement == null) return;

        _currentAchievement.GameCenterId = text;
        EmitSignal(SignalName.AchievementChanged);
    }

    private void OnGameCenterIdFocusExited()
    {
        if (_currentAchievement == null) return;

        var newGameCenterId = _currentAchievement.GameCenterId ?? string.Empty;
        if (_gameCenterIdBeforeEdit != newGameCenterId && _undoRedoManager != null)
        {
            var achievement = _currentAchievement;
            var oldValue = _gameCenterIdBeforeEdit;

            _undoRedoManager.CreateAction("Change Game Center ID");
            _undoRedoManager.AddDoMethod(Callable.From(() => SetAchievementGameCenterId(achievement, newGameCenterId)));
            _undoRedoManager.AddUndoMethod(Callable.From(() => SetAchievementGameCenterId(achievement, oldValue)));
            _undoRedoManager.CommitAction(false);
        }
    }

    private void SetAchievementGameCenterId(Achievement achievement, string gameCenterId)
    {
        achievement.GameCenterId = gameCenterId;
        if (_currentAchievement == achievement)
        {
            _isUpdating = true;
            if (GameCenterIDLineEdit != null)
                GameCenterIDLineEdit.Text = gameCenterId;
            _isUpdating = false;
        }
        EmitSignal(SignalName.AchievementChanged);
    }

    #endregion

    #region Progress and Value Handlers

    private void OnTrackProgressToggled(bool enabled)
    {
        if (_isUpdating || _currentAchievement == null) return;

        var oldValue = _currentAchievement.IsIncremental;
        _currentAchievement.IsIncremental = enabled;
        if (TargetValueSpinBox != null)
            TargetValueSpinBox.Editable = enabled;

        if (_undoRedoManager != null && oldValue != enabled)
        {
            var achievement = _currentAchievement;

            _undoRedoManager.CreateAction("Change Track Progress");
            _undoRedoManager.AddDoMethod(Callable.From(() => SetAchievementTrackProgress(achievement, enabled)));
            _undoRedoManager.AddUndoMethod(Callable.From(() => SetAchievementTrackProgress(achievement, oldValue)));
            _undoRedoManager.CommitAction(false);
        }

        EmitSignal(SignalName.AchievementChanged);
    }

    private void SetAchievementTrackProgress(Achievement achievement, bool enabled)
    {
        achievement.IsIncremental = enabled;
        if (_currentAchievement == achievement)
        {
            _isUpdating = true;
            if (TrackProgressCheckBox != null)
                TrackProgressCheckBox.ButtonPressed = enabled;
            if (TargetValueSpinBox != null)
                TargetValueSpinBox.Editable = enabled;
            _isUpdating = false;
        }
        EmitSignal(SignalName.AchievementChanged);
    }

    private void OnTargetValueChanged(double value)
    {
        if (_isUpdating || _currentAchievement == null) return;

        var oldValue = _currentAchievement.MaxProgress;
        var newValue = (int)value;
        _currentAchievement.MaxProgress = newValue;

        if (_undoRedoManager != null && oldValue != newValue)
        {
            var achievement = _currentAchievement;

            _undoRedoManager.CreateAction("Change Target Value");
            _undoRedoManager.AddDoMethod(Callable.From(() => SetAchievementMaxProgress(achievement, newValue)));
            _undoRedoManager.AddUndoMethod(Callable.From(() => SetAchievementMaxProgress(achievement, oldValue)));
            _undoRedoManager.CommitAction(false);
        }

        EmitSignal(SignalName.AchievementChanged);
    }

    private void SetAchievementMaxProgress(Achievement achievement, int maxProgress)
    {
        achievement.MaxProgress = maxProgress;
        if (_currentAchievement == achievement)
        {
            _isUpdating = true;
            if (TargetValueSpinBox != null)
                TargetValueSpinBox.Value = maxProgress;
            _isUpdating = false;
        }
        EmitSignal(SignalName.AchievementChanged);
    }

    #endregion

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

        // Clean up validation labels
        _internalIdErrorLabel?.QueueFree();
        _steamWarningLabel?.QueueFree();
        _googlePlayWarningLabel?.QueueFree();
        _gameCenterWarningLabel?.QueueFree();

        // Disconnect signals
        if (NameLineEdit != null)
        {
            NameLineEdit.TextChanged -= OnNameChanged;
            NameLineEdit.FocusEntered -= OnNameFocusEntered;
            NameLineEdit.FocusExited -= OnNameFocusExited;
        }
        if (InternalIDLineEdit != null)
        {
            InternalIDLineEdit.TextChanged -= OnIdTextChanged;
            InternalIDLineEdit.FocusEntered -= OnIdFocusEntered;
            InternalIDLineEdit.FocusExited -= OnIdFocusExited;
        }
        if (DescriptionTextBox != null)
        {
            DescriptionTextBox.TextChanged -= OnDescriptionChanged;
            DescriptionTextBox.FocusEntered -= OnDescriptionFocusEntered;
            DescriptionTextBox.FocusExited -= OnDescriptionFocusExited;
        }
        if (TrackProgressCheckBox != null)
            TrackProgressCheckBox.Toggled -= OnTrackProgressToggled;
        if (TargetValueSpinBox != null)
            TargetValueSpinBox.ValueChanged -= OnTargetValueChanged;
        if (SteamIDLineEdit != null)
        {
            SteamIDLineEdit.TextChanged -= OnSteamIdChanged;
            SteamIDLineEdit.FocusEntered -= OnSteamIdFocusEntered;
            SteamIDLineEdit.FocusExited -= OnSteamIdFocusExited;
        }
        if (GooglePlayIDLineEdit != null)
        {
            GooglePlayIDLineEdit.TextChanged -= OnGooglePlayIdChanged;
            GooglePlayIDLineEdit.FocusEntered -= OnGooglePlayIdFocusEntered;
            GooglePlayIDLineEdit.FocusExited -= OnGooglePlayIdFocusExited;
        }
        if (GameCenterIDLineEdit != null)
        {
            GameCenterIDLineEdit.TextChanged -= OnGameCenterIdChanged;
            GameCenterIDLineEdit.FocusEntered -= OnGameCenterIdFocusEntered;
            GameCenterIDLineEdit.FocusExited -= OnGameCenterIdFocusExited;
        }
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

    private void OnIconResourceChanged(Resource resource)
    {
        if (_isUpdating || _currentAchievement == null)
            return;

        var texture = resource as Texture2D;
        var oldIcon = _currentAchievement.Icon;
        _currentAchievement.Icon = texture;
        AchievementIconButton.TextureNormal = texture;

        if (_undoRedoManager != null)
        {
            var achievement = _currentAchievement;

            _undoRedoManager.CreateAction("Change Achievement Icon");
            _undoRedoManager.AddDoMethod(Callable.From(() => SetAchievementIcon(achievement, texture)));
            _undoRedoManager.AddUndoMethod(Callable.From(() => SetAchievementIcon(achievement, oldIcon)));
            _undoRedoManager.CommitAction(false);
        }

        EmitSignal(SignalName.AchievementDisplayNameChanged, _currentAchievement);
        EmitSignal(SignalName.AchievementChanged);
    }

    private void SetAchievementIcon(Achievement achievement, Texture2D? icon)
    {
        achievement.Icon = icon;
        if (_currentAchievement == achievement)
        {
            _isUpdating = true;
            if (AchievementIconButton != null)
                AchievementIconButton.TextureNormal = icon;
            if (_iconPicker != null)
                _iconPicker.EditedResource = icon;
            _isUpdating = false;
        }
        EmitSignal(SignalName.AchievementDisplayNameChanged, achievement);
        EmitSignal(SignalName.AchievementChanged);
    }
}
#endif
