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
    private readonly List<PropertyEntry> _entries = new();
    private Button? _addButton;
    private VBoxContainer? _propertiesContainer;

    [Signal]
    public delegate void PropertyChangedEventHandler();

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
    }

    public override void _ExitTree()
    {
        if (_addButton != null)
        {
            _addButton.Pressed -= OnAddPropertyPressed;
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
        entry.KeyEdit.SetMeta("entry_key", key);
        headerRow.AddChild(entry.KeyEdit);

        // Remove button
        entry.RemoveButton = new Button();
        entry.RemoveButton.Text = "X";
        entry.RemoveButton.CustomMinimumSize = new Vector2(30, 0);
        entry.RemoveButton.Pressed += OnRemovePressed;
        entry.RemoveButton.SetMeta("entry_key", key);
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
            propertyEditor.SetMeta("entry_key", key);
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

        // Update holder with the new value from the signal
        entry.Holder.Value = newValue;

        // Sync the value to achievement
        _currentAchievement.ExtraProperties[entry.OriginalKey] = newValue;

        // Defer UpdateProperty to avoid freeing objects during signal emission
        entry.ValueEditor.CallDeferred(EditorProperty.MethodName.UpdateProperty);

        EmitSignal(SignalName.PropertyChanged);
    }

    private void OnAddPropertyPressed()
    {
        if (_currentAchievement == null) return;

        // Generate unique key
        var baseKey = "new_property";
        var key = baseKey;
        var counter = 1;
        while (_currentAchievement.ExtraProperties.ContainsKey(key))
        {
            key = $"{baseKey}_{counter}";
            counter++;
        }

        var defaultValue = Variant.From("");
        _currentAchievement.ExtraProperties[key] = defaultValue;
        CreatePropertyEntry(key, defaultValue);
        EmitSignal(SignalName.PropertyChanged);
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

            // Check for duplicates
            if (_currentAchievement.ExtraProperties.ContainsKey(newKey))
            {
                entry.KeyEdit.Text = oldKey;
                AchievementLogger.Warning(AchievementLogger.Areas.Editor, $"Property key '{newKey}' already exists");
                continue;
            }

            // Rename the property
            if (_currentAchievement.ExtraProperties.ContainsKey(oldKey))
            {
                var value = _currentAchievement.ExtraProperties[oldKey];
                _currentAchievement.ExtraProperties.Remove(oldKey);
                _currentAchievement.ExtraProperties[newKey] = value;

                // Update meta on controls
                entry.OriginalKey = newKey;
                entry.KeyEdit.SetMeta("entry_key", newKey);
                entry.RemoveButton.SetMeta("entry_key", newKey);
                if (entry.ValueEditor != null)
                    entry.ValueEditor.SetMeta("entry_key", newKey);

                EmitSignal(SignalName.PropertyChanged);
            }
        }
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

                // Remove from achievement
                _currentAchievement.ExtraProperties.Remove(key);

                // Clean up UI
                DisconnectEntry(entry);
                entry.Container.QueueFree();
                _entries.RemoveAt(i);

                EmitSignal(SignalName.PropertyChanged);
                return;
            }
        }
    }
}
#endif
