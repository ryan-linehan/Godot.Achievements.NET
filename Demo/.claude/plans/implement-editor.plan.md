# Achievements Editor Implementation Plan

## Overview

Implement full CRUD functionality for the achievements editor dock, including database management, resource creation/editing, search filtering, and platform-specific field visibility.

## Key Design Decisions

### 1. Database Path Storage

**Decision**: Store in ProjectSettings at `"addons/achievements/database_path"`

- Shared across the team (committed to VCS)
- Default: `"res://addons/Godot.Achievements.Net/_achievements/_achievements.tres"`
- Accessible from any node using `ProjectSettings.GetSetting()`

### 2. Platform Checkbox Behavior

**Decision**: Each checkbox shows/hides its corresponding field

- Steam checkbox → `SteamVBox.Visible`
- Game Center checkbox → `GameCenterVBox.Visible`
- Google Play checkbox → `GooglePlayVBox.Visible`
- Platform Identifiers foldable always visible

### 3. Icon Selection

**Decision**: Use `EditorResourcePicker` for Texture2D resources

- More Godot-native experience
- Shows texture preview
- Handles resource management automatically

### 4. Resource Saving Strategy

**Decision**: Use `ResourceSaver.Save()` with immediate save-on-edit

- No explicit "Save" button
- Each field change saves the resource immediately
- Matches Godot editor conventions

### 5. Resource Duplication

**Decision**: Manual deep clone via helper method

- Avoids Godot's `Duplicate()` issues with Dictionary sub-resources
- Manually copy all properties including CustomPlatformIds and ExtraProperties
- Generate unique ID with "_copy" suffix

### 6. File Naming Convention

**Decision**: Sequential numbering: `{counter:D2}_{sanitizedId}.achievement.tres`

- Example: `01_boss_defeated.achievement.tres`
- Matches existing pattern in the codebase
- Counter ensures uniqueness

## Implementation Phases

### Phase 1: Core Infrastructure (AchievementEditorDock.cs)

#### 1.1 Add Private Fields

```csharp
private AchievementDatabase? _currentDatabase;
private string _currentDatabasePath = string.Empty;
private Achievement? _selectedAchievement;
private int _selectedIndex = -1;
private EditorFileDialog? _databaseFileDialog;
private bool _needsRefresh = false;
```

#### 1.2 Implement _Ready()

- Create and configure `EditorFileDialog` for database selection
- Connect all UI signals (buttons, checkboxes, search, list selection)
- Load database from ProjectSettings or use default path
- Initialize button states (Remove/Duplicate disabled)
- Set initial platform checkbox visibility

#### 1.3 Database Operations

**LoadDatabase(string path)**

- Check file existence with `FileAccess.FileExists()`
- Load using `ResourceLoader.Load<AchievementDatabase>(path)`
- Update `DatabasePathLabel` with current path
- Save path to ProjectSettings
- Call `RefreshAchievementList()`

**SaveDatabase()**

- Validate database using `_currentDatabase.Validate()`
- Save using `ResourceSaver.Save(_currentDatabase, _currentDatabasePath)`
- Log warnings for validation errors

**SaveDatabasePath(string path) / LoadDatabasePath()**

- Use `ProjectSettings.SetSetting()` and `GetSetting()`
- Key: `"addons/achievements/database_path"`
- Fallback to default path if not set

#### 1.4 List Management

**RefreshAchievementList(bool preserveSelection = false)**

- Clear `ItemList`
- Apply search filter (check DisplayName and Id against search text)
- Populate ItemList with filtered achievements
- Set item text to `DisplayName`, icon to `Icon`
- Store Achievement reference in item metadata
- Restore selection if `preserveSelection` is true and item still exists
- Show `NoItemsControl` if list is empty, hide otherwise
- Update button states

**OnSearchTextChanged(string newText)**

- Call `RefreshAchievementList(preserveSelection: true)`

**OnItemSelected(long index)**

- Store selected achievement and index
- Show `DetailsPanel`, hide `NoItemSelectedScroll`
- Set `DetailsPanel.CurrentAchievement` to selected achievement
- Update button states

#### 1.5 Platform Checkbox Handlers

