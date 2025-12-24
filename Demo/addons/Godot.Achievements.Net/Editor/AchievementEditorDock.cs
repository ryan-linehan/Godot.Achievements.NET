#if TOOLS
using System;
using System.Linq;
using System.Text.Json;

namespace Godot.Achievements.Core.Editor;

/// <summary>
/// Editor dock for managing achievements in the Godot editor
/// </summary>
[Tool]
public partial class AchievementEditorDock : Control
{
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
    [Export]
    private ScrollContainer ItemListScrollContainer = null!;

    // Details Panel Component
    [Export]
    private AchievementsEditorDetailsPanel DetailsPanel = null!;
    [Export]
    private ScrollContainer NoItemSelectedScroll = null!;

    // Bottom Bar Controls
    [Export]
    private Label DatabasePathLabel = null!;
    [Export]
    private MenuButton ImportMenuButton = null!;
    [Export]
    private MenuButton ExportMenuButton = null!;

    // Private Fields
    private AchievementDatabase? _currentDatabase;
    private string _currentDatabasePath = string.Empty;
    private Achievement? _selectedAchievement;
    private int _selectedIndex = -1;
    private EditorFileDialog? _importCSVFileDialog;
    private EditorFileDialog? _exportCSVFileDialog;
    private EditorFileDialog? _importJSONFileDialog;
    private EditorFileDialog? _exportJSONFileDialog;
    private ConfirmationDialog? _removeConfirmDialog;
    private AcceptDialog? _unsavedChangesDialog;
    private PopupMenu? _contextMenu;
    private int _contextMenuTargetIndex = -1;
    private bool _hasUnsavedChanges = false;
    private Action? _pendingAction;

    // Validation
    private System.Collections.Generic.Dictionary<Achievement, AchievementValidationResult> _validationResults = new();
    private System.Collections.Generic.List<string> _duplicateInternalIds = new();
    private Texture2D? _warningIcon;
    private const string WARNING_PREFIX = "\u26a0 "; // ⚠ Unicode warning sign
    private const string ERROR_PREFIX = "\u274c "; // ❌ Unicode cross mark

    private const string DATABASE_PATH_SETTING = "addons/achievements/database_path";
    private const string DEFAULT_DATABASE_PATH = "res://addons/Godot.Achievements.Net/_achievements/_achievements.tres";

    // Platform settings keys
    private const string STEAM_ENABLED_SETTING = "addons/achievements/platforms/steam_enabled";
    private const string GAMECENTER_ENABLED_SETTING = "addons/achievements/platforms/gamecenter_enabled";
    private const string GOOGLEPLAY_ENABLED_SETTING = "addons/achievements/platforms/googleplay_enabled";

    // Track last known database path for change detection
    private string _lastKnownDatabasePath = string.Empty;

    public override void _Ready()
    {
        // Create confirmation dialog for removing achievements
        _removeConfirmDialog = new ConfirmationDialog();
        _removeConfirmDialog.Confirmed += ConfirmRemoveAchievement;
        AddChild(_removeConfirmDialog);

        // Create unsaved changes dialog
        _unsavedChangesDialog = new AcceptDialog();
        _unsavedChangesDialog.DialogText = "The database has unsaved changes.\nWhat would you like to do?";
        _unsavedChangesDialog.Title = "Unsaved Changes";
        _unsavedChangesDialog.GetOkButton().Text = "Save and Continue";
        _unsavedChangesDialog.Confirmed += OnUnsavedChangesSaveAndContinue;

        _unsavedChangesDialog.AddButton("Don't Save", true, "dont_save");
        _unsavedChangesDialog.CustomAction += OnUnsavedChangesCustomAction;

        _unsavedChangesDialog.AddCancelButton("Cancel");
        AddChild(_unsavedChangesDialog);

        // Create context menu for list items
        _contextMenu = new PopupMenu();
        _contextMenu.AddItem("Move Up", 0);
        _contextMenu.AddItem("Move Down", 1);
        _contextMenu.AddSeparator();
        _contextMenu.AddItem("Show in File", 2);
        _contextMenu.IdPressed += OnContextMenuItemPressed;
        AddChild(_contextMenu);

        // Create import CSV file dialog
        _importCSVFileDialog = new EditorFileDialog();
        _importCSVFileDialog.FileMode = EditorFileDialog.FileModeEnum.OpenFile;
        _importCSVFileDialog.AddFilter("*.csv", "CSV Files");
        _importCSVFileDialog.Access = EditorFileDialog.AccessEnum.Filesystem;
        _importCSVFileDialog.Title = "Import Achievements from CSV";
        _importCSVFileDialog.FileSelected += OnImportCSVFileSelected;
        AddChild(_importCSVFileDialog);

        // Create export CSV file dialog
        _exportCSVFileDialog = new EditorFileDialog();
        _exportCSVFileDialog.FileMode = EditorFileDialog.FileModeEnum.SaveFile;
        _exportCSVFileDialog.AddFilter("*.csv", "CSV Files");
        _exportCSVFileDialog.Access = EditorFileDialog.AccessEnum.Filesystem;
        _exportCSVFileDialog.Title = "Export Achievements to CSV";
        _exportCSVFileDialog.FileSelected += OnExportCSVFileSelected;
        AddChild(_exportCSVFileDialog);

        // Create import JSON file dialog
        _importJSONFileDialog = new EditorFileDialog();
        _importJSONFileDialog.FileMode = EditorFileDialog.FileModeEnum.OpenFile;
        _importJSONFileDialog.AddFilter("*.json", "JSON Files");
        _importJSONFileDialog.Access = EditorFileDialog.AccessEnum.Filesystem;
        _importJSONFileDialog.Title = "Import Achievements from JSON";
        _importJSONFileDialog.FileSelected += OnImportJSONFileSelected;
        AddChild(_importJSONFileDialog);

        // Create export JSON file dialog
        _exportJSONFileDialog = new EditorFileDialog();
        _exportJSONFileDialog.FileMode = EditorFileDialog.FileModeEnum.SaveFile;
        _exportJSONFileDialog.AddFilter("*.json", "JSON Files");
        _exportJSONFileDialog.Access = EditorFileDialog.AccessEnum.Filesystem;
        _exportJSONFileDialog.Title = "Export Achievements to JSON";
        _exportJSONFileDialog.FileSelected += OnExportJSONFileSelected;
        AddChild(_exportJSONFileDialog);

        // Setup import menu button
        var importPopup = ImportMenuButton.GetPopup();
        importPopup.AddItem("CSV", 0);
        importPopup.AddItem("JSON", 1);
        importPopup.IdPressed += OnImportMenuItemPressed;

        // Setup export menu button
        var exportPopup = ExportMenuButton.GetPopup();
        exportPopup.AddItem("CSV", 0);
        exportPopup.AddItem("JSON", 1);
        exportPopup.IdPressed += OnExportMenuItemPressed;

        // Connect UI signals
        AddAchievementButton.Pressed += OnAddAchievementPressed;
        RemoveButton.Pressed += OnRemovePressed;
        DuplicateButton.Pressed += OnDuplicatePressed;
        SearchLineEdit.TextChanged += OnSearchTextChanged;
        ItemList.ItemSelected += OnItemSelected;
        ItemList.ItemClicked += OnItemListClicked;

        // Connect details panel signals
        if (DetailsPanel != null)
        {
            DetailsPanel.AchievementIdChanged += OnAchievementIdChanged;
            DetailsPanel.AchievementDisplayNameChanged += OnAchievementDisplayNameChanged;
            DetailsPanel.AchievementChanged += OnAchievementChanged;
        }

        // Connect visibility change
        VisibilityChanged += OnVisibilityChanged;

        // Initialize button states
        UpdateButtonStates();

        // Set platform section visibility based on project settings
        UpdatePlatformVisibility();

        // Listen for project settings changes to update platform visibility
        ProjectSettings.Singleton.SettingsChanged += OnProjectSettingsChanged;

        // Load warning icon from editor theme
        _warningIcon = EditorInterface.Singleton.GetEditorTheme().GetIcon("StatusWarning", "EditorIcons");

        // Load database from settings
        var savedPath = LoadDatabasePath();
        _lastKnownDatabasePath = savedPath;
        LoadDatabase(savedPath);

        // Enable shortcut input processing for Ctrl+S
        SetProcessShortcutInput(true);
    }

