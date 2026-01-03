#if TOOLS
using System.Collections.Generic;
using Godot.Achievements.Core;

namespace Godot.Achievements.Core.Editor;

/// <summary>
/// Editor component for managing custom properties (Dictionary&lt;string, Variant&gt;) on achievements.
/// Uses Godot's built-in property editors via EditorInspector.InstantiatePropertyEditor.
/// </summary>
[Tool]
public partial class CustomPropertiesEditor : VBoxContainer
{
    private Achievement? _currentAchievement;
    private AchievementDatabase? _database;
    private EditorUndoRedoManager? _undoRedoManager;
    private readonly List<PropertyEntry> _entries = new();
    private Button? _addButton;
    private VBoxContainer? _propertiesContainer;
    private ConfirmationDialog? _removeConfirmDialog;
    private string _pendingRemoveKey = string.Empty;

    [Signal]
    public delegate void PropertyChangedEventHandler();

    /// <summary>
    /// Sets the database reference for propagating property keys across all achievements.
    /// Automatically syncs property keys across all achievements when a database is set.
    /// </summary>
    public void SetDatabase(AchievementDatabase? database)
    {
        _database = database;
        SyncPropertiesAcrossDatabase();
    }

    /// <summary>
    /// Sets the undo/redo manager for editor history support
    /// </summary>
    public void SetUndoRedoManager(EditorUndoRedoManager undoRedoManager)
    {
        _undoRedoManager = undoRedoManager;
    }

    /// <summary>
    /// Synchronizes property keys across all achievements in the database.
    /// Ensures all achievements have all keys that exist in any achievement.
    /// Call this on editor launch. Does not support undo/redo.
    /// </summary>
    public void SyncPropertiesAcrossDatabase()
    {
        if (_database == null) return;

        // Collect all unique keys from all achievements
        var allKeys = new HashSet<string>();
        foreach (var achievement in _database.Achievements)
        {
            foreach (var key in achievement.ExtraProperties.Keys)
            {
                allKeys.Add(key);
            }
        }

        // Ensure every achievement has all keys
        var defaultValue = Variant.From("");
        foreach (var achievement in _database.Achievements)
        {
            foreach (var key in allKeys)
            {
                if (!achievement.ExtraProperties.ContainsKey(key))
                {
                    achievement.ExtraProperties[key] = defaultValue;
                }
            }
        }
    }

    private class PropertyEntry
    {
        public string OriginalKey { get; set; } = string.Empty;
        public VBoxContainer Container { get; set; } = null!;
        public LineEdit KeyEdit { get; set; } = null!;
        public EditorProperty? ValueEditor { get; set; }
        public Button RemoveButton { get; set; } = null!;
        public VariantPropertyHolder Holder { get; set; } = null!;
        public EditorProperty.PropertyChangedEventHandler? PropertyChangedHandler { get; set; }
    }

    public Achievement? CurrentAchievement
    {
        get => _currentAchievement;
        set
        {
            _currentAchievement = value;
            RefreshProperties();
        }
    }

