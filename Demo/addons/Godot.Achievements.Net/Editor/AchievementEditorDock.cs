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
    private AchievementEditorDetailsPanel DetailsPanel = null!;
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
    private ConfirmationDialog? _removeConfirmDialog;
    private PopupMenu? _contextMenu;
    private EditorUndoRedoManager? _undoRedoManager;

    // Composed Components
    private AchievementCrudOperations? _crudOperations;
    private AchievementListContextMenu? _contextMenuHandler;
    private AchievementImportExportHandler? _importExportHandler;

    // Validation
    private System.Collections.Generic.Dictionary<Achievement, AchievementValidationResult> _validationResults = new();
    private System.Collections.Generic.List<string> _duplicateInternalIds = new();
    private Texture2D? _warningIcon;
    private const string WARNING_PREFIX = "\u26a0 "; // ⚠ Unicode warning sign
    private const string ERROR_PREFIX = "\u274c "; // ❌ Unicode cross mark

    // Track last known database path for change detection
    private string _lastKnownDatabasePath = string.Empty;    

    public override void _Ready()
    {
        // Create confirmation dialog for removing achievements
        _removeConfirmDialog = new ConfirmationDialog();
        _removeConfirmDialog.Confirmed += ConfirmRemoveAchievement;
        AddChild(_removeConfirmDialog);

        // Initialize CRUD operations component
        _crudOperations = new AchievementCrudOperations(
            getDatabaseFunc: () => _currentDatabase,
            saveDatabase: SaveDatabase,
            refreshList: RefreshAchievementList,
            selectAchievementById: SelectAchievementById
        );

        // Initialize context menu handler
        _contextMenuHandler = new AchievementListContextMenu(
            itemList: ItemList,
            getDatabaseFunc: () => _currentDatabase,
            getDatabasePathFunc: () => _currentDatabasePath,
            getSearchTextFunc: () => SearchLineEdit.Text ?? string.Empty
        );
        _contextMenu = _contextMenuHandler.CreateContextMenu(this);
        _contextMenuHandler.MoveUpRequested += OnContextMenuMoveUpRequested;
        _contextMenuHandler.MoveDownRequested += OnContextMenuMoveDownRequested;

        // Initialize import/export handler
        _importExportHandler = new AchievementImportExportHandler(
            getDatabaseFunc: () => _currentDatabase,
            saveDatabase: SaveDatabase,
            refreshList: RefreshAchievementList,
            getParentNodeFunc: () => this
        );
        _importExportHandler.SetupMenuButtons(ImportMenuButton, ExportMenuButton);
        _importExportHandler.CreateFileDialogs(this);

        // Connect UI signals
        AddAchievementButton.Pressed += OnAddAchievementPressed;
        RemoveButton.Pressed += OnRemovePressed;
        DuplicateButton.Pressed += OnDuplicatePressed;
        SearchLineEdit.TextChanged += OnSearchTextChanged;
        ItemList.ItemSelected += OnItemSelected;
        ItemList.ItemClicked += OnItemListClicked;  // Delegates to _contextMenuHandler

        // Connect details panel signals
        DetailsPanel.AchievementDisplayNameChanged += OnAchievementDisplayNameChanged;
        DetailsPanel.AchievementChanged += OnAchievementChanged;

        // Pass undo/redo manager if already set
        if (_undoRedoManager != null)
        {
            DetailsPanel.SetUndoRedoManager(_undoRedoManager);
            _crudOperations.SetUndoRedoManager(_undoRedoManager);
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
    }

    /// <summary>
    /// Sets the undo/redo manager for editor history support
    /// </summary>
    public void SetUndoRedoManager(EditorUndoRedoManager undoRedoManager)
    {
        _undoRedoManager = undoRedoManager;
        DetailsPanel.SetUndoRedoManager(undoRedoManager);
        _crudOperations?.SetUndoRedoManager(undoRedoManager);
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
        DetailsPanel.SteamVBox.Visible = GetPlatformEnabled(AchievementSettings.SteamEnabled);
        DetailsPanel.GameCenterVBox.Visible = GetPlatformEnabled(AchievementSettings.GameCenterEnabled);
        DetailsPanel.GooglePlayVBox.Visible = GetPlatformEnabled(AchievementSettings.GooglePlayEnabled);
    }

    public override void _ExitTree()
    {
        // Disconnect signals
        ProjectSettings.Singleton.SettingsChanged -= OnProjectSettingsChanged;
        VisibilityChanged -= OnVisibilityChanged;

        DetailsPanel.AchievementDisplayNameChanged -= OnAchievementDisplayNameChanged;
        DetailsPanel.AchievementChanged -= OnAchievementChanged;

        _importExportHandler?.Cleanup(ImportMenuButton, ExportMenuButton);

        if (_removeConfirmDialog != null)
        {
            _removeConfirmDialog.Confirmed -= ConfirmRemoveAchievement;
            _removeConfirmDialog.QueueFree();
        }

        if (_contextMenuHandler != null)
        {
            _contextMenuHandler.MoveUpRequested -= OnContextMenuMoveUpRequested;
            _contextMenuHandler.MoveDownRequested -= OnContextMenuMoveDownRequested;
            _contextMenuHandler.Cleanup();
        }

        AddAchievementButton.Pressed -= OnAddAchievementPressed;
        RemoveButton.Pressed -= OnRemovePressed;
        DuplicateButton.Pressed -= OnDuplicatePressed;
        SearchLineEdit.TextChanged -= OnSearchTextChanged;
        ItemList.ItemSelected -= OnItemSelected;
        ItemList.ItemClicked -= OnItemListClicked;
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

    private void OnAchievementDisplayNameChanged(Achievement achievement)
    {
        UpdateListItemForAchievement(achievement);
    }

    private void OnAchievementChanged()
    {
        // Refresh the full list to re-run all validations (including duplicate detection)
        RefreshAchievementList(preserveSelection: true);
    }

    private void UpdateDatabasePathLabel()
    {
        if (_currentDatabase == null)
        {
            DatabasePathLabel.Text = "No database loaded";
        }
        else
        {
            var displayPath = ResolveUidToPath(_currentDatabasePath);
            DatabasePathLabel.Text = displayPath;
        }
    }

    private void UpdateListItemForAchievement(Achievement? achievement)
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
            AchievementLogger.Warning(AchievementLogger.Areas.Editor, $"Database not found at {path}");
            _currentDatabase = null;
            _currentDatabasePath = string.Empty;
            DatabasePathLabel.Text = "No database loaded";
            DetailsPanel.SetDatabase(null);
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
            AchievementLogger.Error(AchievementLogger.Areas.Editor, $"Resource at {path} is not an AchievementDatabase: {ex.Message}");
            var dialog = new AcceptDialog();
            dialog.DialogText = $"The file at:\n{path}\n\nis not a valid AchievementDatabase resource.\n\nPlease select a valid AchievementDatabase (.tres) file.";
            AddChild(dialog);
            dialog.PopupCentered();
            return;
        }

        if (resource == null)
        {
            AchievementLogger.Error(AchievementLogger.Areas.Editor, $"Failed to load database from {path}");
            var dialog = new AcceptDialog();
            dialog.DialogText = $"Failed to load database from:\n{path}\n\nPlease select a valid AchievementDatabase resource.";
            AddChild(dialog);
            dialog.PopupCentered();
            return;
        }

        _currentDatabase = resource;
        _currentDatabasePath = path;
        SaveDatabasePath(path);
        UpdateDatabasePathLabel();
        DetailsPanel.SetDatabase(_currentDatabase);
        RefreshAchievementList();

        AchievementLogger.Log(AchievementLogger.Areas.Editor, $"Loaded database from {path}");
    }

    public void SaveDatabase()
    {
        if (_currentDatabase == null)
        {
            AchievementLogger.Warning(AchievementLogger.Areas.Editor, "No database loaded to save");
            return;
        }

        var validationErrors = _currentDatabase.Validate();
        if (validationErrors.Length > 0)
        {
            AchievementLogger.Warning(AchievementLogger.Areas.Editor, "Database validation warnings:");
            foreach (var validationError in validationErrors)
            {
                AchievementLogger.Warning(AchievementLogger.Areas.Editor, $"  - {validationError}");
            }
        }

        var savePath = ResolveUidToPath(_currentDatabasePath);
        var saveError = ResourceSaver.Save(_currentDatabase, savePath);
        if (saveError != Error.Ok)
        {
            AchievementLogger.Error(AchievementLogger.Areas.Editor, $"Failed to save database: {saveError}");
            return;
        }

        AchievementLogger.Log(AchievementLogger.Areas.Editor, $"Database saved to {savePath}");

        // Refresh the Inspector if it's showing this resource
        _currentDatabase.EmitChanged();
        _currentDatabase.NotifyPropertyListChanged();

        // Auto-generate constants if enabled
        TryAutoGenerateConstants();
    }

    private void TryAutoGenerateConstants()
    {
        if (_currentDatabase == null || _currentDatabase.Achievements.Count == 0)
        {
            return;
        }

        // Check if auto-generation is enabled
        if (!ProjectSettings.HasSetting(AchievementSettings.ConstantsAutoGenerate))
        {
            return;
        }

        var autoGenerate = ProjectSettings.GetSetting(AchievementSettings.ConstantsAutoGenerate).AsBool();
        if (!autoGenerate)
        {
            return;
        }

        // Get settings
        var outputPath = GetSettingOrDefault(AchievementSettings.ConstantsOutputPath, AchievementSettings.DefaultConstantsOutputPath);
        var className = GetSettingOrDefault(AchievementSettings.ConstantsClassName, AchievementSettings.DefaultConstantsClassName);
        var namespaceName = GetSettingOrDefault(AchievementSettings.ConstantsNamespace, null);

        var result = AchievementConstantsGenerator.Generate(
            _currentDatabase,
            outputPath,
            className,
            string.IsNullOrWhiteSpace(namespaceName) ? null : namespaceName);

        if (result.Success)
        {
            AchievementLogger.Log(AchievementLogger.Areas.Editor, $"Auto-generated constants for {result.GeneratedCount} achievements to {result.OutputPath}");
            EditorInterface.Singleton.GetResourceFilesystem().Scan();
        }
        else
        {
            AchievementLogger.Warning(AchievementLogger.Areas.Editor, $"Failed to auto-generate constants: {result.ErrorMessage}");
        }
    }

    private static string? GetSettingOrDefault(string key, string? defaultValue)
    {
        if (ProjectSettings.HasSetting(key))
        {
            var value = ProjectSettings.GetSetting(key).AsString();
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }
        return defaultValue;
    }

    private string LoadDatabasePath()
    {
        var hasSetting = ProjectSettings.HasSetting(AchievementSettings.DatabasePath);

        if (hasSetting)
        {
            var path = ProjectSettings.GetSetting(AchievementSettings.DatabasePath).AsString();
            return string.IsNullOrEmpty(path) ? AchievementSettings.DefaultDatabasePath : path;
        }
        return AchievementSettings.DefaultDatabasePath;
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
        ProjectSettings.SetSetting(AchievementSettings.DatabasePath, path);
        var saveError = ProjectSettings.Save();
        if (saveError != Error.Ok)
        {
            AchievementLogger.Warning(AchievementLogger.Areas.Editor, $"Failed to save project settings: {saveError}");
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
        _crudOperations?.CreateNewAchievement();
    }

    private void SelectAchievementById(string id)
    {
        for (int i = 0; i < ItemList.ItemCount; i++)
        {
            var achievement = ItemList.GetItemMetadata(i).As<Achievement>();
            if (achievement?.Id == id)
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
            AchievementLogger.Warning(AchievementLogger.Areas.Editor, "No achievement selected to remove");
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
        if (_selectedAchievement == null)
            return;

        var achievementToRemove = _selectedAchievement;
        _selectedAchievement = null;
        _selectedIndex = -1;
        _crudOperations?.RemoveAchievement(achievementToRemove);
    }

    private void OnDuplicatePressed()
    {
        if (_selectedAchievement == null)
        {
            AchievementLogger.Warning(AchievementLogger.Areas.Editor, "No achievement selected to duplicate");
            return;
        }

        _crudOperations?.DuplicateAchievement(_selectedAchievement);
    }

    #endregion

    #region Context Menu Operations

    private void OnItemListClicked(long index, Vector2 atPosition, long mouseButtonIndex)
    {
        _contextMenuHandler?.HandleItemListClicked(index, atPosition, mouseButtonIndex);
    }

    private void OnContextMenuMoveUpRequested(Achievement achievement)
    {
        _crudOperations?.MoveAchievementUp(achievement);
    }

    private void OnContextMenuMoveDownRequested(Achievement achievement)
    {
        _crudOperations?.MoveAchievementDown(achievement);
    }

    #endregion

}
#endif