    public override void _ShortcutInput(InputEvent @event)
    {
        // Only process when dock is visible
        if (!Visible) return;

        if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
        {
            // Ctrl+S or Cmd+S to save (don't consume the event - let editor also save)
            if (keyEvent.Keycode == Key.S && (keyEvent.CtrlPressed || keyEvent.MetaPressed) && _hasUnsavedChanges)
            {
                SaveDatabase();
            }
        }
    }

    private void OnProjectSettingsChanged()
    {
        UpdatePlatformVisibility();
        CheckDatabasePathChanged();
        // Re-run validation since platform enablement affects warnings
        RefreshAchievementList(preserveSelection: true);
    }

    private void CheckDatabasePathChanged()
    {
        var currentPath = LoadDatabasePath();
        if (currentPath != _lastKnownDatabasePath)
        {
            _lastKnownDatabasePath = currentPath;
            LoadDatabase(currentPath);
        }
    }

    private void UpdatePlatformVisibility()
    {
        if (DetailsPanel != null)
        {
            if (DetailsPanel.SteamVBox != null)
                DetailsPanel.SteamVBox.Visible = GetPlatformEnabled(STEAM_ENABLED_SETTING);
            if (DetailsPanel.GameCenterVBox != null)
                DetailsPanel.GameCenterVBox.Visible = GetPlatformEnabled(GAMECENTER_ENABLED_SETTING);
            if (DetailsPanel.GooglePlayVBox != null)
                DetailsPanel.GooglePlayVBox.Visible = GetPlatformEnabled(GOOGLEPLAY_ENABLED_SETTING);
        }
    }

    public override void _ExitTree()
    {
        // Disconnect signals
        ProjectSettings.Singleton.SettingsChanged -= OnProjectSettingsChanged;
        VisibilityChanged -= OnVisibilityChanged;

        if (DetailsPanel != null)
        {
            DetailsPanel.AchievementIdChanged -= OnAchievementIdChanged;
            DetailsPanel.AchievementDisplayNameChanged -= OnAchievementDisplayNameChanged;
            DetailsPanel.AchievementChanged -= OnAchievementChanged;
        }

        if (_importCSVFileDialog != null)
        {
            _importCSVFileDialog.FileSelected -= OnImportCSVFileSelected;
            _importCSVFileDialog.QueueFree();
        }

        if (_exportCSVFileDialog != null)
        {
            _exportCSVFileDialog.FileSelected -= OnExportCSVFileSelected;
            _exportCSVFileDialog.QueueFree();
        }

        if (_importJSONFileDialog != null)
        {
            _importJSONFileDialog.FileSelected -= OnImportJSONFileSelected;
            _importJSONFileDialog.QueueFree();
        }

        if (_exportJSONFileDialog != null)
        {
            _exportJSONFileDialog.FileSelected -= OnExportJSONFileSelected;
            _exportJSONFileDialog.QueueFree();
        }

        // Disconnect menu button popup signals
        if (ImportMenuButton != null)
        {
            var importPopup = ImportMenuButton.GetPopup();
            if (importPopup != null)
                importPopup.IdPressed -= OnImportMenuItemPressed;
        }

        if (ExportMenuButton != null)
        {
            var exportPopup = ExportMenuButton.GetPopup();
            if (exportPopup != null)
                exportPopup.IdPressed -= OnExportMenuItemPressed;
        }

        if (_removeConfirmDialog != null)
        {
            _removeConfirmDialog.Confirmed -= ConfirmRemoveAchievement;
            _removeConfirmDialog.QueueFree();
        }

        if (_unsavedChangesDialog != null)
        {
            _unsavedChangesDialog.Confirmed -= OnUnsavedChangesSaveAndContinue;
            _unsavedChangesDialog.CustomAction -= OnUnsavedChangesCustomAction;
            _unsavedChangesDialog.QueueFree();
        }

        if (_contextMenu != null)
        {
            _contextMenu.IdPressed -= OnContextMenuItemPressed;
            _contextMenu.QueueFree();
        }

        if (AddAchievementButton != null)
            AddAchievementButton.Pressed -= OnAddAchievementPressed;
        if (RemoveButton != null)
            RemoveButton.Pressed -= OnRemovePressed;
        if (DuplicateButton != null)
            DuplicateButton.Pressed -= OnDuplicatePressed;
        if (SearchLineEdit != null)
            SearchLineEdit.TextChanged -= OnSearchTextChanged;
        if (ItemList != null)
        {
            ItemList.ItemSelected -= OnItemSelected;
            ItemList.ItemClicked -= OnItemListClicked;
        }
    }

