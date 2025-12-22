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
    private AchievementDatabase? _database;
    private ItemList? _achievementList;
    private VBoxContainer? _detailsPanel;
    private LineEdit? _idEdit;
    private LineEdit? _displayNameEdit;
    private TextEdit? _descriptionEdit;
    private LineEdit? _steamIdEdit;
    private LineEdit? _gameCenterIdEdit;
    private LineEdit? _googlePlayIdEdit;
    private Button? _addButton;
    private Button? _removeButton;
    private Button? _saveButton;
    private Label? _statusLabel;
    private int _selectedIndex = -1;

    /// <summary>
    /// Initializes the editor dock UI with a two-panel layout (list + details)
    /// </summary>
    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(0, 300);

        var hbox = new HBoxContainer();
        hbox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        hbox.SizeFlagsVertical = SizeFlags.ExpandFill;
        AddChild(hbox);

        // Left panel: Achievement list with add/remove buttons
        var leftPanel = CreateLeftPanel();
        leftPanel.SizeFlagsHorizontal = SizeFlags.Fill;
        leftPanel.CustomMinimumSize = new Vector2(250, 0);
        hbox.AddChild(leftPanel);

        // Right panel: Achievement details editor (hidden until selection)
        _detailsPanel = CreateRightPanel();
        _detailsPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        hbox.AddChild(_detailsPanel);

        // Attempt to automatically load database from default path
        LoadDatabase();
    }

    /// <summary>
    /// Creates the left panel containing the achievement list and action buttons
    /// </summary>
    /// <returns>VBoxContainer with list UI and add/remove buttons</returns>
    private VBoxContainer CreateLeftPanel()
    {
        var vbox = new VBoxContainer();

        // Header
        var header = new Label();
        header.Text = "Achievements";
        header.AddThemeFontSizeOverride("font_size", 16);
        vbox.AddChild(header);

        // Database path selector
        var pathBox = new HBoxContainer();
        var pathLabel = new Label();
        pathLabel.Text = "Database: ";
        pathBox.AddChild(pathLabel);

        var pathButton = new Button();
        pathButton.Text = "Select Database...";
        pathButton.Pressed += OnSelectDatabasePressed;
        pathBox.AddChild(pathButton);
        vbox.AddChild(pathBox);

        var separator1 = new HSeparator();
        vbox.AddChild(separator1);

        // Achievement list
        _achievementList = new ItemList();
        _achievementList.SizeFlagsVertical = SizeFlags.ExpandFill;
        _achievementList.ItemSelected += OnAchievementSelected;
        vbox.AddChild(_achievementList);

        // Buttons
        var buttonBox = new HBoxContainer();
        _addButton = new Button();
        _addButton.Text = "Add";
        _addButton.Pressed += OnAddPressed;
        buttonBox.AddChild(_addButton);

        _removeButton = new Button();
        _removeButton.Text = "Remove";
        _removeButton.Pressed += OnRemovePressed;
        _removeButton.Disabled = true;
        buttonBox.AddChild(_removeButton);

        vbox.AddChild(buttonBox);

        return vbox;
    }

    /// <summary>
    /// Creates the right panel containing achievement detail editors and save button
    /// </summary>
    /// <returns>VBoxContainer with form fields for editing achievement properties</returns>
    private VBoxContainer CreateRightPanel()
    {
        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 8);

        // Header
        var header = new Label();
        header.Text = "Achievement Details";
        header.AddThemeFontSizeOverride("font_size", 16);
        vbox.AddChild(header);

        var separator = new HSeparator();
        vbox.AddChild(separator);

        // Scroll container for form
        var scroll = new ScrollContainer();
        scroll.SizeFlagsVertical = SizeFlags.ExpandFill;
        vbox.AddChild(scroll);

        var formContainer = new VBoxContainer();
        formContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        formContainer.AddThemeConstantOverride("separation", 12);
        scroll.AddChild(formContainer);

        // Basic Info Section
        var basicLabel = new Label();
        basicLabel.Text = "Basic Information";
        basicLabel.AddThemeFontSizeOverride("font_size", 14);
        formContainer.AddChild(basicLabel);

        _idEdit = CreateLabeledLineEdit(formContainer, "ID:");
        _displayNameEdit = CreateLabeledLineEdit(formContainer, "Display Name:");

        var descLabel = new Label();
        descLabel.Text = "Description:";
        formContainer.AddChild(descLabel);

        _descriptionEdit = new TextEdit();
        _descriptionEdit.CustomMinimumSize = new Vector2(0, 80);
        formContainer.AddChild(_descriptionEdit);

        // Platform IDs Section
        var separator2 = new HSeparator();
        formContainer.AddChild(separator2);

        var platformLabel = new Label();
        platformLabel.Text = "Platform IDs";
        platformLabel.AddThemeFontSizeOverride("font_size", 14);
        formContainer.AddChild(platformLabel);

        _steamIdEdit = CreateLabeledLineEdit(formContainer, "Steam ID:");
        _gameCenterIdEdit = CreateLabeledLineEdit(formContainer, "Game Center ID:");
        _googlePlayIdEdit = CreateLabeledLineEdit(formContainer, "Google Play ID:");

        // Save button
        var separator3 = new HSeparator();
        vbox.AddChild(separator3);

        var bottomBox = new HBoxContainer();

        _saveButton = new Button();
        _saveButton.Text = "Save Changes";
        _saveButton.Pressed += OnSavePressed;
        bottomBox.AddChild(_saveButton);

        _statusLabel = new Label();
        _statusLabel.Text = "";
        bottomBox.AddChild(_statusLabel);

        vbox.AddChild(bottomBox);

        // Initially disable details panel
        vbox.Visible = false;

        return vbox;
    }

    /// <summary>
    /// Helper method to create a labeled line edit control
    /// </summary>
    /// <param name="parent">Parent container to add the label and line edit to</param>
    /// <param name="labelText">Text to display in the label</param>
    /// <returns>The created LineEdit control</returns>
    private LineEdit CreateLabeledLineEdit(VBoxContainer parent, string labelText)
    {
        var label = new Label();
        label.Text = labelText;
        parent.AddChild(label);

        var lineEdit = new LineEdit();
        parent.AddChild(lineEdit);

        return lineEdit;
    }

    /// <summary>
    /// Attempts to load the achievement database from the default path (res://achievements.tres)
    /// </summary>
    private void LoadDatabase()
    {
        // Try to find achievement database in project
        var databasePath = "res://achievements.tres";

        if (ResourceLoader.Exists(databasePath))
        {
            _database = ResourceLoader.Load<AchievementDatabase>(databasePath);
            if (_database != null)
            {
                RefreshAchievementList();
                ShowStatus($"Loaded database from {databasePath}", false);
                return;
            }
        }

        ShowStatus("No database loaded. Create or select an achievement database.", true);
    }

    /// <summary>
    /// Refreshes the achievement list UI with current database contents
    /// </summary>
    private void RefreshAchievementList()
    {
        if (_achievementList == null || _database == null)
            return;

        _achievementList.Clear();

        foreach (var achievement in _database.Achievements)
        {
            _achievementList.AddItem($"{achievement.DisplayName} ({achievement.Id})");
        }
    }

    /// <summary>
    /// Handler for the "Select Database" button press
    /// </summary>
    private void OnSelectDatabasePressed()
    {
        // TODO: Implement file dialog for selecting database
        ShowStatus("Database selection not yet implemented", true);
    }

    /// <summary>
    /// Handler for when an achievement is selected in the list
    /// Shows the details panel and enables the remove button
    /// </summary>
    /// <param name="index">Index of the selected achievement</param>
    private void OnAchievementSelected(long index)
    {
        _selectedIndex = (int)index;
        LoadAchievementDetails(_selectedIndex);

        if (_removeButton != null)
            _removeButton.Disabled = false;

        if (_detailsPanel != null)
            _detailsPanel.Visible = true;
    }

    /// <summary>
    /// Loads achievement data into the detail editor form
    /// </summary>
    /// <param name="index">Index of the achievement to load</param>
    private void LoadAchievementDetails(int index)
    {
        if (_database == null || index < 0 || index >= _database.Achievements.Count)
            return;

        var achievement = _database.Achievements[index];

        if (_idEdit != null) _idEdit.Text = achievement.Id;
        if (_displayNameEdit != null) _displayNameEdit.Text = achievement.DisplayName;
        if (_descriptionEdit != null) _descriptionEdit.Text = achievement.Description;
        if (_steamIdEdit != null) _steamIdEdit.Text = achievement.SteamId;
        if (_gameCenterIdEdit != null) _gameCenterIdEdit.Text = achievement.GameCenterId;
        if (_googlePlayIdEdit != null) _googlePlayIdEdit.Text = achievement.GooglePlayId;
    }

    /// <summary>
    /// Handler for the "Add" button press
    /// Creates a new achievement with default values and adds it to the database
    /// </summary>
    private void OnAddPressed()
    {
        if (_database == null)
        {
            ShowStatus("No database loaded", true);
            return;
        }

        var newAchievement = new Achievement
        {
            Id = $"achievement_{_database.Achievements.Count + 1}",
            DisplayName = "New Achievement",
            Description = "Description here"
        };

        _database.AddAchievement(newAchievement);
        RefreshAchievementList();
        ShowStatus("Achievement added", false);
    }

    /// <summary>
    /// Handler for the "Remove" button press
    /// Removes the selected achievement from the database
    /// </summary>
    private void OnRemovePressed()
    {
        if (_database == null || _selectedIndex < 0 || _selectedIndex >= _database.Achievements.Count)
            return;

        var achievement = _database.Achievements[_selectedIndex];
        _database.RemoveAchievement(achievement.Id);

        _selectedIndex = -1;
        if (_removeButton != null)
            _removeButton.Disabled = true;

        if (_detailsPanel != null)
            _detailsPanel.Visible = false;

        RefreshAchievementList();
        ShowStatus("Achievement removed", false);
    }

    /// <summary>
    /// Handler for the "Save Changes" button press
    /// Validates and saves the current achievement's modified data to the database resource file
    /// </summary>
    private void OnSavePressed()
    {
        if (_database == null || _selectedIndex < 0 || _selectedIndex >= _database.Achievements.Count)
            return;

        var achievement = _database.Achievements[_selectedIndex];

        if (_idEdit != null) achievement.Id = _idEdit.Text;
        if (_displayNameEdit != null) achievement.DisplayName = _displayNameEdit.Text;
        if (_descriptionEdit != null) achievement.Description = _descriptionEdit.Text;
        if (_steamIdEdit != null) achievement.SteamId = _steamIdEdit.Text;
        if (_gameCenterIdEdit != null) achievement.GameCenterId = _gameCenterIdEdit.Text;
        if (_googlePlayIdEdit != null) achievement.GooglePlayId = _googlePlayIdEdit.Text;

        // Validate database
        var errors = _database.Validate();
        if (errors.Length > 0)
        {
            ShowStatus($"Validation errors: {string.Join(", ", errors)}", true);
            return;
        }

        // Save database resource
        var error = ResourceSaver.Save(_database, "res://achievements.tres");
        if (error == Error.Ok)
        {
            RefreshAchievementList();
            ShowStatus("Changes saved successfully", false);
        }
        else
        {
            ShowStatus($"Failed to save: {error}", true);
        }
    }

    /// <summary>
    /// Displays a status message to the user with color coding
    /// Message automatically clears after 3 seconds
    /// </summary>
    /// <param name="message">Status message to display</param>
    /// <param name="isError">Whether this is an error (red) or success (green) message</param>
    private void ShowStatus(string message, bool isError)
    {
        if (_statusLabel == null)
            return;

        _statusLabel.Text = message;
        _statusLabel.AddThemeColorOverride("font_color", isError ? Colors.Red : Colors.Green);

        // Clear status after 3 seconds
        var timer = GetTree().CreateTimer(3.0);
        timer.Timeout += () =>
        {
            if (_statusLabel != null)
                _statusLabel.Text = "";
        };
    }
}
#endif