```csharp
private void OnPlatformCheckboxToggled(bool enabled, string platform)
{
    switch (platform)
    {
        case "Steam":
            DetailsPanel.SteamVBox.Visible = enabled;
            break;
        case "GameCenter":
            DetailsPanel.GameCenterVBox.Visible = enabled;
            break;
        case "GooglePlay":
            DetailsPanel.GooglePlayVBox.Visible = enabled;
            break;
    }
}
```

Connect in _Ready():

```csharp
SteamCheckbox.Toggled += (enabled) => OnPlatformCheckboxToggled(enabled, "Steam");
GameCenterCheckbox.Toggled += (enabled) => OnPlatformCheckboxToggled(enabled, "GameCenter");
GooglePlayCheckbox.Toggled += (enabled) => OnPlatformCheckboxToggled(enabled, "GooglePlay");
```

#### 1.6 Button State Management

```csharp
private void UpdateButtonStates()
{
    var hasSelection = _selectedAchievement != null;
    RemoveButton.Disabled = !hasSelection;
    DuplicateButton.Disabled = !hasSelection;
}
```

#### 1.7 Tab Visibility Detection

```csharp
public override void _Ready()
{
    // ... existing code ...
    VisibilityChanged += OnVisibilityChanged;
}

private void OnVisibilityChanged()
{
    if (Visible)
    {
        RefreshAchievementList(preserveSelection: true);
    }
}
```

#### 1.8 Signal Cleanup

```csharp
public override void _ExitTree()
{
    VisibilityChanged -= OnVisibilityChanged;

    if (_databaseFileDialog != null)
    {
        _databaseFileDialog.FileSelected -= OnDatabaseFileSelected;
        _databaseFileDialog.QueueFree();
    }

    // Disconnect all other signals
}
```

### Phase 2: CRUD Operations (AchievementEditorDock.cs)

#### 2.1 Add Achievement

**OnAddAchievementPressed()**

1. Check if database is loaded
2. Generate unique ID using `GenerateUniqueId()`
3. Create new `Achievement` instance with default values
4. Generate filename using `GenerateAchievementFileName()`
5. Save using `ResourceSaver.Save(achievement, filename)`
6. Add to database: `_currentDatabase.AddAchievement(achievement)`
7. Call `SaveDatabase()`
8. Refresh list and auto-select new item

**Helper Methods:**

```csharp
private string GenerateUniqueId()
{
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

private string GenerateAchievementFileName(string achievementId)
{
    var sanitized = achievementId.Replace(" ", "_").ToLower();
    sanitized = System.Text.RegularExpressions.Regex.Replace(sanitized, @"[^\w\-]", "");

    var counter = 1;
    string path;
    do
    {
        path = $"res://addons/Godot.Achievements.Net/_achievements/{counter:D2}_{sanitized}.achievement.tres";
        counter++;
    } while (FileAccess.FileExists(path));

    return path;
}
```

#### 2.2 Remove Achievement

**OnRemovePressed()**

1. Check if achievement is selected
2. Show `ConfirmationDialog` with achievement name
3. On confirm, call `ConfirmRemoveAchievement()`

**ConfirmRemoveAchievement()**

1. Remove from database: `_currentDatabase.RemoveAchievement(id)`
2. Call `SaveDatabase()`
3. Delete .tres file using `DirAccess.Open()` and `dir.Remove()`
4. Clear selection
5. Refresh list
6. Show `NoItemSelectedScroll`, hide `DetailsPanel`

#### 2.3 Duplicate Achievement

**OnDuplicatePressed()**

1. Check if achievement is selected
2. Call `DuplicateAchievement()` to create deep clone
3. Generate new filename
4. Save using `ResourceSaver.Save()`
5. Add to database
6. Call `SaveDatabase()`
7. Refresh list and auto-select duplicate

**DuplicateAchievement(Achievement original)**

```csharp
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
```

### Phase 3: Details Panel (AchievementsEditorDetailsPanel.cs)

#### 3.1 Add Private Fields

```csharp
private Achievement? _currentAchievement;
private bool _isUpdating = false;
private EditorResourcePicker? _iconPicker;
```

#### 3.2 Add Public Property