    private void OnVisibilityChanged()
    {
        if (Visible)
        {
            RefreshAchievementList(preserveSelection: true);
        }
    }

    private void UpdateButtonStates()
    {
        var hasSelection = _selectedAchievement != null;
        RemoveButton.Disabled = !hasSelection;
        DuplicateButton.Disabled = !hasSelection;
    }

    private void OnAchievementIdChanged(Achievement achievement, string oldId, string newId)
    {
        // Just refresh the list to show the updated ID
        // No file renaming needed since achievements are stored inline in the database
        GD.Print($"[Achievements:Editor] Achievement ID changed: {oldId} -> {newId}");
    }

    private void OnAchievementDisplayNameChanged(Achievement achievement)
    {
        UpdateListItemForAchievement(achievement);
    }

    private void OnAchievementChanged()
    {
        MarkDirty();

        // Refresh the full list to re-run all validations (including duplicate detection)
        RefreshAchievementList(preserveSelection: true);
    }

    private void MarkDirty()
    {
        if (_hasUnsavedChanges) return;

        _hasUnsavedChanges = true;
        UpdateDatabasePathLabel();
    }

    private void ClearDirty()
    {
        if (!_hasUnsavedChanges) return;

        _hasUnsavedChanges = false;
        UpdateDatabasePathLabel();
    }

    private void UpdateDatabasePathLabel()
    {
        if (_currentDatabase == null)
        {
            DatabasePathLabel.Text = "No database loaded";
        }
        else
        {
            var prefix = _hasUnsavedChanges ? "* " : "";
            var displayPath = ResolveUidToPath(_currentDatabasePath);
            DatabasePathLabel.Text = $"{prefix}{displayPath}";
        }
    }

    private void ShowUnsavedChangesDialog()
    {
        if (_unsavedChangesDialog != null)
        {
            _unsavedChangesDialog.PopupCentered();
        }
    }

    private void OnUnsavedChangesSaveAndContinue()
    {
        // Save and then execute the pending action
        SaveDatabase();
        ExecutePendingAction();
    }

    private void OnUnsavedChangesCustomAction(StringName action)
    {
        if (action == "dont_save")
        {
            // Clear dirty flag and execute pending action without saving
            ClearDirty();
            ExecutePendingAction();
        }
    }

    private void ExecutePendingAction()
    {
        if (_pendingAction != null)
        {
            var action = _pendingAction;
            _pendingAction = null;
            action.Invoke();
        }
    }

    private void UpdateListItemForAchievement(Achievement achievement)
    {
        if (achievement == null) return;

        // Re-validate this specific achievement
        var validationResult = AchievementValidator.ValidateAchievement(achievement);
        _validationResults[achievement] = validationResult;

        // Find the item in the list
        for (int i = 0; i < ItemList.ItemCount; i++)
        {
            var itemAchievement = ItemList.GetItemMetadata(i).As<Achievement>();
            if (itemAchievement == achievement)
            {
                var hasWarnings = validationResult.HasWarnings;
                var displayText = hasWarnings ? WARNING_PREFIX + achievement.DisplayName : achievement.DisplayName;

                ItemList.SetItemText(i, displayText);
                ItemList.SetItemIcon(i, achievement.Icon);

                // Update tooltip with warnings or clear it
                ItemList.SetItemTooltip(i, hasWarnings ? validationResult.GetTooltipText() : string.Empty);
                break;
            }
        }
    }

    #region Database Operations

    private void LoadDatabase(string path)
    {
        if (!ResourceLoader.Exists(path))
        {
            GD.PushWarning($"[Achievements:Editor] Database not found at {path}");
            _currentDatabase = null;
            _currentDatabasePath = string.Empty;
            DatabasePathLabel.Text = "No database loaded";
            RefreshAchievementList();
            return;
        }

        AchievementDatabase? resource = null;
        try
        {
            resource = ResourceLoader.Load<AchievementDatabase>(path);
        }
        catch (System.InvalidCastException ex)
        {
            GD.PushError($"[Achievements:Editor] Resource at {path} is not an AchievementDatabase: {ex.Message}");
            var dialog = new AcceptDialog();
            dialog.DialogText = $"The file at:\n{path}\n\nis not a valid AchievementDatabase resource.\n\nPlease select a valid AchievementDatabase (.tres) file.";
            AddChild(dialog);
            dialog.PopupCentered();
            return;
        }

        if (resource == null)
        {
            GD.PushError($"[Achievements:Editor] Failed to load database from {path}");
            var dialog = new AcceptDialog();
            dialog.DialogText = $"Failed to load database from:\n{path}\n\nPlease select a valid AchievementDatabase resource.";
            AddChild(dialog);
            dialog.PopupCentered();
            return;
        }

        _currentDatabase = resource;
        _currentDatabasePath = path;
        SaveDatabasePath(path);
        ClearDirty();
        UpdateDatabasePathLabel();
        RefreshAchievementList();

        GD.Print($"[Achievements:Editor] Loaded database from {path}");
    }

    private void SaveDatabase()
    {
        if (_currentDatabase == null)
        {
            GD.PushWarning("[Achievements:Editor] No database loaded to save");
            return;
        }

        var validationErrors = _currentDatabase.Validate();
        if (validationErrors.Length > 0)
        {
            GD.PushWarning("[Achievements:Editor] Database validation warnings:");
            foreach (var validationError in validationErrors)
            {
                GD.PushWarning($"  - {validationError}");
            }
        }

        var savePath = ResolveUidToPath(_currentDatabasePath);
        var saveError = ResourceSaver.Save(_currentDatabase, savePath);
        if (saveError != Error.Ok)
        {
            GD.PushError($"[Achievements:Editor] Failed to save database: {saveError}");
            return;
        }

        ClearDirty();
        GD.Print($"[Achievements:Editor] Database saved to {savePath}");

        // Refresh the Inspector if it's showing this resource
        _currentDatabase.EmitChanged();
        _currentDatabase.NotifyPropertyListChanged();
    }

    private string LoadDatabasePath()
    {
        var hasSetting = ProjectSettings.HasSetting(DATABASE_PATH_SETTING);

        if (hasSetting)
        {
            var path = ProjectSettings.GetSetting(DATABASE_PATH_SETTING).AsString();
            return string.IsNullOrEmpty(path) ? DEFAULT_DATABASE_PATH : path;
        }
        return DEFAULT_DATABASE_PATH;
    }