    public override void _Ready()
    {
        // Add note about property scope
        var noteLabel = new Label();
        noteLabel.Text = "Adding, removing, or renaming a property affects all achievements. Values are per-achievement.";
        noteLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.7f, 1f));
        noteLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        AddChild(noteLabel);

        // Create the properties container
        _propertiesContainer = new VBoxContainer();
        _propertiesContainer.AddThemeConstantOverride("separation", 12);
        AddChild(_propertiesContainer);

        // Create add button
        var buttonContainer = new HBoxContainer();
        AddChild(buttonContainer);

        _addButton = new Button();
        _addButton.Text = "+ Add Property";
        _addButton.Pressed += OnAddPropertyPressed;
        buttonContainer.AddChild(_addButton);

        // Create confirmation dialog for removing properties with data
        _removeConfirmDialog = new ConfirmationDialog();
        _removeConfirmDialog.Confirmed += OnRemoveConfirmed;
        AddChild(_removeConfirmDialog);
    }

    public override void _ExitTree()
    {
        if (_addButton != null)
        {
            _addButton.Pressed -= OnAddPropertyPressed;
        }

        if (_removeConfirmDialog != null)
        {
            _removeConfirmDialog.Confirmed -= OnRemoveConfirmed;
            _removeConfirmDialog.QueueFree();
        }

        ClearEntries();
    }

    private void RefreshProperties()
    {
        ClearEntries();

        if (_currentAchievement == null || _propertiesContainer == null)
            return;

        foreach (var kvp in _currentAchievement.ExtraProperties)
        {
            CreatePropertyEntry(kvp.Key, kvp.Value);
        }
    }

    private void ClearEntries()
    {
        foreach (var entry in _entries)
        {
            DisconnectEntry(entry);
            // Remove from parent immediately so UI updates, then queue free
            _propertiesContainer?.RemoveChild(entry.Container);
            entry.Container.QueueFree();
        }
        _entries.Clear();
    }

    private void DisconnectEntry(PropertyEntry entry)
    {
        entry.KeyEdit.FocusExited -= OnKeyFocusExited;
        entry.RemoveButton.Pressed -= OnRemovePressed;
        if (entry.ValueEditor != null && entry.PropertyChangedHandler != null)
        {
            entry.ValueEditor.PropertyChanged -= entry.PropertyChangedHandler;
        }
    }

    private void CreatePropertyEntry(string key, Variant value)
    {
        if (_propertiesContainer == null) return;

        // Create a holder object for the property value
        var holder = new VariantPropertyHolder();
        holder.Value = value;

        var entry = new PropertyEntry
        {
            OriginalKey = key,
            Holder = holder
        };

        // Main container for this property
        entry.Container = new VBoxContainer();
        entry.Container.AddThemeConstantOverride("separation", 4);
        _propertiesContainer.AddChild(entry.Container);

        // Header row with key and remove button
        var headerRow = new HBoxContainer();
        headerRow.AddThemeConstantOverride("separation", 8);
        entry.Container.AddChild(headerRow);

        // Key label
        var keyLabel = new Label { Text = "Key:" };
        keyLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.7f));
        headerRow.AddChild(keyLabel);

        // Key input
        entry.KeyEdit = new LineEdit
        {
            Text = key,
            PlaceholderText = "property_name",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        entry.KeyEdit.FocusExited += OnKeyFocusExited;
        headerRow.AddChild(entry.KeyEdit);

        // Remove button
        entry.RemoveButton = new Button();
        entry.RemoveButton.Text = "X";
        entry.RemoveButton.CustomMinimumSize = new Vector2(30, 0);
        entry.RemoveButton.Pressed += OnRemovePressed;
        headerRow.AddChild(entry.RemoveButton);

        // Value editor row using Godot's built-in property editor
        var valueRow = new HBoxContainer();
        valueRow.AddThemeConstantOverride("separation", 8);
        entry.Container.AddChild(valueRow);

        var valueLabel = new Label { Text = "Value:" };
        valueLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.7f));
        valueRow.AddChild(valueLabel);

        // Use Godot's InstantiatePropertyEditor to get the variant type picker
        var propertyEditor = EditorInspector.InstantiatePropertyEditor(
            holder,
            Variant.Type.Nil,
            VariantPropertyHolder.PropertyName.Value,
            PropertyHint.None,
            "",
            (uint)(PropertyUsageFlags.Default | PropertyUsageFlags.NilIsVariant)
        );

        if (propertyEditor != null)
        {
            propertyEditor.SetObjectAndProperty(holder, VariantPropertyHolder.PropertyName.Value);

            // Set size flags
            propertyEditor.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            propertyEditor.UpdateProperty();
            // Add to tree
            valueRow.AddChild(propertyEditor);

            // Connect to property changed signal - store handler so we can disconnect later
            entry.PropertyChangedHandler = (property, value, field, changing) => OnPropertyEditorChanged(entry, value);
            propertyEditor.PropertyChanged += entry.PropertyChangedHandler;

            entry.ValueEditor = propertyEditor;
        }
        else
        {
            // Fallback if instantiate fails
            var fallbackLabel = new Label { Text = "(Editor not available)" };
            valueRow.AddChild(fallbackLabel);
        }

        // Separator
        var separator = new HSeparator();
        separator.AddThemeConstantOverride("separation", 4);
        entry.Container.AddChild(separator);

        _entries.Add(entry);
    }

    private void OnPropertyEditorChanged(PropertyEntry entry, Variant newValue)
    {
        if (_currentAchievement == null || entry.ValueEditor == null)
            return;

        var key = entry.OriginalKey;
        var oldValue = _currentAchievement.ExtraProperties.TryGetValue(key, out var existing) ? existing : Variant.From("");

        // Skip if value hasn't actually changed
        if (oldValue.Equals(newValue)) return;

        if (_undoRedoManager != null)
        {
            _undoRedoManager.CreateAction("Change Custom Property Value");
            _undoRedoManager.AddDoMethod(this, nameof(DoSetPropertyValue), _currentAchievement, key, newValue);
            _undoRedoManager.AddUndoMethod(this, nameof(DoSetPropertyValue), _currentAchievement, key, oldValue);
            _undoRedoManager.CommitAction();
        }
        else
        {
            DoSetPropertyValue(_currentAchievement, key, newValue);
        }
    }

    private void DoSetPropertyValue(Achievement achievement, string key, Variant value)
    {
        achievement.ExtraProperties[key] = value;

        // Update UI if this is the current achievement
        if (achievement == _currentAchievement)
        {
            foreach (var entry in _entries)
            {
                if (entry.OriginalKey == key)
                {
                    entry.Holder.Value = value;
                    entry.ValueEditor?.CallDeferred(EditorProperty.MethodName.UpdateProperty);
                    break;
                }
            }
        }

        EmitSignal(SignalName.PropertyChanged);
    }

    private void OnAddPropertyPressed()
    {
        if (_currentAchievement == null) return;

        // Generate unique key (check all achievements if database is available)
        var baseKey = "new_property";
        var key = baseKey;
        var counter = 1;

        // Find a key that doesn't exist in any achievement
        while (KeyExistsInAnyAchievement(key))
        {
            key = $"{baseKey}_{counter}";
            counter++;
        }

        if (_undoRedoManager != null && _database != null)
        {
            _undoRedoManager.CreateAction("Add Custom Property");
            _undoRedoManager.AddDoMethod(this, nameof(DoAddProperty), key);
            _undoRedoManager.AddUndoMethod(this, nameof(DoRemoveProperty), key);
            _undoRedoManager.CommitAction();
        }
        else
        {
            DoAddProperty(key);
        }
    }

    private void DoAddProperty(string key)
    {
        var defaultValue = Variant.From("");

        // Propagate the new key to all achievements in the database
        if (_database != null)
        {
            foreach (var achievement in _database.Achievements)
            {
                if (!achievement.ExtraProperties.ContainsKey(key))
                {
                    achievement.ExtraProperties[key] = defaultValue;
                }
            }
        }
        else if (_currentAchievement != null)
        {
            // Fallback: only add to current achievement
            _currentAchievement.ExtraProperties[key] = defaultValue;
        }

        // Add UI entry if viewing the current achievement
        if (_currentAchievement != null && _currentAchievement.ExtraProperties.ContainsKey(key))
        {
            // Check if entry already exists in UI
            bool entryExists = false;
            foreach (var entry in _entries)
            {
                if (entry.OriginalKey == key)
                {
                    entryExists = true;
                    break;
                }
            }
            if (!entryExists)
            {
                CreatePropertyEntry(key, defaultValue);
            }
        }

        EmitSignal(SignalName.PropertyChanged);
    }

    private bool KeyExistsInAnyAchievement(string key)
    {
        if (_database != null)
        {
            foreach (var achievement in _database.Achievements)
            {
                if (achievement.ExtraProperties.ContainsKey(key))
                    return true;
            }
            return false;
        }

        // Fallback: only check current achievement
        return _currentAchievement?.ExtraProperties.ContainsKey(key) ?? false;
    }

    private void OnKeyFocusExited()
    {
        if (_currentAchievement == null) return;

        foreach (var entry in _entries)
        {
            var newKey = entry.KeyEdit.Text;
            var oldKey = entry.OriginalKey;

            if (newKey == oldKey) continue;

            // Validate new key
            if (string.IsNullOrWhiteSpace(newKey))
            {
                entry.KeyEdit.Text = oldKey;
                continue;
            }

            // Check for duplicates across all achievements
            if (KeyExistsInAnyAchievement(newKey))
            {
                entry.KeyEdit.Text = oldKey;
                AchievementLogger.Warning(AchievementLogger.Areas.Editor, $"Property key '{newKey}' already exists");
                continue;
            }

            // Rename the property across all achievements
            RenamePropertyInAll(oldKey, newKey, entry);
        }
    }

    private void RenamePropertyInAll(string oldKey, string newKey, PropertyEntry entry)
    {
        if (_database == null)
        {
            // Fallback: only rename in current achievement (no undo support without database)
            if (_currentAchievement != null && _currentAchievement.ExtraProperties.ContainsKey(oldKey))
            {
                var value = _currentAchievement.ExtraProperties[oldKey];
                _currentAchievement.ExtraProperties.Remove(oldKey);
                _currentAchievement.ExtraProperties[newKey] = value;
                UpdateEntryKey(entry, newKey);
                EmitSignal(SignalName.PropertyChanged);
            }
            return;
        }

        if (_undoRedoManager != null)
        {
            _undoRedoManager.CreateAction("Rename Custom Property");
            _undoRedoManager.AddDoMethod(this, nameof(DoRenameProperty), oldKey, newKey);
            _undoRedoManager.AddUndoMethod(this, nameof(DoRenameProperty), newKey, oldKey);
            _undoRedoManager.CommitAction();
        }
        else
        {
            DoRenameProperty(oldKey, newKey);
        }

        // Update UI entry meta
        UpdateEntryKey(entry, newKey);
    }

    private void DoRenameProperty(string oldKey, string newKey)
    {
        if (_database == null) return;

        foreach (var achievement in _database.Achievements)
        {
            if (achievement.ExtraProperties.ContainsKey(oldKey))
            {
                var value = achievement.ExtraProperties[oldKey];
                achievement.ExtraProperties.Remove(oldKey);
                achievement.ExtraProperties[newKey] = value;
            }
        }

        // Update UI entries to reflect the new key
        foreach (var entry in _entries)
        {
            if (entry.OriginalKey == oldKey)
            {
                UpdateEntryKey(entry, newKey);
                break;
            }
        }

        EmitSignal(SignalName.PropertyChanged);
    }

    private void UpdateEntryKey(PropertyEntry entry, string newKey)
    {
        entry.OriginalKey = newKey;
        entry.KeyEdit.Text = newKey;
    }

    private void OnRemovePressed()
    {
        if (_currentAchievement == null) return;

        // Find which entry's remove button was pressed
        for (int i = _entries.Count - 1; i >= 0; i--)
        {
            var entry = _entries[i];
            if (entry.RemoveButton.ButtonPressed)
            {
                var key = entry.OriginalKey;

                // Check if other achievements have non-empty values for this key
                var achievementsWithData = CountAchievementsWithNonEmptyValue(key);

                if (achievementsWithData > 0 && _removeConfirmDialog != null)
                {
                    // Show confirmation dialog
                    _pendingRemoveKey = key;
                    var plural = achievementsWithData == 1 ? "achievement has" : "achievements have";
                    _removeConfirmDialog.DialogText = $"Remove property '{key}' from all achievements?\n\n{achievementsWithData} other {plural} non-empty values that will be deleted.";
                    _removeConfirmDialog.PopupCentered();
                }
                else
                {
                    // No data loss, remove directly
                    RemovePropertyFromAll(key);
                }
                return;
            }
        }
    }

    private void OnRemoveConfirmed()
    {
        if (!string.IsNullOrEmpty(_pendingRemoveKey))
        {
            RemovePropertyFromAll(_pendingRemoveKey);
            _pendingRemoveKey = string.Empty;
        }
    }

    private int CountAchievementsWithNonEmptyValue(string key)
    {
        if (_database == null) return 0;

        var count = 0;
        foreach (var achievement in _database.Achievements)
        {
            // Skip the current achievement
            if (achievement == _currentAchievement) continue;

            if (achievement.ExtraProperties.TryGetValue(key, out var value))
            {
                // Check if value is non-empty (not null, not empty string)
                if (value.VariantType != Variant.Type.Nil &&
                    !(value.VariantType == Variant.Type.String && string.IsNullOrEmpty(value.AsString())))
                {
                    count++;
                }
            }
        }
        return count;
    }

    private void RemovePropertyFromAll(string key)
    {
        if (_database == null)
        {
            // Fallback: only remove from current (no undo support without database)
            _currentAchievement?.ExtraProperties.Remove(key);
            RemoveEntryUI(key);
            EmitSignal(SignalName.PropertyChanged);
            return;
        }

        // Collect all values for undo
        var savedValues = new Godot.Collections.Dictionary<Achievement, Variant>();
        foreach (var achievement in _database.Achievements)
        {
            if (achievement.ExtraProperties.TryGetValue(key, out var value))
            {
                savedValues[achievement] = value;
            }
        }

        if (_undoRedoManager != null)
        {
            _undoRedoManager.CreateAction("Remove Custom Property");
            _undoRedoManager.AddDoMethod(this, nameof(DoRemoveProperty), key);
            _undoRedoManager.AddUndoMethod(this, nameof(UndoRemoveProperty), key, savedValues);
            _undoRedoManager.CommitAction();
        }
        else
        {
            DoRemoveProperty(key);
        }
    }

    private void DoRemoveProperty(string key)
    {
        if (_database != null)
        {
            foreach (var achievement in _database.Achievements)
            {
                achievement.ExtraProperties.Remove(key);
            }
        }

        RemoveEntryUI(key);
        EmitSignal(SignalName.PropertyChanged);
    }

    private void UndoRemoveProperty(string key, Godot.Collections.Dictionary<Achievement, Variant> savedValues)
    {
        // Restore all values
        foreach (var kvp in savedValues)
        {
            kvp.Key.ExtraProperties[key] = kvp.Value;
        }

        // Refresh UI if this is the current achievement
        RefreshProperties();
        EmitSignal(SignalName.PropertyChanged);
    }

    private void RemoveEntryUI(string key)
    {
        // Clean up UI for this entry
        for (int i = _entries.Count - 1; i >= 0; i--)
        {
            var entry = _entries[i];
            if (entry.OriginalKey == key)
            {
                DisconnectEntry(entry);
                _propertiesContainer?.RemoveChild(entry.Container);
                entry.Container.QueueFree();
                _entries.RemoveAt(i);
                break;
            }
        }
    }
}
#endif