```csharp
public Achievement? CurrentAchievement
{
    get => _currentAchievement;
    set
    {
        _currentAchievement = value;
        LoadAchievementData();
    }
}
```

#### 3.3 Implement _Ready()

**Create Icon Picker**

```csharp
_iconPicker = new EditorResourcePicker();
_iconPicker.BaseType = "Texture2D";
_iconPicker.ResourceChanged += OnIconResourceChanged;

// Replace SelectIconButton with the picker or add to ImageSection
var imageSection = SelectIconButton.GetParent();
imageSection.AddChild(_iconPicker);
SelectIconButton.QueueFree(); // Remove the button
```

**Connect Field Signals**

```csharp
NameLineEdit.TextChanged += (text) => OnFieldChanged(() => _currentAchievement!.DisplayName = text);
InternalIDLineEdit.TextChanged += (text) => OnFieldChanged(() => _currentAchievement!.Id = text);
DescriptionTextBox.TextChanged += () => OnFieldChanged(() => _currentAchievement!.Description = DescriptionTextBox.Text);
SteamIDLineEdit.TextChanged += (text) => OnFieldChanged(() => _currentAchievement!.SteamId = text);
GooglePlayIDLineEdit.TextChanged += (text) => OnFieldChanged(() => _currentAchievement!.GooglePlayId = text);
GameCenterIDLineEdit.TextChanged += (text) => OnFieldChanged(() => _currentAchievement!.GameCenterId = text);
```

#### 3.4 Load Achievement Data

```csharp
private void LoadAchievementData()
{
    if (_currentAchievement == null)
    {
        ClearFields();
        return;
    }

    _isUpdating = true;

    NameLineEdit.Text = _currentAchievement.DisplayName;
    InternalIDLineEdit.Text = _currentAchievement.Id;
    DescriptionTextBox.Text = _currentAchievement.Description;

    if (_currentAchievement.Icon != null)
    {
        AchievementIconButton.TextureNormal = _currentAchievement.Icon;
        _iconPicker.EditedResource = _currentAchievement.Icon;
    }

    SteamIDLineEdit.Text = _currentAchievement.SteamId;
    GooglePlayIDLineEdit.Text = _currentAchievement.GooglePlayId;
    GameCenterIDLineEdit.Text = _currentAchievement.GameCenterId;

    _isUpdating = false;
}
```

#### 3.5 Save on Field Change

```csharp
private void OnFieldChanged(Action updateAction)
{
    if (_isUpdating || _currentAchievement == null) return;

    updateAction();
    SaveCurrentAchievement();
}

private void SaveCurrentAchievement()
{
    if (_currentAchievement == null) return;

    var path = _currentAchievement.ResourcePath;
    if (string.IsNullOrEmpty(path))
    {
        GD.PushError("[Achievements:Editor] Achievement has no resource path");
        return;
    }

    var error = ResourceSaver.Save(_currentAchievement, path);
    if (error != Error.Ok)
    {
        GD.PushError($"[Achievements:Editor] Failed to save achievement: {error}");
    }
}
```

#### 3.6 Icon Selection

```csharp
private void OnIconResourceChanged(Resource resource)
{
    if (_isUpdating || _currentAchievement == null) return;

    var texture = resource as Texture2D;
    if (texture == null) return;

    _currentAchievement.Icon = texture;
    AchievementIconButton.TextureNormal = texture;
    SaveCurrentAchievement();
}
```

#### 3.7 Signal Cleanup

```csharp
public override void _ExitTree()
{
    if (_iconPicker != null)
    {
        _iconPicker.ResourceChanged -= OnIconResourceChanged;
        _iconPicker.QueueFree();
    }

    // Disconnect all field signals
}
```

### Phase 4: Database Selection Dialog (AchievementEditorDock.cs)

#### 4.1 Create Dialog in _Ready()

```csharp
_databaseFileDialog = new EditorFileDialog();
_databaseFileDialog.FileMode = EditorFileDialog.FileModeEnum.OpenFile;
_databaseFileDialog.AddFilter("*.tres", "Godot Resource");
_databaseFileDialog.Access = EditorFileDialog.AccessEnum.Resources;
_databaseFileDialog.FileSelected += OnDatabaseFileSelected;
AddChild(_databaseFileDialog);
```