    private static string ResolveUidToPath(string path)
    {
        if (path.StartsWith("uid://"))
        {
            var uid = ResourceUid.TextToId(path);
            if (ResourceUid.HasId(uid))
            {
                return ResourceUid.GetIdPath(uid);
            }
        }
        return path;
    }

    private void SaveDatabasePath(string path)
    {
        ProjectSettings.SetSetting(DATABASE_PATH_SETTING, path);
        var saveError = ProjectSettings.Save();
        if (saveError != Error.Ok)
        {
            GD.PushWarning($"[Achievements:Editor] Failed to save project settings: {saveError}");
        }
    }

    private static bool GetPlatformEnabled(string settingKey)
    {
        if (ProjectSettings.HasSetting(settingKey))
        {
            return ProjectSettings.GetSetting(settingKey).AsBool();
        }
        return false;
    }

    #endregion

    #region List Management

    private void RefreshAchievementList(bool preserveSelection = false)
    {
        var previousSelection = preserveSelection ? _selectedAchievement : null;

        ItemList.Clear();
        _selectedAchievement = null;
        _selectedIndex = -1;

        if (_currentDatabase == null || _currentDatabase.Achievements.Count == 0)
        {
            _validationResults.Clear();
            NoItemsControl.Visible = true;
            ItemList.Visible = false;
            ItemListScrollContainer.Visible = false;
            DetailsPanel.Visible = false;
            NoItemSelectedScroll.Visible = true;
            UpdateButtonStates();
            return;
        }

        // Run validation on all achievements
        _validationResults = AchievementValidator.ValidateDatabase(_currentDatabase);
        _duplicateInternalIds = AchievementValidator.GetDuplicateInternalIds(_currentDatabase);

        NoItemsControl.Visible = false;
        ItemList.Visible = true;
        ItemListScrollContainer.Visible = true;

        var searchText = SearchLineEdit.Text?.ToLower() ?? string.Empty;
        var filteredAchievements = _currentDatabase.Achievements
            .Where(a => string.IsNullOrEmpty(searchText)
                || (a.DisplayName?.ToLower().Contains(searchText) ?? false)
                || (a.Id?.ToLower().Contains(searchText) ?? false))
            .ToList();

        if (filteredAchievements.Count == 0)
        {
            NoItemsControl.Visible = true;
            ItemList.Visible = false;
            ItemListScrollContainer.Visible = false;
            DetailsPanel.Visible = false;
            NoItemSelectedScroll.Visible = true;
            UpdateButtonStates();
            return;
        }

        for (int i = 0; i < filteredAchievements.Count; i++)
        {
            var achievement = filteredAchievements[i];

            // Check for errors (missing/duplicate internal ID) vs warnings
            var hasError = string.IsNullOrWhiteSpace(achievement.Id)
                || _duplicateInternalIds.Contains(achievement.Id);
            var hasWarnings = _validationResults.TryGetValue(achievement, out var validationResult) && validationResult.HasWarnings;

            string displayText;
            if (hasError)
                displayText = ERROR_PREFIX + achievement.DisplayName;
            else if (hasWarnings)
                displayText = WARNING_PREFIX + achievement.DisplayName;
            else
                displayText = achievement.DisplayName;

            var index = ItemList.AddItem(displayText, achievement.Icon);
            ItemList.SetItemMetadata(index, achievement);

            // Set tooltip with warning/error details
            if (hasError)
            {
                var errorText = string.IsNullOrWhiteSpace(achievement.Id)
                    ? "Internal ID is required"
                    : $"Duplicate internal ID: {achievement.Id}";
                ItemList.SetItemTooltip(index, errorText);
            }
            else if (hasWarnings && validationResult != null)
            {
                ItemList.SetItemTooltip(index, validationResult.GetTooltipText());
            }

            // Restore selection if this is the previously selected achievement
            if (achievement == previousSelection)
            {
                ItemList.Select(index);
                _selectedAchievement = achievement;
                _selectedIndex = index;
            }
        }

        // Update UI based on selection
        if (_selectedAchievement != null)
        {
            DetailsPanel.Visible = true;
            NoItemSelectedScroll.Visible = false;
            DetailsPanel.CurrentAchievement = _selectedAchievement;
            UpdateDetailsPanelValidation();
        }
        else
        {
            DetailsPanel.Visible = false;
            NoItemSelectedScroll.Visible = true;
            DetailsPanel.ClearValidation();
        }

        UpdateButtonStates();
    }

    private void UpdateDetailsPanelValidation()
    {
        if (_selectedAchievement == null || _currentDatabase == null)
        {
            DetailsPanel.ClearValidation();
            return;
        }

        _validationResults.TryGetValue(_selectedAchievement, out var validationResult);
        DetailsPanel.UpdateValidation(validationResult, _duplicateInternalIds);
    }

    private void OnSearchTextChanged(string newText)
    {
        RefreshAchievementList(preserveSelection: true);
    }

    private void OnItemSelected(long index)
    {
        var targetAchievement = ItemList.GetItemMetadata((int)index).As<Achievement>();
        SelectAchievement((int)index, targetAchievement);
    }

    private void SelectAchievement(int index, Achievement achievement)
    {
        _selectedIndex = index;
        _selectedAchievement = achievement;

        DetailsPanel.Visible = true;
        NoItemSelectedScroll.Visible = false;
        DetailsPanel.CurrentAchievement = _selectedAchievement;
        UpdateDetailsPanelValidation();

        UpdateButtonStates();
    }

    #endregion

    #region CRUD Operations

    private void OnAddAchievementPressed()
    {
        // Check if there are unsaved changes before adding new achievement
        if (_hasUnsavedChanges)
        {
            _pendingAction = () => CreateNewAchievement();
            ShowUnsavedChangesDialog();
            return;
        }

        CreateNewAchievement();
    }

