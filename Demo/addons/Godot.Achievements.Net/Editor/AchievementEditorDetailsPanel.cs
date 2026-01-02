#if TOOLS
using Godot;
using Godot.Achievements.Core;
using System;

namespace Godot.Achievements.Core.Editor;

/// <summary>
/// Details panel for editing individual achievement properties
/// </summary>
[Tool]
public partial class AchievementEditorDetailsPanel : PanelContainer
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
    private LineEdit SteamStatIdLineEdit = null!;
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
    private CustomPropertiesEditor? _customPropertiesEditor;

    // Track old values for undo/redo
    private string _nameBeforeEdit = string.Empty;
    private string _descriptionBeforeEdit = string.Empty;
    private string _steamIdBeforeEdit = string.Empty;
    private string _steamStatIdBeforeEdit = string.Empty;
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
        _customPropertiesEditor?.SetUndoRedoManager(undoRedoManager);
    }

    /// <summary>
    /// Sets the database reference for propagating property keys across all achievements
    /// </summary>
    public void SetDatabase(AchievementDatabase? database)
    {
        _customPropertiesEditor?.SetDatabase(database);
    }

    public override void _Ready()
    {
        // Create and setup icon picker
        _iconPicker = new EditorResourcePicker();
        _iconPicker.BaseType = "Texture2D";
        _iconPicker.ResourceChanged += OnIconResourceChanged;

        // Add icon picker to the container
        IconPickerContainer.AddChild(_iconPicker);

        // Connect field signals
        NameLineEdit.TextChanged += OnNameChanged;
        NameLineEdit.FocusEntered += OnNameFocusEntered;
        NameLineEdit.FocusExited += OnNameFocusExited;

        InternalIDLineEdit.TextChanged += OnIdTextChanged;
        InternalIDLineEdit.FocusEntered += OnIdFocusEntered;
        InternalIDLineEdit.FocusExited += OnIdFocusExited;

        DescriptionTextBox.TextChanged += OnDescriptionChanged;
        DescriptionTextBox.FocusEntered += OnDescriptionFocusEntered;
        DescriptionTextBox.FocusExited += OnDescriptionFocusExited;

        TrackProgressCheckBox.Toggled += OnTrackProgressToggled;
        TargetValueSpinBox.ValueChanged += OnTargetValueChanged;

        SteamIDLineEdit.TextChanged += OnSteamIdChanged;
        SteamIDLineEdit.FocusEntered += OnSteamIdFocusEntered;
        SteamIDLineEdit.FocusExited += OnSteamIdFocusExited;

        SteamStatIdLineEdit.TextChanged += OnSteamStatIdChanged;
        SteamStatIdLineEdit.FocusEntered += OnSteamStatIdFocusEntered;
        SteamStatIdLineEdit.FocusExited += OnSteamStatIdFocusExited;

        GooglePlayIDLineEdit.TextChanged += OnGooglePlayIdChanged;
        GooglePlayIDLineEdit.FocusEntered += OnGooglePlayIdFocusEntered;
        GooglePlayIDLineEdit.FocusExited += OnGooglePlayIdFocusExited;

        GameCenterIDLineEdit.TextChanged += OnGameCenterIdChanged;
        GameCenterIDLineEdit.FocusEntered += OnGameCenterIdFocusEntered;
        GameCenterIDLineEdit.FocusExited += OnGameCenterIdFocusExited;

        VisualizeUnlockButton.Pressed += OnVisualizeUnlockPressed;

        // Create and setup custom properties editor
        _customPropertiesEditor = new CustomPropertiesEditor();
        _customPropertiesEditor.PropertyChanged += OnCustomPropertyChanged;
        CustomPropertiesContainer.AddChild(_customPropertiesEditor);

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
        UpdateFieldWarningLabel(_steamWarningLabel, validationResult, ValidationFields.SteamId);
        UpdateFieldWarningLabel(_googlePlayWarningLabel, validationResult, ValidationFields.GooglePlayId);
        UpdateFieldWarningLabel(_gameCenterWarningLabel, validationResult, ValidationFields.GameCenterId);
    }

    private void UpdateFieldWarningLabel(Label? label, AchievementValidationResult? validationResult, string fieldKey)
    {
        if (label == null) return;

        string? warningText = null;
        if (validationResult != null && validationResult.FieldWarnings.TryGetValue(fieldKey, out var warningType))
        {
            warningText = warningType == ValidationWarningType.Missing ? "\u26a0 Missing" : "\u26a0 Duplicate";
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
        NameLineEdit.CaretColumn = NameLineEdit.Text.Length;
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
            _undoRedoManager.AddDoMethod(this, nameof(SetAchievementName), achievement, newName);
            _undoRedoManager.AddUndoMethod(this, nameof(SetAchievementName), achievement, oldValue);
            _undoRedoManager.CommitAction(false);
        }
    }

    private void SetAchievementName(Achievement achievement, string name)
    {
        achievement.DisplayName = name;
        if (_currentAchievement == achievement)
        {
            _isUpdating = true;
            NameLineEdit.Text = name;
            NameLineEdit.CaretColumn = name.Length;
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
        InternalIDLineEdit.CaretColumn = InternalIDLineEdit.Text.Length;
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
                _undoRedoManager.AddDoMethod(this, nameof(SetAchievementId), achievement, newId);
                _undoRedoManager.AddUndoMethod(this, nameof(SetAchievementId), achievement, oldValue);
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
            InternalIDLineEdit.Text = id;
            InternalIDLineEdit.CaretColumn = id.Length;
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
        var lastLine = DescriptionTextBox.GetLineCount() - 1;
        DescriptionTextBox.SetCaretLine(lastLine);
        DescriptionTextBox.SetCaretColumn(DescriptionTextBox.GetLine(lastLine).Length);
    }

    private void OnDescriptionChanged()
    {
        if (_isUpdating || _currentAchievement == null) return;

        _currentAchievement.Description = DescriptionTextBox.Text;
        EmitSignal(SignalName.AchievementChanged);
    }

    private void OnDescriptionFocusExited()
    {
        if (_currentAchievement == null) return;

        var newDescription = _currentAchievement.Description ?? string.Empty;
        if (_descriptionBeforeEdit != newDescription && _undoRedoManager != null)
        {
            var achievement = _currentAchievement;
            var oldValue = _descriptionBeforeEdit;

            _undoRedoManager.CreateAction("Change Achievement Description");
            _undoRedoManager.AddDoMethod(this, nameof(SetAchievementDescription), achievement, newDescription);
            _undoRedoManager.AddUndoMethod(this, nameof(SetAchievementDescription), achievement, oldValue);
            _undoRedoManager.CommitAction(false);
        }
    }

    private void SetAchievementDescription(Achievement achievement, string description)
    {
        achievement.Description = description;
        if (_currentAchievement == achievement)
        {
            _isUpdating = true;
            DescriptionTextBox.Text = description;
            var lastLine = DescriptionTextBox.GetLineCount() - 1;
            DescriptionTextBox.SetCaretLine(lastLine);
            DescriptionTextBox.SetCaretColumn(DescriptionTextBox.GetLine(lastLine).Length);
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
        SteamIDLineEdit.CaretColumn = SteamIDLineEdit.Text.Length;
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
            _undoRedoManager.AddDoMethod(this, nameof(SetAchievementSteamId), achievement, newSteamId);
            _undoRedoManager.AddUndoMethod(this, nameof(SetAchievementSteamId), achievement, oldValue);
            _undoRedoManager.CommitAction(false);
        }
    }

    private void SetAchievementSteamId(Achievement achievement, string steamId)
    {
        achievement.SteamId = steamId;
        if (_currentAchievement == achievement)
        {
            _isUpdating = true;
            SteamIDLineEdit.Text = steamId;
            SteamIDLineEdit.CaretColumn = steamId.Length;
            _isUpdating = false;
        }
        EmitSignal(SignalName.AchievementChanged);
    }

    private void OnSteamStatIdFocusEntered()
    {
        if (_currentAchievement == null) return;
        _steamStatIdBeforeEdit = _currentAchievement.SteamStatId ?? string.Empty;
        SteamStatIdLineEdit.CaretColumn = SteamStatIdLineEdit.Text.Length;
    }

    private void OnSteamStatIdChanged(string text)
    {
        if (_isUpdating || _currentAchievement == null) return;

        _currentAchievement.SteamStatId = text;
        EmitSignal(SignalName.AchievementChanged);
    }

    private void OnSteamStatIdFocusExited()
    {
        if (_currentAchievement == null) return;

        var newSteamStatId = _currentAchievement.SteamStatId ?? string.Empty;
        if (_steamStatIdBeforeEdit != newSteamStatId && _undoRedoManager != null)
        {
            var achievement = _currentAchievement;
            var oldValue = _steamStatIdBeforeEdit;

            _undoRedoManager.CreateAction("Change Steam Stat ID");
            _undoRedoManager.AddDoMethod(this, nameof(SetAchievementSteamStatId), achievement, newSteamStatId);
            _undoRedoManager.AddUndoMethod(this, nameof(SetAchievementSteamStatId), achievement, oldValue);
            _undoRedoManager.CommitAction(false);
        }
    }

    private void SetAchievementSteamStatId(Achievement achievement, string steamStatId)
    {
        achievement.SteamStatId = steamStatId;
        if (_currentAchievement == achievement)
        {
            _isUpdating = true;
            SteamStatIdLineEdit.Text = steamStatId;
            SteamStatIdLineEdit.CaretColumn = steamStatId.Length;
            _isUpdating = false;
        }
        EmitSignal(SignalName.AchievementChanged);
    }

    private void OnGooglePlayIdFocusEntered()
    {
        if (_currentAchievement == null) return;
        _googlePlayIdBeforeEdit = _currentAchievement.GooglePlayId ?? string.Empty;
        GooglePlayIDLineEdit.CaretColumn = GooglePlayIDLineEdit.Text.Length;
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
            _undoRedoManager.AddDoMethod(this, nameof(SetAchievementGooglePlayId), achievement, newGooglePlayId);
            _undoRedoManager.AddUndoMethod(this, nameof(SetAchievementGooglePlayId), achievement, oldValue);
            _undoRedoManager.CommitAction(false);
        }
    }

    private void SetAchievementGooglePlayId(Achievement achievement, string googlePlayId)
    {
        achievement.GooglePlayId = googlePlayId;
        if (_currentAchievement == achievement)
        {
            _isUpdating = true;
            GooglePlayIDLineEdit.Text = googlePlayId;
            GooglePlayIDLineEdit.CaretColumn = googlePlayId.Length;
            _isUpdating = false;
        }
        EmitSignal(SignalName.AchievementChanged);
    }

    private void OnGameCenterIdFocusEntered()
    {
        if (_currentAchievement == null) return;
        _gameCenterIdBeforeEdit = _currentAchievement.GameCenterId ?? string.Empty;
        GameCenterIDLineEdit.CaretColumn = GameCenterIDLineEdit.Text.Length;
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
            _undoRedoManager.AddDoMethod(this, nameof(SetAchievementGameCenterId), achievement, newGameCenterId);
            _undoRedoManager.AddUndoMethod(this, nameof(SetAchievementGameCenterId), achievement, oldValue);
            _undoRedoManager.CommitAction(false);
        }
    }

    private void SetAchievementGameCenterId(Achievement achievement, string gameCenterId)
    {
        achievement.GameCenterId = gameCenterId;
        if (_currentAchievement == achievement)
        {
            _isUpdating = true;
            GameCenterIDLineEdit.Text = gameCenterId;
            GameCenterIDLineEdit.CaretColumn = gameCenterId.Length;
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
        TargetValueSpinBox.Editable = enabled;

        if (_undoRedoManager != null && oldValue != enabled)
        {
            var achievement = _currentAchievement;

            _undoRedoManager.CreateAction("Change Track Progress");
            _undoRedoManager.AddDoMethod(this, nameof(SetAchievementTrackProgress), achievement, enabled);
            _undoRedoManager.AddUndoMethod(this, nameof(SetAchievementTrackProgress), achievement, oldValue);
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
            TrackProgressCheckBox.ButtonPressed = enabled;
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
            _undoRedoManager.AddDoMethod(this, nameof(SetAchievementMaxProgress), achievement, newValue);
            _undoRedoManager.AddUndoMethod(this, nameof(SetAchievementMaxProgress), achievement, oldValue);
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
            TargetValueSpinBox.Value = maxProgress;
            _isUpdating = false;
        }
        EmitSignal(SignalName.AchievementChanged);
    }

    #endregion

    #region Custom Properties Handlers

    private void OnCustomPropertyChanged()
    {
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

        if (_customPropertiesEditor != null)
        {
            _customPropertiesEditor.PropertyChanged -= OnCustomPropertyChanged;
            _customPropertiesEditor.QueueFree();
        }

        // Clean up validation labels
        _internalIdErrorLabel?.QueueFree();
        _steamWarningLabel?.QueueFree();
        _googlePlayWarningLabel?.QueueFree();
        _gameCenterWarningLabel?.QueueFree();

        // Disconnect signals
        NameLineEdit.TextChanged -= OnNameChanged;
        NameLineEdit.FocusEntered -= OnNameFocusEntered;
        NameLineEdit.FocusExited -= OnNameFocusExited;

        InternalIDLineEdit.TextChanged -= OnIdTextChanged;
        InternalIDLineEdit.FocusEntered -= OnIdFocusEntered;
        InternalIDLineEdit.FocusExited -= OnIdFocusExited;

        DescriptionTextBox.TextChanged -= OnDescriptionChanged;
        DescriptionTextBox.FocusEntered -= OnDescriptionFocusEntered;
        DescriptionTextBox.FocusExited -= OnDescriptionFocusExited;

        TrackProgressCheckBox.Toggled -= OnTrackProgressToggled;
        TargetValueSpinBox.ValueChanged -= OnTargetValueChanged;

        SteamIDLineEdit.TextChanged -= OnSteamIdChanged;
        SteamIDLineEdit.FocusEntered -= OnSteamIdFocusEntered;
        SteamIDLineEdit.FocusExited -= OnSteamIdFocusExited;

        SteamStatIdLineEdit.TextChanged -= OnSteamStatIdChanged;
        SteamStatIdLineEdit.FocusEntered -= OnSteamStatIdFocusEntered;
        SteamStatIdLineEdit.FocusExited -= OnSteamStatIdFocusExited;

        GooglePlayIDLineEdit.TextChanged -= OnGooglePlayIdChanged;
        GooglePlayIDLineEdit.FocusEntered -= OnGooglePlayIdFocusEntered;
        GooglePlayIDLineEdit.FocusExited -= OnGooglePlayIdFocusExited;

        GameCenterIDLineEdit.TextChanged -= OnGameCenterIdChanged;
        GameCenterIDLineEdit.FocusEntered -= OnGameCenterIdFocusEntered;
        GameCenterIDLineEdit.FocusExited -= OnGameCenterIdFocusExited;

        VisualizeUnlockButton.Pressed -= OnVisualizeUnlockPressed;
    }

    private void LoadAchievementData()
    {
        // Guard against being called before export fields are initialized
        if (NameLineEdit == null)
            return;

        if (_currentAchievement == null)
        {
            ClearFields();
            return;
        }

        _isUpdating = true;

        // Replace LineEdits with fresh instances to clear undo history
        ReplaceNameLineEdit(_currentAchievement.DisplayName ?? string.Empty);
        ReplaceInternalIdLineEdit(_currentAchievement.Id ?? string.Empty);
        _idBeforeEdit = _currentAchievement.Id ?? string.Empty;
        DescriptionTextBox.Text = _currentAchievement.Description ?? string.Empty;
        DescriptionTextBox.ClearUndoHistory();

        TrackProgressCheckBox.ButtonPressed = _currentAchievement.IsIncremental;
        TargetValueSpinBox.Value = _currentAchievement.MaxProgress;
        TargetValueSpinBox.Editable = _currentAchievement.IsIncremental;

        if (_currentAchievement.Icon != null)
        {
            AchievementIconButton.TextureNormal = _currentAchievement.Icon;
            if (_iconPicker != null)
                _iconPicker.EditedResource = _currentAchievement.Icon;
        }
        else
        {
            AchievementIconButton.TextureNormal = null;
            if (_iconPicker != null)
                _iconPicker.EditedResource = null;
        }

        ReplaceSteamIdLineEdit(_currentAchievement.SteamId ?? string.Empty);
        ReplaceSteamStatIdLineEdit(_currentAchievement.SteamStatId ?? string.Empty);
        ReplaceGooglePlayIdLineEdit(_currentAchievement.GooglePlayId ?? string.Empty);
        ReplaceGameCenterIdLineEdit(_currentAchievement.GameCenterId ?? string.Empty);

        // Update custom properties editor
        if (_customPropertiesEditor != null)
        {
            _customPropertiesEditor.CurrentAchievement = _currentAchievement;
        }

        _isUpdating = false;
    }

    private void ClearFields()
    {
        if (NameLineEdit == null)
            return;

        _isUpdating = true;

        ReplaceNameLineEdit(string.Empty);
        ReplaceInternalIdLineEdit(string.Empty);
        DescriptionTextBox.Text = string.Empty;
        DescriptionTextBox.ClearUndoHistory();
        TrackProgressCheckBox.ButtonPressed = false;
        TargetValueSpinBox.Value = 1;
        TargetValueSpinBox.Editable = false;
        AchievementIconButton.TextureNormal = null;
        if (_iconPicker != null)
            _iconPicker.EditedResource = null;
        ReplaceSteamIdLineEdit(string.Empty);
        ReplaceSteamStatIdLineEdit(string.Empty);
        ReplaceGooglePlayIdLineEdit(string.Empty);
        ReplaceGameCenterIdLineEdit(string.Empty);

        // Clear custom properties editor
        if (_customPropertiesEditor != null)
        {
            _customPropertiesEditor.CurrentAchievement = null;
        }

        _isUpdating = false;
    }

    /// <summary>
    /// Replaces a LineEdit with a fresh instance to clear its internal undo/redo history.
    /// </summary>
    /// <remarks>
    /// Godot's LineEdit maintains an internal undo/redo stack that cannot be cleared via any public API.
    /// When switching between achievements, we must replace the entire control to prevent Ctrl+Z from
    /// restoring text from a previously selected achievement. This is the only way to reset the history.
    /// </remarks>
    /// <returns>The new LineEdit instance that replaces the old one.</returns>
    private static LineEdit ReplaceLineEdit(LineEdit oldLineEdit, string newText)
    {
        var parent = oldLineEdit.GetParent();
        if (parent == null)
        {
            oldLineEdit.Text = newText;
            return oldLineEdit;
        }

        // Create new LineEdit with same properties
        var newLineEdit = new LineEdit
        {
            Text = newText,
            Name = oldLineEdit.Name,
            PlaceholderText = oldLineEdit.PlaceholderText,
            Editable = oldLineEdit.Editable,
            CustomMinimumSize = oldLineEdit.CustomMinimumSize,
            SizeFlagsHorizontal = oldLineEdit.SizeFlagsHorizontal,
            SizeFlagsVertical = oldLineEdit.SizeFlagsVertical,
            FocusMode = oldLineEdit.FocusMode,
            ExpandToTextLength = oldLineEdit.ExpandToTextLength,
            MaxLength = oldLineEdit.MaxLength,
            Flat = oldLineEdit.Flat
        };

        // Replace in tree
        var index = oldLineEdit.GetIndex();
        parent.RemoveChild(oldLineEdit);
        parent.AddChild(newLineEdit);
        parent.MoveChild(newLineEdit, index);
        oldLineEdit.QueueFree();

        return newLineEdit;
    }

    private void ReplaceNameLineEdit(string text)
    {
        NameLineEdit.TextChanged -= OnNameChanged;
        NameLineEdit.FocusEntered -= OnNameFocusEntered;
        NameLineEdit.FocusExited -= OnNameFocusExited;
        NameLineEdit = ReplaceLineEdit(NameLineEdit, text);
        NameLineEdit.TextChanged += OnNameChanged;
        NameLineEdit.FocusEntered += OnNameFocusEntered;
        NameLineEdit.FocusExited += OnNameFocusExited;
    }

    private void ReplaceInternalIdLineEdit(string text)
    {
        InternalIDLineEdit.TextChanged -= OnIdTextChanged;
        InternalIDLineEdit.FocusEntered -= OnIdFocusEntered;
        InternalIDLineEdit.FocusExited -= OnIdFocusExited;
        InternalIDLineEdit = ReplaceLineEdit(InternalIDLineEdit, text);
        InternalIDLineEdit.TextChanged += OnIdTextChanged;
        InternalIDLineEdit.FocusEntered += OnIdFocusEntered;
        InternalIDLineEdit.FocusExited += OnIdFocusExited;
    }

    private void ReplaceSteamIdLineEdit(string text)
    {
        SteamIDLineEdit.TextChanged -= OnSteamIdChanged;
        SteamIDLineEdit.FocusEntered -= OnSteamIdFocusEntered;
        SteamIDLineEdit.FocusExited -= OnSteamIdFocusExited;
        SteamIDLineEdit = ReplaceLineEdit(SteamIDLineEdit, text);
        SteamIDLineEdit.TextChanged += OnSteamIdChanged;
        SteamIDLineEdit.FocusEntered += OnSteamIdFocusEntered;
        SteamIDLineEdit.FocusExited += OnSteamIdFocusExited;
    }

    private void ReplaceSteamStatIdLineEdit(string text)
    {
        SteamStatIdLineEdit.TextChanged -= OnSteamStatIdChanged;
        SteamStatIdLineEdit.FocusEntered -= OnSteamStatIdFocusEntered;
        SteamStatIdLineEdit.FocusExited -= OnSteamStatIdFocusExited;
        SteamStatIdLineEdit = ReplaceLineEdit(SteamStatIdLineEdit, text);
        SteamStatIdLineEdit.TextChanged += OnSteamStatIdChanged;
        SteamStatIdLineEdit.FocusEntered += OnSteamStatIdFocusEntered;
        SteamStatIdLineEdit.FocusExited += OnSteamStatIdFocusExited;
    }

    private void ReplaceGooglePlayIdLineEdit(string text)
    {
        GooglePlayIDLineEdit.TextChanged -= OnGooglePlayIdChanged;
        GooglePlayIDLineEdit.FocusEntered -= OnGooglePlayIdFocusEntered;
        GooglePlayIDLineEdit.FocusExited -= OnGooglePlayIdFocusExited;
        GooglePlayIDLineEdit = ReplaceLineEdit(GooglePlayIDLineEdit, text);
        GooglePlayIDLineEdit.TextChanged += OnGooglePlayIdChanged;
        GooglePlayIDLineEdit.FocusEntered += OnGooglePlayIdFocusEntered;
        GooglePlayIDLineEdit.FocusExited += OnGooglePlayIdFocusExited;
    }

    private void ReplaceGameCenterIdLineEdit(string text)
    {
        GameCenterIDLineEdit.TextChanged -= OnGameCenterIdChanged;
        GameCenterIDLineEdit.FocusEntered -= OnGameCenterIdFocusEntered;
        GameCenterIDLineEdit.FocusExited -= OnGameCenterIdFocusExited;
        GameCenterIDLineEdit = ReplaceLineEdit(GameCenterIDLineEdit, text);
        GameCenterIDLineEdit.TextChanged += OnGameCenterIdChanged;
        GameCenterIDLineEdit.FocusEntered += OnGameCenterIdFocusEntered;
        GameCenterIDLineEdit.FocusExited += OnGameCenterIdFocusExited;
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
            if (texture != null && oldIcon != null)
            {
                _undoRedoManager.AddDoMethod(this, nameof(SetAchievementIcon), achievement, texture);
                _undoRedoManager.AddUndoMethod(this, nameof(SetAchievementIcon), achievement, oldIcon);
                _undoRedoManager.CommitAction(false);
            }            
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