#### 4.2 Handle Button Press

```csharp
private void OnChangeDatabasePressed()
{
    _databaseFileDialog.CurrentDir = "res://addons/Godot.Achievements.Net/_achievements";
    _databaseFileDialog.PopupCentered(new Vector2I(800, 600));
}

private void OnDatabaseFileSelected(string path)
{
    LoadDatabase(path);
}
```

## Critical Files to Modify

1. **AchievementEditorDock.cs** (`C:\Users\rline\Documents\Godot\Projects\Godot.Achievements.NET\Demo\addons\Godot.Achievements.Net\Editor\AchievementEditorDock.cs`)
   - Main controller for the editor dock
   - ~500 lines of new code

2. **AchievementsEditorDetailsPanel.cs** (`C:\Users\rline\Documents\Godot\Projects\Godot.Achievements.NET\Demo\addons\Godot.Achievements.Net\Editor\AchievementsEditorDetailsPanel.cs`)
   - Details panel controller for field binding
   - ~200 lines of new code

## Important Edge Cases

### 1. Database Not Found

```csharp
if (!FileAccess.FileExists(path))
{
    GD.PushWarning($"[Achievements:Editor] Database not found at {path}");
    _currentDatabase = null;
    _currentDatabasePath = string.Empty;
    RefreshAchievementList();
    return;
}
```

### 2. Invalid Database Resource

```csharp
var resource = ResourceLoader.Load<AchievementDatabase>(path);
if (resource == null)
{
    GD.PushError($"[Achievements:Editor] Failed to load database from {path}");
    var dialog = new AcceptDialog();
    dialog.DialogText = $"Failed to load database from:\n{path}\n\nPlease select a valid AchievementDatabase resource.";
    AddChild(dialog);
    dialog.PopupCentered();
    return;
}
```

### 3. Search Returns No Results

```csharp
if (filteredAchievements.Count == 0 && !string.IsNullOrEmpty(searchText))
{
    NoItemsControl.Visible = true;
    // Could update label to show "No achievements match '{searchText}'"
}
```

### 4. ID Collision Prevention

- `GenerateUniqueId()` loops until finding unused ID
- Safety limit of 9999 iterations, then uses GUID
- User can manually edit ID in details panel (validates on save)

### 5. Duplicate ID Handling (Optional Enhancement)

If implementing ID change validation:

```csharp
// In OnFieldChanged for ID field
if (_currentDatabase?.GetById(newId) != null)
{
    GD.PushWarning($"[Achievements:Editor] Achievement with ID '{newId}' already exists");
    // Revert or show error dialog
}
```

## Data Flow

### Add Achievement Flow

```
User clicks "+ Add"
  → Generate unique ID
  → Create new Achievement()
  → Generate filename: {counter}_{id}.achievement.tres
  → ResourceSaver.Save(achievement, filename)
  → Database.AddAchievement(achievement)
  → SaveDatabase()
  → RefreshAchievementList()
  → Auto-select new item
  → Load in DetailsPanel
```

### Edit Achievement Flow

```
User edits field in DetailsPanel
  → TextChanged signal fires
  → OnFieldChanged(updateAction)
  → Check !_isUpdating flag
  → Execute updateAction (sets property)
  → SaveCurrentAchievement()
  → ResourceSaver.Save(achievement, path)
```

### Search Flow

```
User types in SearchLineEdit
  → TextChanged signal fires
  → OnSearchTextChanged()
  → RefreshAchievementList(preserveSelection: true)
  → Filter achievements by DisplayName/Id
  → Repopulate ItemList
  → Restore selection if item matches
```

### Tab Click Flow

```
User clicks "Achievements" tab
  → VisibilityChanged signal fires
  → OnVisibilityChanged()
  → If Visible: RefreshAchievementList(preserveSelection: true)
```

## Testing Checklist

### Database Operations

- [ ] Select database file via dialog
- [ ] Invalid database shows error dialog
- [ ] Database path saved to ProjectSettings
- [ ] Database path displayed in bottom label
- [ ] Default database loads on first launch

### Achievement CRUD