    private void CreateNewAchievement()
    {
        if (_currentDatabase == null)
        {
            GD.PushWarning("[Achievements:Editor] Cannot add achievement - no database loaded");
            return;
        }

        var uniqueId = GenerateUniqueId();
        var newAchievement = new Achievement
        {
            Id = uniqueId,
            DisplayName = "New Achievement",
            Description = "Achievement description",
            Icon = null,
            SteamId = string.Empty,
            GameCenterId = string.Empty,
            GooglePlayId = string.Empty,
            IsIncremental = false,
            MaxProgress = 1,
            CustomPlatformIds = new Godot.Collections.Dictionary<string, string>(),
            ExtraProperties = new Godot.Collections.Dictionary<string, Variant>()
        };

        _currentDatabase.AddAchievement(newAchievement);
        MarkDirty();

        GD.Print($"[Achievements:Editor] Created new achievement: {uniqueId}");

        // Auto-save after add
        SaveDatabase();

        // Refresh and auto-select new achievement
        RefreshAchievementList();

        // Find and select the new achievement in the list
        for (int i = 0; i < ItemList.ItemCount; i++)
        {
            var achievement = ItemList.GetItemMetadata(i).As<Achievement>();
            if (achievement.Id == uniqueId)
            {
                ItemList.Select(i);
                SelectAchievement(i, achievement);
                break;
            }
        }
    }

    private void OnRemovePressed()
    {
        if (_selectedAchievement == null)
        {
            GD.PushWarning("[Achievements:Editor] No achievement selected to remove");
            return;
        }

        if (_removeConfirmDialog != null)
        {
            _removeConfirmDialog.DialogText = $"Are you sure you want to remove the achievement:\n\n'{_selectedAchievement.DisplayName}' ({_selectedAchievement.Id}).";
            _removeConfirmDialog.PopupCentered();
        }
    }

    private void ConfirmRemoveAchievement()
    {
        if (_selectedAchievement == null || _currentDatabase == null)
            return;

        var achievementToRemove = _selectedAchievement;

        // Remove from database
        _currentDatabase.RemoveAchievement(achievementToRemove.Id);
        MarkDirty();

        GD.Print($"[Achievements:Editor] Removed achievement: {achievementToRemove.Id}");

        // Auto-save after remove
        SaveDatabase();

        // Clear selection
        _selectedAchievement = null;
        _selectedIndex = -1;

        // Refresh list
        RefreshAchievementList();
    }

    private void OnDuplicatePressed()
    {
        if (_selectedAchievement == null || _currentDatabase == null)
        {
            GD.PushWarning("[Achievements:Editor] No achievement selected to duplicate");
            return;
        }

        var duplicate = DuplicateAchievement(_selectedAchievement);

        // Ensure unique ID
        var baseId = duplicate.Id;
        var counter = 1;
        while (_currentDatabase.GetById(duplicate.Id) != null)
        {
            duplicate.Id = counter == 1 ? baseId : $"{baseId.Replace("_copy", "")}_{counter}_copy";
            counter++;
        }

        _currentDatabase.AddAchievement(duplicate);
        MarkDirty();

        GD.Print($"[Achievements:Editor] Duplicated achievement: {_selectedAchievement.Id} -> {duplicate.Id}");

        // Auto-save after duplicate
        SaveDatabase();

        // Refresh and auto-select duplicate
        RefreshAchievementList();

        // Find and select the duplicated achievement in the list
        for (int i = 0; i < ItemList.ItemCount; i++)
        {
            var achievement = ItemList.GetItemMetadata(i).As<Achievement>();
            if (achievement.Id == duplicate.Id)
            {
                ItemList.Select(i);
                OnItemSelected(i);
                break;
            }
        }
    }

    private Achievement DuplicateAchievement(Achievement original)
    {
        var duplicate = new Achievement
        {
            Id = $"{original.Id}_copy",
            DisplayName = $"{original.DisplayName} (Copy)",
            Description = original.Description,
            Icon = original.Icon,
            SteamId = original.SteamId,
            GameCenterId = original.GameCenterId,
            GooglePlayId = original.GooglePlayId,
            IsIncremental = original.IsIncremental,
            MaxProgress = original.MaxProgress,
            CustomPlatformIds = new Godot.Collections.Dictionary<string, string>(original.CustomPlatformIds),
            ExtraProperties = new Godot.Collections.Dictionary<string, Variant>(original.ExtraProperties)
        };
        return duplicate;
    }

    private string GenerateUniqueId()
    {
        if (_currentDatabase == null)
            return "achievement_01";

        var counter = 1;
        string id;
        do
        {
            id = $"achievement_{counter:D2}";
            counter++;
            if (counter > 9999) // Safety check
            {
                id = $"achievement_{Guid.NewGuid():N}";
                break;
            }
        } while (_currentDatabase.GetById(id) != null);
        return id;
    }

    #endregion

    #region Context Menu Operations

    private void OnItemListClicked(long index, Vector2 atPosition, long mouseButtonIndex)
    {
        // Right-click to show context menu
        if (mouseButtonIndex == (long)MouseButton.Right)
        {
            _contextMenuTargetIndex = (int)index;

            if (_contextMenu != null && _currentDatabase != null && _contextMenuTargetIndex >= 0)
            {
                var achievement = ItemList.GetItemMetadata(_contextMenuTargetIndex).As<Achievement>();
                var databaseIndex = _currentDatabase.Achievements.IndexOf(achievement);

                // Check if filtering is active
                var isFiltered = !string.IsNullOrEmpty(SearchLineEdit.Text);

                // Note: Item indices are 0=Move Up, 1=Move Down
                if (isFiltered)
                {
                    // Disable move operations when filtered
                    _contextMenu.SetItemDisabled(0, true); // Move Up
                    _contextMenu.SetItemDisabled(1, true); // Move Down
                }
                else
                {
                    // Enable/disable based on actual position in database
                    var totalCount = _currentDatabase.Achievements.Count;
                    _contextMenu.SetItemDisabled(0, databaseIndex <= 0); // Move Up
                    _contextMenu.SetItemDisabled(1, databaseIndex >= totalCount - 1); // Move Down
                }

                _contextMenu.Position = (Vector2I)(GetGlobalMousePosition());
                _contextMenu.Popup();
            }
        }
    }

    private void OnContextMenuItemPressed(long id)
    {
        if (_contextMenuTargetIndex < 0 || _contextMenuTargetIndex >= ItemList.ItemCount)
            return;

        var achievement = ItemList.GetItemMetadata(_contextMenuTargetIndex).As<Achievement>();
        if (achievement == null)
            return;

        switch (id)
        {
            case 0: // Move Up
                MoveAchievementUp(achievement);
                break;
            case 1: // Move Down
                MoveAchievementDown(achievement);
                break;
            case 2: // Show in File
                ShowInFile(achievement);
                break;
        }
    }

