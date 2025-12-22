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
    private Button ImportCSVButton = null!;
    [Export]
    private Button ExportCSVButton = null!;

    // Private Fields
    private AchievementDatabase? _currentDatabase;
    private string _currentDatabasePath = string.Empty;
    private Achievement? _selectedAchievement;
    private int _selectedIndex = -1;
    private EditorFileDialog? _databaseFileDialog;
    private ConfirmationDialog? _removeConfirmDialog;
    private AcceptDialog? _unsavedChangesDialog;
    private PopupMenu? _contextMenu;
    private int _contextMenuTargetIndex = -1;
    private bool _hasUnsavedChanges = false;
    private Action? _pendingAction;

    private const string DATABASE_PATH_SETTING = "addons/achievements/database_path";
    private const string DEFAULT_DATABASE_PATH = "res://addons/Godot.Achievements.Net/_achievements/_achievements.tres";

    public override void _Ready()
    {
        // Create and configure database file dialog
        _databaseFileDialog = new EditorFileDialog();
        _databaseFileDialog.FileMode = EditorFileDialog.FileModeEnum.OpenFile;
        _databaseFileDialog.AddFilter("*.tres", "Godot Resource");
        _databaseFileDialog.Access = EditorFileDialog.AccessEnum.Resources;
        _databaseFileDialog.FileSelected += OnDatabaseFileSelected;
        AddChild(_databaseFileDialog);

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
        _contextMenu.IdPressed += OnContextMenuItemPressed;
        AddChild(_contextMenu);

        // Connect UI signals
        ChangeDatabaseButton.Pressed += OnChangeDatabasePressed;
        AddAchievementButton.Pressed += OnAddAchievementPressed;
        RemoveButton.Pressed += OnRemovePressed;
        DuplicateButton.Pressed += OnDuplicatePressed;
        SearchLineEdit.TextChanged += OnSearchTextChanged;
        ItemList.ItemSelected += OnItemSelected;
        ItemList.ItemClicked += OnItemListClicked;

        // Connect platform checkboxes
        if (SteamCheckbox != null)
            SteamCheckbox.Toggled += OnSteamCheckboxToggled;
        if (GameCenterCheckbox != null)
            GameCenterCheckbox.Toggled += OnGameCenterCheckboxToggled;
        if (GooglePlayCheckbox != null)
            GooglePlayCheckbox.Toggled += OnGooglePlayCheckboxToggled;

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

        // Set initial platform checkbox visibility
        if (DetailsPanel != null && SteamCheckbox != null && DetailsPanel.SteamVBox != null)
            DetailsPanel.SteamVBox.Visible = SteamCheckbox.ButtonPressed;
        if (DetailsPanel != null && GameCenterCheckbox != null && DetailsPanel.GameCenterVBox != null)
            DetailsPanel.GameCenterVBox.Visible = GameCenterCheckbox.ButtonPressed;
        if (DetailsPanel != null && GooglePlayCheckbox != null && DetailsPanel.GooglePlayVBox != null)
            DetailsPanel.GooglePlayVBox.Visible = GooglePlayCheckbox.ButtonPressed;

        // Load database from settings
        var savedPath = LoadDatabasePath();
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

    public override void _ExitTree()
    {
        // Disconnect signals
        VisibilityChanged -= OnVisibilityChanged;

        if (DetailsPanel != null)
        {
            DetailsPanel.AchievementIdChanged -= OnAchievementIdChanged;
            DetailsPanel.AchievementDisplayNameChanged -= OnAchievementDisplayNameChanged;
            DetailsPanel.AchievementChanged -= OnAchievementChanged;
        }

        if (_databaseFileDialog != null)
        {
            _databaseFileDialog.FileSelected -= OnDatabaseFileSelected;
            _databaseFileDialog.QueueFree();
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

        if (ChangeDatabaseButton != null)
            ChangeDatabaseButton.Pressed -= OnChangeDatabasePressed;
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
        if (SteamCheckbox != null)
            SteamCheckbox.Toggled -= OnSteamCheckboxToggled;
        if (GameCenterCheckbox != null)
            GameCenterCheckbox.Toggled -= OnGameCenterCheckboxToggled;
        if (GooglePlayCheckbox != null)
            GooglePlayCheckbox.Toggled -= OnGooglePlayCheckboxToggled;
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

    private void OnSteamCheckboxToggled(bool enabled)
    {
        if (DetailsPanel?.SteamVBox != null)
            DetailsPanel.SteamVBox.Visible = enabled;
    }

    private void OnGameCenterCheckboxToggled(bool enabled)
    {
        if (DetailsPanel?.GameCenterVBox != null)
            DetailsPanel.GameCenterVBox.Visible = enabled;
    }

    private void OnGooglePlayCheckboxToggled(bool enabled)
    {
        if (DetailsPanel?.GooglePlayVBox != null)
            DetailsPanel.GooglePlayVBox.Visible = enabled;
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
            DatabasePathLabel.Text = $"{prefix}{_currentDatabasePath}";
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

        // Find the item in the list
        for (int i = 0; i < ItemList.ItemCount; i++)
        {
            var itemAchievement = ItemList.GetItemMetadata(i).As<Achievement>();
            if (itemAchievement == achievement)
            {
                ItemList.SetItemText(i, achievement.DisplayName);
                ItemList.SetItemIcon(i, achievement.Icon);
                break;
            }
        }
    }

    #region Database Operations

    private void LoadDatabase(string path)
    {
        if (!FileAccess.FileExists(path))
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

        var saveError = ResourceSaver.Save(_currentDatabase, _currentDatabasePath);
        if (saveError != Error.Ok)
        {
            GD.PushError($"[Achievements:Editor] Failed to save database: {saveError}");
            return;
        }

        ClearDirty();
        GD.Print($"[Achievements:Editor] Database saved to {_currentDatabasePath}");
    }

    private string LoadDatabasePath()
    {
        if (ProjectSettings.HasSetting(DATABASE_PATH_SETTING))
        {
            var path = ProjectSettings.GetSetting(DATABASE_PATH_SETTING).AsString();
            return string.IsNullOrEmpty(path) ? DEFAULT_DATABASE_PATH : path;
        }
        return DEFAULT_DATABASE_PATH;
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

    private void OnChangeDatabasePressed()
    {
        if (_databaseFileDialog != null)
        {
            _databaseFileDialog.CurrentDir = "res://addons/Godot.Achievements.Net/_achievements";
            _databaseFileDialog.PopupCentered(new Vector2I(800, 600));
        }
    }

    private void OnDatabaseFileSelected(string path)
    {
        LoadDatabase(path);
    }

    #endregion

    #region List Management

    private void RefreshAchievementList(bool preserveSelection = false)
    {
        var previousSelection = preserveSelection && _selectedAchievement != null
            ? _selectedAchievement.Id
            : null;

        ItemList.Clear();
        _selectedAchievement = null;
        _selectedIndex = -1;

        if (_currentDatabase == null || _currentDatabase.Achievements.Count == 0)
        {
            NoItemsControl.Visible = true;
            ItemList.Visible = false;
            ItemListScrollContainer.Visible = false;
            DetailsPanel.Visible = false;
            NoItemSelectedScroll.Visible = true;
            UpdateButtonStates();
            return;
        }

        NoItemsControl.Visible = false;
        ItemList.Visible = true;
        ItemListScrollContainer.Visible = true;

        var searchText = SearchLineEdit.Text.ToLower();
        var filteredAchievements = _currentDatabase.Achievements
            .Where(a => string.IsNullOrEmpty(searchText)
                || a.DisplayName.ToLower().Contains(searchText)
                || a.Id.ToLower().Contains(searchText))
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
            var index = ItemList.AddItem(achievement.DisplayName, achievement.Icon);
            ItemList.SetItemMetadata(index, achievement);

            // Restore selection if this is the previously selected achievement
            if (previousSelection != null && achievement.Id == previousSelection)
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
        }
        else
        {
            DetailsPanel.Visible = false;
            NoItemSelectedScroll.Visible = true;
        }

        UpdateButtonStates();
    }

    private void OnSearchTextChanged(string newText)
    {
        RefreshAchievementList(preserveSelection: true);
    }

    private void OnItemSelected(long index)
    {
        var targetAchievement = ItemList.GetItemMetadata((int)index).As<Achievement>();

        // Check if there are unsaved changes before switching
        if (_hasUnsavedChanges && targetAchievement != _selectedAchievement)
        {
            _pendingAction = () => SelectAchievement((int)index, targetAchievement);
            ShowUnsavedChangesDialog();

            // Restore previous selection in the list (user might cancel)
            if (_selectedIndex >= 0 && _selectedIndex < ItemList.ItemCount)
            {
                ItemList.Select(_selectedIndex);
            }
            return;
        }

        SelectAchievement((int)index, targetAchievement);
    }

    private void SelectAchievement(int index, Achievement achievement)
    {
        _selectedIndex = index;
        _selectedAchievement = achievement;

        DetailsPanel.Visible = true;
        NoItemSelectedScroll.Visible = false;
        DetailsPanel.CurrentAchievement = _selectedAchievement;

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
            _removeConfirmDialog.DialogText = $"Are you sure you want to remove the achievement:\n\n'{_selectedAchievement.DisplayName}' ({_selectedAchievement.Id})\n\nThis will delete the .tres file from disk.";
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
}
#endif