- [ ] Add creates new .tres file with sequential numbering
- [ ] Add generates unique IDs
- [ ] Add auto-selects new achievement
- [ ] Remove shows confirmation dialog
- [ ] Remove deletes from database and disk
- [ ] Duplicate creates copy with "_copy" suffix
- [ ] Duplicate preserves all properties including dictionaries

### Details Panel

- [ ] Name changes save immediately
- [ ] ID changes save immediately
- [ ] Description changes save immediately
- [ ] Icon picker shows texture preview
- [ ] Icon changes save immediately
- [ ] Platform IDs save correctly
- [ ] No saves triggered during LoadAchievementData()

### Search and Filtering

- [ ] Search filters by DisplayName
- [ ] Search filters by Id
- [ ] Search preserves selection if item matches
- [ ] Empty search shows all items
- [ ] No results shows NoItemsControl

### Platform Visibility

- [ ] Steam checkbox toggles SteamVBox visibility
- [ ] Game Center checkbox toggles GameCenterVBox visibility
- [ ] Google Play checkbox toggles GooglePlayVBox visibility
- [ ] Hidden fields still save data

### List Management

- [ ] Icons displayed in ItemList
- [ ] Selection updates DetailsPanel
- [ ] Selection shows DetailsPanel, hides NoItemSelectedScroll
- [ ] No selection shows NoItemSelectedScroll, hides DetailsPanel
- [ ] Remove/Duplicate disabled when no selection
- [ ] Tab click refreshes list and preserves selection

### Edge Cases

- [ ] Empty database handled gracefully
- [ ] Missing database path uses default
- [ ] ID collision prevented
- [ ] Long names/descriptions handled
- [ ] Special characters in IDs sanitized
- [ ] File naming handles duplicates

### Build Verification

- [ ] `dotnet build` succeeds with no errors
- [ ] No compiler warnings
- [ ] Plugin loads in Godot editor
- [ ] No console errors on initialization

## Godot APIs Used

### Resource Management

- `ResourceLoader.Load<T>(path)` - Load resources
- `ResourceSaver.Save(resource, path)` - Save resources
- `FileAccess.FileExists(path)` - Check file existence
- `DirAccess.Open(path)` - Directory operations

### Editor Integration

- `EditorFileDialog` - File selection dialogs
- `EditorResourcePicker` - Texture2D resource picker
- `ProjectSettings.SetSetting() / GetSetting()` - Persistent settings
- `ConfirmationDialog` / `AcceptDialog` - User prompts

### UI Controls

- `ItemList.AddItem()` - Add list items
- `ItemList.SetItemMetadata()` - Store references
- `ItemList.GetItemMetadata()` - Retrieve references
- Control visibility toggling

## Logging Format

Follow the standard from agents.md:

```csharp
GD.Print($"[Achievements:Editor] Loaded database from {path}");
GD.PushError($"[Achievements:Editor] Failed to save achievement: {error}");
GD.PushWarning($"[Achievements:Editor] Database validation warnings");
```

## Implementation Notes

### Resource Paths

- Always use `res://` paths (not absolute file system paths)
- Use `FileAccess`, never `System.IO`
- Achievement files: `res://addons/Godot.Achievements.Net/_achievements/*.achievement.tres`
- Database: `res://addons/Godot.Achievements.Net/_achievements/_achievements.tres`

### Signal Management

- Always disconnect signals in `_ExitTree()`
- Use lambda captures carefully (they can prevent GC)
- Use `_isUpdating` flag to prevent recursive updates

### Performance

- Search filtering uses LINQ `.Where()` - acceptable for < 1000 achievements
- `preserveSelection` flag avoids unnecessary UI updates
- ResourceSaver is synchronous - may freeze editor for large resources (acceptable for this use case)

### Memory Management

- Use `QueueFree()` for nodes, not `Free()`
- Disconnect signals before freeing nodes
- EditorFileDialog and EditorResourcePicker are children, will be freed automatically

## Future Enhancements (Out of Scope)

- CSV Import/Export functionality
- Custom properties editor
- Incremental achievement UI
- Achievement validation UI
- Undo/Redo support
- Drag-and-drop list reordering
- Batch operations
- Icon preview/editing