    private void ShowInFile(Achievement achievement)
    {
        var editorInterface = EditorInterface.Singleton;
        if (editorInterface == null)
        {
            GD.PushError("[Achievements:Editor] EditorInterface not available");
            return;
        }

        var databasePath = ResolveUidToPath(_currentDatabasePath);

        // Check if the achievement has its own resource path (saved as external file)
        // Sub-resources have paths like "res://file.tres::Resource_id" - these are NOT external
        var achievementPath = achievement.ResourcePath;
        var isSubResource = !string.IsNullOrEmpty(achievementPath) && achievementPath.Contains("::");
        var isExternalResource = !string.IsNullOrEmpty(achievementPath)
            && !isSubResource
            && ResourceLoader.Exists(achievementPath)
            && achievementPath != databasePath;

        if (isExternalResource)
        {
            // Achievement is saved as its own file - navigate to it
            editorInterface.SelectFile(achievementPath);
            GD.Print($"[Achievements:Editor] Showing achievement file: {achievementPath}");
        }
        else
        {
            // Achievement is internal to the database - show the database file
            if (!string.IsNullOrEmpty(databasePath) && ResourceLoader.Exists(databasePath))
            {
                editorInterface.SelectFile(databasePath);
                GD.Print($"[Achievements:Editor] Achievement is internal - showing database file: {databasePath}");
            }
            else
            {
                GD.PushWarning("[Achievements:Editor] No database path available to show");
            }
        }
    }

    private void MoveAchievementUp(Achievement achievement)
    {
        if (_currentDatabase == null)
            return;

        var currentIndex = _currentDatabase.Achievements.IndexOf(achievement);
        if (currentIndex <= 0)
            return;

        _currentDatabase.Achievements.RemoveAt(currentIndex);
        _currentDatabase.Achievements.Insert(currentIndex - 1, achievement);
        MarkDirty();

        SaveDatabase();
        RefreshAchievementList(preserveSelection: true);
        GD.Print($"[Achievements:Editor] Moved achievement up: {achievement.DisplayName}");
    }

    private void MoveAchievementDown(Achievement achievement)
    {
        if (_currentDatabase == null)
            return;

        var currentIndex = _currentDatabase.Achievements.IndexOf(achievement);
        if (currentIndex < 0 || currentIndex >= _currentDatabase.Achievements.Count - 1)
            return;

        _currentDatabase.Achievements.RemoveAt(currentIndex);
        _currentDatabase.Achievements.Insert(currentIndex + 1, achievement);
        MarkDirty();

        SaveDatabase();
        RefreshAchievementList(preserveSelection: true);
        GD.Print($"[Achievements:Editor] Moved achievement down: {achievement.DisplayName}");
    }

    #endregion

    #region Import/Export

    private void OnImportMenuItemPressed(long id)
    {
        if (_currentDatabase == null)
        {
            var dialog = new AcceptDialog();
            dialog.DialogText = "Please load a database first before importing achievements.";
            AddChild(dialog);
            dialog.PopupCentered();
            return;
        }

        switch (id)
        {
            case 0: // CSV
                if (_importCSVFileDialog != null)
                {
                    _importCSVFileDialog.CurrentPath = "achievements.csv";
                    _importCSVFileDialog.PopupCentered(new Vector2I(800, 600));
                }
                break;
            case 1: // JSON
                if (_importJSONFileDialog != null)
                {
                    _importJSONFileDialog.CurrentPath = "achievements.json";
                    _importJSONFileDialog.PopupCentered(new Vector2I(800, 600));
                }
                break;
        }
    }

    private void OnExportMenuItemPressed(long id)
    {
        if (_currentDatabase == null || _currentDatabase.Achievements.Count == 0)
        {
            var dialog = new AcceptDialog();
            dialog.DialogText = "No achievements to export. Please load a database with achievements first.";
            AddChild(dialog);
            dialog.PopupCentered();
            return;
        }

        switch (id)
        {
            case 0: // CSV
                if (_exportCSVFileDialog != null)
                {
                    _exportCSVFileDialog.CurrentPath = "achievements.csv";
                    _exportCSVFileDialog.PopupCentered(new Vector2I(800, 600));
                }
                break;
            case 1: // JSON
                if (_exportJSONFileDialog != null)
                {
                    _exportJSONFileDialog.CurrentPath = "achievements.json";
                    _exportJSONFileDialog.PopupCentered(new Vector2I(800, 600));
                }
                break;
        }
    }

