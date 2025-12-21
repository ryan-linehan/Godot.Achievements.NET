# AI Agent Implementation Guide

This document provides comprehensive guidelines for AI agents implementing the Godot.Achievements.NET system. Follow these standards to ensure consistency, maintainability, and proper Godot integration.

## 📋 Table of Contents

- [Naming Conventions](#naming-conventions)
- [Godot-Specific Patterns](#godot-specific-patterns)
- [Memory Management](#memory-management)
- [Signal Handling](#signal-handling)
- [Serialization & Data Storage](#serialization--data-storage)
- [Initialization Patterns](#initialization-patterns)
- [AOT Compatibility](#aot-compatibility)
- [Error Handling](#error-handling)
- [Performance Considerations](#performance-considerations)
- [Testing Guidelines](#testing-guidelines)

---

## 🏷️ Naming Conventions

### Follow Microsoft C# Conventions

**Classes, Methods, Properties:**
```csharp
// ✅ GOOD - PascalCase
public class AchievementManager : Node
{
    public void UnlockAchievement(string achievementId) { }
    public bool IsUnlocked { get; set; }
}

// ❌ BAD
public class achievement_manager { }  // Wrong casing
public void unlock_achievement() { }  // Wrong casing
```

**Private Fields:**
```csharp
// ✅ GOOD - _camelCase with underscore prefix
private AchievementDatabase _database;
private List<IAchievementProvider> _platformProviders;

// ❌ BAD
private AchievementDatabase m_database;  // Hungarian notation
private AchievementDatabase database;    // Missing underscore
```

**Local Variables and Parameters:**
```csharp
// ✅ GOOD - camelCase
public void ProcessAchievement(string achievementId)
{
    var localProvider = GetLocalProvider();
    bool isAvailable = CheckAvailability();
}

// ❌ BAD
public void ProcessAchievement(string AchievementId)  // Wrong casing
{
    var LocalProvider = GetLocalProvider();  // Wrong casing
}
```

**Constants:**
```csharp
// ✅ GOOD - PascalCase (C# convention)
private const string SavePath = "user://achievements.json";
private const int MaxRetryAttempts = 5;

// ❌ BAD
private const string SAVE_PATH = "user://achievements.json";  // C++ style
```

### Godot-Specific Naming

**Signals:**
```csharp
// ✅ GOOD - PascalCase with EventHandler suffix
[Signal]
public delegate void AchievementUnlockedEventHandler(string achievementId);

[Signal]
public delegate void ProgressChangedEventHandler(string achievementId, float progress);

// ❌ BAD
[Signal]
public delegate void achievement_unlocked(string id);  // Wrong casing
```

**Node Paths:**
```csharp
// ✅ GOOD - Use absolute paths for autoloads
var manager = GetNode<AchievementManager>("/root/Achievements");

// ⚠️ CAUTION - Relative paths are fragile
var manager = GetNode<AchievementManager>("../AchievementManager");
```

---

## 🎮 Godot-Specific Patterns

### Use StringName for Performance

**Why:** `StringName` is interned and optimized for Godot's internal systems, especially signals and node paths.

```csharp
// ✅ GOOD - Use StringName for signals, node names, groups
public partial class AchievementManager : Node
{
    [Signal]
    public delegate void AchievementUnlockedEventHandler(string achievementId);

    public void EmitUnlock(string achievementId)
    {
        EmitSignal(SignalName.AchievementUnlocked, achievementId);  // Uses StringName
    }

    public void AddToGroup()
    {
        AddToGroup(new StringName("achievement_listeners"));
    }
}

// ❌ BAD - Regular strings for repeated lookups
EmitSignal("AchievementUnlocked", achievementId);  // String allocation every call
```

**Cache StringNames for frequently used values:**
```csharp
// ✅ GOOD - Cache if used very frequently (rare case)
private static readonly StringName GroupAchievements = new("achievement_listeners");

public void AddToGroup()
{
    AddToGroup(GroupAchievements);
}
```

### Prefer Godot Collections

```csharp
// ✅ GOOD - Use Godot collections for serialization
private Godot.Collections.Dictionary<string, Godot.Collections.Dictionary> _savedData;
private Godot.Collections.Array<Achievement> _achievements;

// ❌ BAD - C# collections don't serialize well with Godot
private Dictionary<string, AchievementData> _savedData;  // Won't work with Json.Stringify
private List<Achievement> _achievements;  // Won't work with [Export]
```

**When to use each:**
- **Godot.Collections.Dictionary/Array**: Serialization, exports, Godot API integration
- **System.Collections.Generic**: Internal logic, temporary data structures

### Use FileAccess, Not System.IO

```csharp
// ✅ GOOD - Use Godot's FileAccess
private void SaveToDisk()
{
    using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write);
    file.StoreString(jsonString);
}

private void LoadFromDisk()
{
    if (!FileAccess.FileExists(SavePath))
        return;

    using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Read);
    var content = file.GetAsText();
}

// ❌ BAD - Don't use System.IO
using var stream = File.OpenWrite("user://achievements.json");  // Wrong!
```

**Why:**
- Godot's `FileAccess` handles `user://` and `res://` paths correctly
- Works consistently across platforms
- Proper permission handling

---

## 🧹 Memory Management

### Always Dispose Unmanaged Resources

```csharp
// ✅ GOOD - Properly dispose FileAccess
private void SaveData()
{
    using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write);
    file.StoreString(data);
}  // Automatically disposed

// ❌ BAD - Resource leak
private void SaveData()
{
    var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write);
    file.StoreString(data);
    // File never disposed!
}
```

### QueueFree() for Nodes

```csharp
// ✅ GOOD - Use QueueFree() for nodes
private void ClearAchievementList()
{
    foreach (var child in _achievementList.GetChildren())
    {
        child.QueueFree();
    }
}

// ❌ BAD - Don't use Free() unless you know what you're doing
foreach (var child in _achievementList.GetChildren())
{
    child.Free();  // Can cause issues if node is in scene tree
}
```

### Avoid Memory Leaks with Lambdas

```csharp
// ⚠️ CAUTION - Lambda can create reference cycles
public void SetupButton()
{
    _button.Pressed += () => OnButtonPressed();  // 'this' is captured
}

// ✅ BETTER - Use method group or be aware of cleanup
public void SetupButton()
{
    _button.Pressed += OnButtonPressed;  // No closure
}

public override void _ExitTree()
{
    _button.Pressed -= OnButtonPressed;  // Clean up
}
```

---

## 📡 Signal Handling

### ALWAYS Disconnect Signals

**Why:** Godot signals don't automatically disconnect, causing memory leaks and ghost callbacks.

```csharp
// ✅ GOOD - Proper signal lifecycle
public partial class AchievementToast : Control
{
    private Timer _timer;

    public override void _Ready()
    {
        _timer = GetNode<Timer>("Timer");
        _timer.Timeout += OnTimeout;
    }

    public override void _ExitTree()
    {
        // CRITICAL: Disconnect all signals
        if (_timer != null)
        {
            _timer.Timeout -= OnTimeout;
        }
    }

    private void OnTimeout()
    {
        QueueFree();
    }
}

// ❌ BAD - Signal leak
public override void _Ready()
{
    _timer.Timeout += OnTimeout;
    // Never disconnected!
}
```

### Use Signals Over Direct Calls for Decoupling

```csharp
// ✅ GOOD - Loose coupling via signals
public partial class AchievementManager : Node
{
    [Signal]
    public delegate void AchievementUnlockedEventHandler(string achievementId);

    public async Task Unlock(string achievementId)
    {
        // Unlock logic...
        EmitSignal(SignalName.AchievementUnlocked, achievementId);
    }
}

// Other systems listen
public override void _Ready()
{
    var manager = GetNode<AchievementManager>("/root/Achievements");
    manager.AchievementUnlocked += OnAchievementUnlocked;
}

// ❌ BAD - Tight coupling
public class AchievementManager
{
    private ToastManager _toastManager;  // Direct dependency

    public void Unlock(string id)
    {
        _toastManager.ShowToast(id);  // Tightly coupled
    }
}
```

### OneShot Connections for Single-Use Handlers

```csharp
// ✅ GOOD - Use lambda with manual disconnect for one-time events
private async Task WaitForUnlock()
{
    var tcs = new TaskCompletionSource<bool>();

    EventHandler handler = null;
    handler = (achievementId) =>
    {
        AchievementUnlocked -= handler;  // Auto-disconnect
        tcs.SetResult(true);
    };

    AchievementUnlocked += handler;
    await tcs.Task;
}
```

---

## 💾 Serialization & Data Storage

### Use Godot's Json Class (NOT System.Text.Json)

**Why:** AOT compatibility + Godot Variant system integration

```csharp
// ✅ GOOD - Godot's Json for AOT compatibility
private void SaveToDisk()
{
    var jsonString = Json.Stringify(_savedData, "\t");  // Pretty print
    using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write);
    file.StoreString(jsonString);
}

private void LoadFromDisk()
{
    using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Read);
    var jsonString = file.GetAsText();

    var json = new Json();
    var error = json.Parse(jsonString);

    if (error != Error.Ok)
    {
        GD.PushError($"JSON parse error: {json.GetErrorMessage()}");
        return;
    }

    _savedData = json.Data.AsGodotDictionary<string, Godot.Collections.Dictionary>();
}

// ❌ BAD - System.Text.Json breaks AOT
var json = JsonSerializer.Serialize(_data);  // Not AOT-safe!
var data = JsonSerializer.Deserialize<MyClass>(json);  // Reflection!
```

### Store DateTime as ISO 8601 Strings

```csharp
// ✅ GOOD - Store as string for JSON compatibility
_savedData[achievementId] = new Godot.Collections.Dictionary
{
    { "IsUnlocked", true },
    { "UnlockedAt", DateTime.UtcNow.ToString("O") },  // ISO 8601
    { "Progress", 1.0f }
};

// Parse back
if (_savedData[id]["UnlockedAt"] is string dateStr && !string.IsNullOrEmpty(dateStr))
{
    var unlockedAt = DateTime.Parse(dateStr, null, DateTimeStyles.RoundtripKind);
}

// ❌ BAD - Can't serialize DateTime directly
_savedData[id] = new Dictionary
{
    { "UnlockedAt", DateTime.UtcNow }  // Won't serialize properly
};
```

### User Data vs Resource Data

```csharp
// ✅ GOOD - Understand the difference

// Resources (version controlled, readonly at runtime)
// res://achievements.tres - Achievement definitions
public partial class AchievementDatabase : Resource
{
    [Export] public Godot.Collections.Array<Achievement> Achievements { get; set; }
}

// User data (runtime state, saved locally)
// user://achievements.json - Player progress
private Godot.Collections.Dictionary<string, Godot.Collections.Dictionary> _savedData;
```

---

## ⚙️ Initialization Patterns

### Prefer [Export] Over _Ready() Initialization

**Why:**
- Configurable in editor
- No null checks needed
- Clear dependencies

```csharp
// ✅ GOOD - Export for configuration
public partial class AchievementToast : PanelContainer
{
    [Export] public float DisplayDuration { get; set; } = 3.0f;
    [Export] public Vector2 SlideDirection { get; set; } = Vector2.Down;
    [Export] public AudioStream UnlockSound { get; set; }
    [Export] public PackedScene ToastTemplate { get; set; }

    // No _Ready() needed for these values!
}

// ❌ BAD - Hardcoded in _Ready()
public partial class AchievementToast : PanelContainer
{
    private float _displayDuration;

    public override void _Ready()
    {
        _displayDuration = 3.0f;  // Not configurable!
    }
}
```

### Use [Export] with Good Defaults

```csharp
// ✅ GOOD - Sensible defaults
[Export] public float RetryInterval { get; set; } = 30.0f;
[Export] public int MaxRetryAttempts { get; set; } = 10;
[Export] public bool ShowToasts { get; set; } = true;

// User can override in editor if needed
```

### GetNode in _Ready(), Not Constructor

```csharp
// ✅ GOOD - GetNode in _Ready()
public partial class AchievementManager : Node
{
    private LocalAchievementProvider _localProvider;

    public override void _Ready()
    {
        _localProvider = new LocalAchievementProvider();

        var database = GD.Load<AchievementDatabase>("res://achievements.tres");
        // ... initialization
    }
}

// ❌ BAD - GetNode in constructor
public AchievementManager()
{
    var node = GetNode("SomeNode");  // Scene tree not ready!
}
```

### Initialization Order

**Godot lifecycle order:**
```
1. Constructor
2. _EnterTree()  ← Scene tree available, but not fully ready
3. _Ready()      ← Safe to access children, call GetNode
4. _Process()    ← First frame
```

```csharp
// ✅ GOOD - Use correct lifecycle methods
public override void _EnterTree()
{
    // Register with systems, add to groups
    AddToGroup("achievement_listeners");
}

public override void _Ready()
{
    // GetNode, load resources, connect signals
    _timer = GetNode<Timer>("Timer");
    _timer.Timeout += OnTimeout;
}

public override void _ExitTree()
{
    // Clean up signals, save state
    _timer.Timeout -= OnTimeout;
}
```

---

## 🔒 AOT Compatibility

### No Reflection, Ever

```csharp
// ✅ GOOD - Direct type usage
public void RegisterProvider(IAchievementProvider provider)
{
    _providers.Add(provider);
}

// ❌ BAD - Reflection breaks AOT
var type = Type.GetType("SteamAchievementProvider");
var instance = Activator.CreateInstance(type);  // NOT AOT-safe!
```

### Use Godot Collections for Serialization

```csharp
// ✅ GOOD - Godot.Collections work with Json
private Godot.Collections.Dictionary _data;

// ❌ BAD - Generic types need JsonSerializer (reflection)
private Dictionary<string, MyClass> _data;
```

### Conditional Compilation for Platform Code

```csharp
// ✅ GOOD - Platform-specific code
#if GODOT_PC || GODOT_WINDOWS
public class SteamAchievementProvider : IAchievementProvider
{
    public bool IsAvailable => SteamAPI.IsSteamRunning();
}
#endif

// ❌ BAD - Runtime platform checks for compilation
if (OS.GetName() == "Windows")  // Still compiles unused code
{
    // Steam logic
}
```

---

## ⚠️ Error Handling

### Use GD.PushError/PushWarning

```csharp
// ✅ GOOD - Godot's logging
if (error != Error.Ok)
{
    GD.PushError($"Failed to load achievements: {error}");
    return;
}

// For warnings
if (string.IsNullOrEmpty(achievement.SteamId))
{
    GD.PushWarning($"Achievement '{achievement.Id}' missing Steam ID");
}

// For debug info
GD.Print($"[Achievements] Loaded {count} achievements");

// ❌ BAD - Don't use Console or Debug
Console.WriteLine("Error!");  // Doesn't show in Godot editor
Debug.WriteLine("Error!");    // Wrong framework
```

### Return Error Codes, Not Exceptions for Expected Failures

```csharp
// ✅ GOOD - Return success/failure
public async Task<AchievementUnlockResult> UnlockAchievement(string id)
{
    if (!IsAvailable)
    {
        return new AchievementUnlockResult
        {
            Success = false,
            Error = "Provider not available"
        };
    }

    // ... unlock logic

    return new AchievementUnlockResult { Success = true };
}

// ❌ BAD - Exceptions for flow control
public void UnlockAchievement(string id)
{
    if (!IsAvailable)
        throw new InvalidOperationException("Provider not available");
}
```

### Let Unexpected Exceptions Bubble

```csharp
// ✅ GOOD - Catch at boundaries, let unexpected errors bubble
private async void OnUnlockButtonPressed()
{
    try
    {
        await AchievementManager.Instance.Unlock(_achievementId);
    }
    catch (Exception ex)
    {
        GD.PushError($"Unexpected error unlocking achievement: {ex}");
        // Show error to user
    }
}
```

---

## 🚀 Performance Considerations

### Cache GetNode Results

```csharp
// ✅ GOOD - Cache node references
private Timer _timer;
private Label _titleLabel;

public override void _Ready()
{
    _timer = GetNode<Timer>("Timer");
    _titleLabel = GetNode<Label>("VBox/Title");
}

public override void _Process(double delta)
{
    _titleLabel.Text = $"Score: {_score}";  // Use cached reference
}

// ❌ BAD - Repeated GetNode calls
public override void _Process(double delta)
{
    GetNode<Label>("VBox/Title").Text = $"Score: {_score}";  // Slow!
}
```

### Avoid Allocations in _Process()

```csharp
// ✅ GOOD - Reuse objects
private readonly List<Achievement> _tempList = new();

public void UpdateAchievements()
{
    _tempList.Clear();
    // Use _tempList for temporary work
}

// ❌ BAD - Allocations every frame
public override void _Process(double delta)
{
    var list = new List<Achievement>();  // GC pressure!
}
```

### Use Object Pooling for Frequent Instantiation

```csharp
// ✅ GOOD - Pool toasts
private Queue<AchievementToast> _toastPool = new();

public AchievementToast GetToast()
{
    if (_toastPool.Count > 0)
        return _toastPool.Dequeue();

    return ToastScene.Instantiate<AchievementToast>();
}

public void ReturnToast(AchievementToast toast)
{
    toast.Hide();
    _toastPool.Enqueue(toast);
}
```

---

## 🧪 Testing Guidelines

### Write Unit Tests for Logic

```csharp
// ✅ GOOD - Testable logic separated from Godot nodes
public class AchievementValidator
{
    public bool IsValidId(string id)
    {
        return !string.IsNullOrEmpty(id) && id.Length <= 64;
    }
}

// Easy to unit test
[Test]
public void IsValidId_ReturnsTrue_ForValidId()
{
    var validator = new AchievementValidator();
    Assert.IsTrue(validator.IsValidId("boss_defeated"));
}
```

### Use Godot's Test Framework for Integration Tests

```csharp
// Integration test in Godot
public partial class AchievementManagerTests : Node
{
    public override async void _Ready()
    {
        var manager = GetNode<AchievementManager>("/root/Achievements");

        // Test unlock
        await manager.Unlock("test_achievement");

        // Verify
        var achievement = manager.GetAchievement("test_achievement");
        GD.Print($"Test: Achievement unlocked = {achievement.IsUnlocked}");
    }
}
```

---

## ✅ Pre-Implementation Checklist

Before writing code, verify:

- [ ] Using `StringName` for signals and frequently accessed node names
- [ ] All signal connections have corresponding disconnections in `_ExitTree()`
- [ ] Using `Godot.Collections.Dictionary/Array` for serialization
- [ ] Using `FileAccess`, not `System.IO`
- [ ] Using `Godot.Json`, not `System.Text.Json`
- [ ] Using `[Export]` for configurable values instead of hardcoding in `_Ready()`
- [ ] Following Microsoft C# naming conventions (PascalCase, _camelCase)
- [ ] No reflection or `Activator.CreateInstance()` (AOT compatibility)
- [ ] Error handling uses `GD.PushError/PushWarning`
- [ ] Node references cached in `_Ready()`, not called repeatedly
- [ ] Proper `using` statements for `FileAccess` and other disposables
- [ ] Conditional compilation for platform-specific code

---

## 📚 Quick Reference

### Common Patterns

```csharp
// Signal definition and emission
[Signal]
public delegate void MyEventEventHandler(string arg);

EmitSignal(SignalName.MyEvent, arg);

// Signal connection and cleanup
public override void _Ready()
{
    someNode.SomeSignal += OnSomeSignal;
}

public override void _ExitTree()
{
    someNode.SomeSignal -= OnSomeSignal;
}

// File I/O
using var file = FileAccess.Open("user://data.json", FileAccess.ModeFlags.Write);
file.StoreString(jsonString);

// JSON serialization
var jsonString = Json.Stringify(godotDictionary, "\t");
var json = new Json();
if (json.Parse(jsonString) == Error.Ok)
{
    var data = json.Data.AsGodotDictionary();
}

// Node cleanup
foreach (var child in container.GetChildren())
    child.QueueFree();

// Conditional compilation
#if GODOT_PC || GODOT_WINDOWS
// Windows-specific code
#endif
```

---

## 🎯 Summary

**Remember the core principles:**

1. **Use Godot APIs** - FileAccess, Json, Collections over .NET equivalents
2. **Manage memory** - Disconnect signals, dispose resources, cache node references
3. **Think AOT** - No reflection, concrete types, Godot collections
4. **Export configuration** - Use [Export] for values that might change
5. **Follow conventions** - Microsoft C# naming + Godot patterns

When in doubt, check the official Godot C# documentation and existing Godot plugins for reference.