    private void OnImportCSVFileSelected(string path)
    {
        if (_currentDatabase == null)
            return;

        try
        {
            var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
            if (file == null)
            {
                ShowErrorDialog($"Failed to open file: {FileAccess.GetOpenError()}");
                return;
            }

            // Use Godot's built-in CSV parsing
            var header = file.GetCsvLine();
            if (header.Length == 0)
            {
                file.Close();
                ShowErrorDialog("CSV file is empty.");
                return;
            }

            // Build column index map from header
            var columnMap = new System.Collections.Generic.Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < header.Length; i++)
            {
                columnMap[header[i].Trim()] = i;
            }

            // Validate required columns
            if (!columnMap.ContainsKey("Id"))
            {
                file.Close();
                ShowErrorDialog("CSV must contain an 'Id' column.");
                return;
            }

            int importedCount = 0;
            int updatedCount = 0;
            int skippedCount = 0;

            // Process data rows using Godot's GetCsvLine()
            while (!file.EofReached())
            {
                var values = file.GetCsvLine();
                if (values.Length == 0 || (values.Length == 1 && string.IsNullOrWhiteSpace(values[0])))
                    continue;

                var id = GetCSVValue(values, columnMap, "Id");
                if (string.IsNullOrWhiteSpace(id))
                    continue;

                // Check if achievement already exists
                var existing = _currentDatabase.GetById(id);
                bool isNew = existing == null;

                var achievement = existing ?? new Achievement { Id = id };
                bool hasChanges = false;

                // Update fields from CSV, tracking if any changes occur
                if (columnMap.ContainsKey("DisplayName"))
                {
                    var newValue = GetCSVValue(values, columnMap, "DisplayName") ?? achievement.DisplayName;
                    if (achievement.DisplayName != newValue)
                    {
                        achievement.DisplayName = newValue;
                        hasChanges = true;
                    }
                }
                if (columnMap.ContainsKey("Description"))
                {
                    var newValue = GetCSVValue(values, columnMap, "Description") ?? achievement.Description;
                    if (achievement.Description != newValue)
                    {
                        achievement.Description = newValue;
                        hasChanges = true;
                    }
                }
                if (columnMap.ContainsKey("SteamId"))
                {
                    var newValue = GetCSVValue(values, columnMap, "SteamId") ?? achievement.SteamId;
                    if (achievement.SteamId != newValue)
                    {
                        achievement.SteamId = newValue;
                        hasChanges = true;
                    }
                }
                if (columnMap.ContainsKey("GameCenterId"))
                {
                    var newValue = GetCSVValue(values, columnMap, "GameCenterId") ?? achievement.GameCenterId;
                    if (achievement.GameCenterId != newValue)
                    {
                        achievement.GameCenterId = newValue;
                        hasChanges = true;
                    }
                }
                if (columnMap.ContainsKey("GooglePlayId"))
                {
                    var newValue = GetCSVValue(values, columnMap, "GooglePlayId") ?? achievement.GooglePlayId;
                    if (achievement.GooglePlayId != newValue)
                    {
                        achievement.GooglePlayId = newValue;
                        hasChanges = true;
                    }
                }
                if (columnMap.ContainsKey("IsIncremental"))
                {
                    var newValue = GetCSVValue(values, columnMap, "IsIncremental")?.ToLower() == "true";
                    if (achievement.IsIncremental != newValue)
                    {
                        achievement.IsIncremental = newValue;
                        hasChanges = true;
                    }
                }
                if (columnMap.ContainsKey("MaxProgress"))
                {
                    if (int.TryParse(GetCSVValue(values, columnMap, "MaxProgress"), out int maxProgress))
                    {
                        if (achievement.MaxProgress != maxProgress)
                        {
                            achievement.MaxProgress = maxProgress;
                            hasChanges = true;
                        }
                    }
                }

                if (isNew)
                {
                    achievement.CustomPlatformIds = new Godot.Collections.Dictionary<string, string>();
                    achievement.ExtraProperties = new Godot.Collections.Dictionary<string, Variant>();
                    _currentDatabase.AddAchievement(achievement);
                    importedCount++;
                }
                else if (hasChanges)
                {
                    updatedCount++;
                }
                else
                {
                    skippedCount++;
                }
            }

            file.Close();

            MarkDirty();
            SaveDatabase();
            RefreshAchievementList(preserveSelection: true);

            var resultDialog = new AcceptDialog();
            resultDialog.DialogText = $"CSV Import Complete!\n\nNew achievements: {importedCount}\nUpdated achievements: {updatedCount}\nSkipped (unchanged): {skippedCount}";
            resultDialog.Title = "Import Successful";
            AddChild(resultDialog);
            resultDialog.PopupCentered();

            GD.Print($"[Achievements:Editor] Imported CSV from {path}: {importedCount} new, {updatedCount} updated, {skippedCount} skipped");
        }
        catch (Exception ex)
        {
            ShowErrorDialog($"Failed to import CSV: {ex.Message}");
            GD.PushError($"[Achievements:Editor] CSV import error: {ex}");
        }
    }

    private void OnExportCSVFileSelected(string path)
    {
        if (_currentDatabase == null || _currentDatabase.Achievements.Count == 0)
            return;

        try
        {
            var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
            if (file == null)
            {
                ShowErrorDialog($"Failed to create file: {FileAccess.GetOpenError()}");
                return;
            }

            // Write header using Godot's StoreCsvLine
            file.StoreCsvLine(new string[] { "Id", "DisplayName", "Description", "SteamId", "GameCenterId", "GooglePlayId", "IsIncremental", "MaxProgress" });

            // Write achievement rows using Godot's StoreCsvLine (handles escaping automatically)
            foreach (var achievement in _currentDatabase.Achievements)
            {
                file.StoreCsvLine(new string[]
                {
                    achievement.Id ?? "",
                    achievement.DisplayName ?? "",
                    achievement.Description ?? "",
                    achievement.SteamId ?? "",
                    achievement.GameCenterId ?? "",
                    achievement.GooglePlayId ?? "",
                    achievement.IsIncremental.ToString().ToLower(),
                    achievement.MaxProgress.ToString()
                });
            }

            file.Close();

            var resultDialog = new AcceptDialog();
            resultDialog.DialogText = $"Successfully exported {_currentDatabase.Achievements.Count} achievements to:\n\n{path}";
            resultDialog.Title = "Export Successful";
            AddChild(resultDialog);
            resultDialog.PopupCentered();

            GD.Print($"[Achievements:Editor] Exported {_currentDatabase.Achievements.Count} achievements to {path}");
        }
        catch (Exception ex)
        {
            ShowErrorDialog($"Failed to export CSV: {ex.Message}");
            GD.PushError($"[Achievements:Editor] CSV export error: {ex}");
        }
    }

    private void OnImportJSONFileSelected(string path)
    {
        if (_currentDatabase == null)
            return;

        try
        {
            var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
            if (file == null)
            {
                ShowErrorDialog($"Failed to open file: {FileAccess.GetOpenError()}");
                return;
            }

            var jsonContent = file.GetAsText();
            file.Close();

            if (string.IsNullOrWhiteSpace(jsonContent))
            {
                ShowErrorDialog("JSON file is empty.");
                return;
            }

            // Parse JSON
            JsonDocument? doc;
            try
            {
                doc = JsonDocument.Parse(jsonContent);
            }
            catch (JsonException ex)
            {
                ShowErrorDialog($"Invalid JSON format: {ex.Message}");
                return;
            }

            var root = doc.RootElement;
            JsonElement achievementsArray;

            // Support both { "achievements": [...] } and direct array [...]
            if (root.ValueKind == JsonValueKind.Array)
            {
                achievementsArray = root;
            }
            else if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("achievements", out var achProp))
            {
                achievementsArray = achProp;
            }
            else
            {
                ShowErrorDialog("JSON must be an array of achievements or an object with an 'achievements' array.");
                return;
            }

            int importedCount = 0;
            int updatedCount = 0;
            int skippedCount = 0;

            foreach (var item in achievementsArray.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                    continue;

                // Get Id (required)
                if (!item.TryGetProperty("Id", out var idProp) && !item.TryGetProperty("id", out idProp))
                    continue;

                var id = idProp.GetString();
                if (string.IsNullOrWhiteSpace(id))
                    continue;

                // Check if achievement already exists
                var existing = _currentDatabase.GetById(id);
                bool isNew = existing == null;

                var achievement = existing ?? new Achievement { Id = id };
                bool hasChanges = false;

                // Update fields from JSON, tracking if any changes occur
                if (TryGetJsonString(item, "DisplayName", out var displayName) || TryGetJsonString(item, "displayName", out displayName))
                {
                    if (achievement.DisplayName != displayName)
                    {
                        achievement.DisplayName = displayName;
                        hasChanges = true;
                    }
                }

                if (TryGetJsonString(item, "Description", out var description) || TryGetJsonString(item, "description", out description))
                {
                    if (achievement.Description != description)
                    {
                        achievement.Description = description;
                        hasChanges = true;
                    }
                }

                if (TryGetJsonString(item, "SteamId", out var steamId) || TryGetJsonString(item, "steamId", out steamId))
                {
                    if (achievement.SteamId != steamId)
                    {
                        achievement.SteamId = steamId;
                        hasChanges = true;
                    }
                }

                if (TryGetJsonString(item, "GameCenterId", out var gameCenterId) || TryGetJsonString(item, "gameCenterId", out gameCenterId))
                {
                    if (achievement.GameCenterId != gameCenterId)
                    {
                        achievement.GameCenterId = gameCenterId;
                        hasChanges = true;
                    }
                }

                if (TryGetJsonString(item, "GooglePlayId", out var googlePlayId) || TryGetJsonString(item, "googlePlayId", out googlePlayId))
                {
                    if (achievement.GooglePlayId != googlePlayId)
                    {
                        achievement.GooglePlayId = googlePlayId;
                        hasChanges = true;
                    }
                }

                if (TryGetJsonBool(item, "IsIncremental", out var isIncremental) || TryGetJsonBool(item, "isIncremental", out isIncremental))
                {
                    if (achievement.IsIncremental != isIncremental)
                    {
                        achievement.IsIncremental = isIncremental;
                        hasChanges = true;
                    }
                }

                if (TryGetJsonInt(item, "MaxProgress", out var maxProgress) || TryGetJsonInt(item, "maxProgress", out maxProgress))
                {
                    if (achievement.MaxProgress != maxProgress)
                    {
                        achievement.MaxProgress = maxProgress;
                        hasChanges = true;
                    }
                }

                if (isNew)
                {
                    achievement.CustomPlatformIds = new Godot.Collections.Dictionary<string, string>();
                    achievement.ExtraProperties = new Godot.Collections.Dictionary<string, Variant>();
                    _currentDatabase.AddAchievement(achievement);
                    importedCount++;
                }
                else if (hasChanges)
                {
                    updatedCount++;
                }
                else
                {
                    skippedCount++;
                }
            }

            doc.Dispose();

            MarkDirty();
            SaveDatabase();
            RefreshAchievementList(preserveSelection: true);

            var resultDialog = new AcceptDialog();
            resultDialog.DialogText = $"JSON Import Complete!\n\nNew achievements: {importedCount}\nUpdated achievements: {updatedCount}\nSkipped (unchanged): {skippedCount}";
            resultDialog.Title = "Import Successful";
            AddChild(resultDialog);
            resultDialog.PopupCentered();

            GD.Print($"[Achievements:Editor] Imported JSON from {path}: {importedCount} new, {updatedCount} updated, {skippedCount} skipped");
        }
        catch (Exception ex)
        {
            ShowErrorDialog($"Failed to import JSON: {ex.Message}");
            GD.PushError($"[Achievements:Editor] JSON import error: {ex}");
        }
    }

    private void OnExportJSONFileSelected(string path)
    {
        if (_currentDatabase == null || _currentDatabase.Achievements.Count == 0)
            return;

        try
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = null // Keep PascalCase for compatibility with Godot conventions
            };

            // Build achievements list for JSON
            var achievementsList = new System.Collections.Generic.List<object>();
            foreach (var achievement in _currentDatabase.Achievements)
            {
                achievementsList.Add(new
                {
                    Id = achievement.Id ?? "",
                    DisplayName = achievement.DisplayName ?? "",
                    Description = achievement.Description ?? "",
                    SteamId = achievement.SteamId ?? "",
                    GameCenterId = achievement.GameCenterId ?? "",
                    GooglePlayId = achievement.GooglePlayId ?? "",
                    IsIncremental = achievement.IsIncremental,
                    MaxProgress = achievement.MaxProgress
                });
            }

            var exportObject = new { achievements = achievementsList };
            var jsonContent = JsonSerializer.Serialize(exportObject, options);

            var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
            if (file == null)
            {
                ShowErrorDialog($"Failed to create file: {FileAccess.GetOpenError()}");
                return;
            }

            file.StoreString(jsonContent);
            file.Close();

            var resultDialog = new AcceptDialog();
            resultDialog.DialogText = $"Successfully exported {_currentDatabase.Achievements.Count} achievements to:\n\n{path}";
            resultDialog.Title = "Export Successful";
            AddChild(resultDialog);
            resultDialog.PopupCentered();

            GD.Print($"[Achievements:Editor] Exported {_currentDatabase.Achievements.Count} achievements to JSON: {path}");
        }
        catch (Exception ex)
        {
            ShowErrorDialog($"Failed to export JSON: {ex.Message}");
            GD.PushError($"[Achievements:Editor] JSON export error: {ex}");
        }
    }

    private static bool TryGetJsonString(JsonElement element, string propertyName, out string value)
    {
        value = string.Empty;
        if (element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String)
        {
            value = prop.GetString() ?? string.Empty;
            return true;
        }
        return false;
    }

    private static bool TryGetJsonBool(JsonElement element, string propertyName, out bool value)
    {
        value = false;
        if (element.TryGetProperty(propertyName, out var prop))
        {
            if (prop.ValueKind == JsonValueKind.True)
            {
                value = true;
                return true;
            }
            if (prop.ValueKind == JsonValueKind.False)
            {
                value = false;
                return true;
            }
        }
        return false;
    }

    private static bool TryGetJsonInt(JsonElement element, string propertyName, out int value)
    {
        value = 0;
        if (element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.Number)
        {
            if (prop.TryGetInt32(out value))
                return true;
        }
        return false;
    }

    private static string? GetCSVValue(string[] values, System.Collections.Generic.Dictionary<string, int> columnMap, string columnName)
    {
        if (!columnMap.TryGetValue(columnName, out int index) || index >= values.Length)
            return null;
        return values[index].Trim();
    }

    private void ShowErrorDialog(string message)
    {
        var dialog = new AcceptDialog();
        dialog.DialogText = message;
        dialog.Title = "Error";
        AddChild(dialog);
        dialog.PopupCentered();
    }

    #endregion
}
#endif
