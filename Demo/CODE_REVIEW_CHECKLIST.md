# Code Review Checklist

Use this checklist when reviewing code for the Godot.Achievements.NET project.

## 🔍 General Code Quality

### Naming Conventions
- [ ] Classes, methods, properties use PascalCase
- [ ] Private fields use _camelCase with underscore prefix
- [ ] Local variables and parameters use camelCase
- [ ] Constants use PascalCase (not SCREAMING_SNAKE_CASE)
- [ ] Signal delegates end with `EventHandler`
- [ ] No Hungarian notation (m_, p_, etc.)

### Code Organization
- [ ] One class per file
- [ ] File name matches class name
- [ ] Namespace matches folder structure
- [ ] Logical grouping of related methods
- [ ] Public members before private
- [ ] Fields at top of class

---

## 🎮 Godot-Specific

### Node Lifecycle
- [ ] `GetNode()` calls only in `_Ready()` or later
- [ ] Node references cached, not called repeatedly
- [ ] `_ExitTree()` implemented for cleanup
- [ ] No heavy work in constructors
- [ ] Proper lifecycle method usage:
  - `_EnterTree()` for scene tree registration
  - `_Ready()` for initialization
  - `_ExitTree()` for cleanup

### Signals
- [ ] All signal connections have corresponding disconnections
- [ ] Signals disconnected in `_ExitTree()`
- [ ] Signal parameters use proper types (no `Variant` unless necessary)
- [ ] `SignalName.X` used instead of string literals
- [ ] Signals named as events (past tense or state change)

```csharp
// ✅ GOOD
public override void _Ready()
{
    _button.Pressed += OnButtonPressed;
}

public override void _ExitTree()
{
    _button.Pressed -= OnButtonPressed;  // ✅ Disconnected
}

// ❌ BAD
public override void _Ready()
{
    _button.Pressed += OnButtonPressed;
    // ❌ Never disconnected - memory leak!
}
```

### Exports and Configuration
- [ ] Configurable values use `[Export]` instead of hardcoding
- [ ] Exports have sensible defaults
- [ ] Export hints used where appropriate (ranges, files, etc.)
- [ ] Exports grouped logically with blank lines
- [ ] Export tooltips provided for unclear properties

```csharp
// ✅ GOOD
[Export] public float DisplayDuration { get; set; } = 3.0f;
[Export] public AudioStream UnlockSound { get; set; }

// ❌ BAD
private float displayDuration = 3.0f;  // Not configurable in editor
```

### Resources
- [ ] Resources used for data definitions (not Nodes)
- [ ] `[GlobalClass]` attribute on custom resources
- [ ] Resources marked `partial` if extending in C#
- [ ] Resources saved in `res://` for version control

### StringName Usage
- [ ] `StringName` used for signal names
- [ ] `StringName` used for frequently accessed node paths
- [ ] `StringName` used for group names
- [ ] Cached if used very frequently (rare)

```csharp
// ✅ GOOD
EmitSignal(SignalName.AchievementUnlocked, id);

// ❌ BAD
EmitSignal("AchievementUnlocked", id);  // String allocation
```

---

## 💾 Data & Serialization

### JSON Handling
- [ ] Uses `Godot.Json` class, not `System.Text.Json`
- [ ] Proper error checking with `Error.Ok`
- [ ] Uses `Godot.Collections.Dictionary/Array` for serialization
- [ ] DateTime stored as ISO 8601 strings
- [ ] Pretty printing enabled for debugging (`Json.Stringify(data, "\t")`)

```csharp
// ✅ GOOD
var json = new Json();
var error = json.Parse(jsonString);
if (error != Error.Ok)
{
    GD.PushError($"Parse error: {json.GetErrorMessage()}");
    return;
}

// ❌ BAD
var data = JsonSerializer.Deserialize<MyClass>(json);  // Not AOT-safe!
```

### File I/O
- [ ] Uses `FileAccess`, not `System.IO`
- [ ] `using` statements for file handles
- [ ] Checks `FileAccess.FileExists()` before reading
- [ ] Error handling for file operations
- [ ] Uses `user://` or `res://` paths (not absolute paths)

```csharp
// ✅ GOOD
using var file = FileAccess.Open("user://save.json", FileAccess.ModeFlags.Write);
file.StoreString(jsonString);

// ❌ BAD
var file = FileAccess.Open("user://save.json", FileAccess.ModeFlags.Write);
file.StoreString(jsonString);
// ❌ Never disposed - resource leak!
```

### Collections
- [ ] `Godot.Collections.Dictionary/Array` for Godot API/serialization
- [ ] `System.Collections.Generic` for internal logic
- [ ] Collections initialized before use
- [ ] Collections cleared when reused (not recreated)

---

## 🔒 AOT Compatibility

### No Reflection
- [ ] No `Type.GetType()` or `typeof()` for instantiation
- [ ] No `Activator.CreateInstance()`
- [ ] No `Assembly.Load()` or dynamic loading
- [ ] No `MethodInfo.Invoke()`
- [ ] Uses concrete types, not dynamic types

```csharp
// ✅ GOOD
manager.RegisterProvider(new SteamAchievementProvider());

// ❌ BAD
var type = Type.GetType("SteamAchievementProvider");
var instance = Activator.CreateInstance(type);  // Reflection!
```

### Platform Compilation
- [ ] Platform-specific code wrapped in `#if` directives
- [ ] Correct platform symbols used (`GODOT_PC`, `GODOT_IOS`, etc.)
- [ ] Code compiles for all target platforms
- [ ] Unused platforms don't bloat binaries

```csharp
// ✅ GOOD
#if GODOT_PC || GODOT_WINDOWS
public class SteamProvider : IAchievementProvider { }
#endif
```

---

## 🧹 Memory Management

### Resource Disposal
- [ ] `using` statements for `IDisposable` objects
- [ ] `FileAccess` properly disposed
- [ ] Textures/Resources freed when no longer needed
- [ ] Large objects nulled when done

### Node Management
- [ ] `QueueFree()` used for nodes (not `Free()`)
- [ ] Parent nodes removed before freeing
- [ ] No orphaned nodes in scene tree
- [ ] Children freed before parent

```csharp
// ✅ GOOD
foreach (var child in container.GetChildren())
{
    child.QueueFree();
}

// ❌ BAD
foreach (var child in container.GetChildren())
{
    child.Free();  // Can cause issues if in tree
}
```

### Lambda Capture
- [ ] Aware of closure captures (`this` captured in lambdas)
- [ ] Event handlers properly unsubscribed
- [ ] No circular references via lambdas

---

## ⚡ Performance

### Caching
- [ ] GetNode results cached in `_Ready()`
- [ ] Resource loads cached (not loaded repeatedly)
- [ ] Expensive calculations cached
- [ ] No allocations in `_Process()` or `_PhysicsProcess()`

```csharp
// ✅ GOOD
private Label _scoreLabel;

public override void _Ready()
{
    _scoreLabel = GetNode<Label>("Score");
}

public override void _Process(double delta)
{
    _scoreLabel.Text = $"Score: {_score}";  // Cached reference
}

// ❌ BAD
public override void _Process(double delta)
{
    GetNode<Label>("Score").Text = $"Score: {_score}";  // Slow!
}
```

### Allocations
- [ ] No unnecessary allocations in hot paths
- [ ] String interpolation avoided in loops
- [ ] Collections reused, not recreated
- [ ] Object pooling for frequent instantiation

### Async/Await
- [ ] `ConfigureAwait(false)` used where appropriate
- [ ] No blocking calls (`.Result`, `.Wait()`)
- [ ] Cancellation tokens used for long operations
- [ ] Tasks properly awaited

---

## ⚠️ Error Handling

### Logging
- [ ] Uses `GD.Print()`, `GD.PushWarning()`, `GD.PushError()`
- [ ] Not using `Console.WriteLine()` or `Debug.WriteLine()`
- [ ] Appropriate log levels (error vs warning vs info)
- [ ] Meaningful error messages with context

```csharp
// ✅ GOOD
if (error != Error.Ok)
{
    GD.PushError($"Failed to load achievement '{id}': {error}");
    return;
}

// ❌ BAD
Console.WriteLine("Error!");  // Doesn't show in Godot
```

### Exception Handling
- [ ] Try-catch at appropriate boundaries
- [ ] Exceptions not used for flow control
- [ ] Specific exceptions caught (not bare `catch`)
- [ ] Exceptions logged with context
- [ ] Finally blocks or `using` for cleanup

### Return Types
- [ ] Result objects for expected failures (not exceptions)
- [ ] Nullable types for optional values
- [ ] Error codes documented

```csharp
// ✅ GOOD
public async Task<AchievementUnlockResult> Unlock(string id)
{
    if (!IsAvailable)
        return new() { Success = false, Error = "Not available" };

    // ... unlock logic
    return new() { Success = true };
}

// ❌ BAD
public void Unlock(string id)
{
    if (!IsAvailable)
        throw new InvalidOperationException("Not available");
}
```

---

## 🧪 Testing

### Testability
- [ ] Logic separated from Godot nodes (when practical)
- [ ] Dependencies injected, not hardcoded
- [ ] Public methods have clear contracts
- [ ] Side effects minimized and documented

### Test Coverage
- [ ] Critical paths have tests
- [ ] Edge cases covered
- [ ] Error paths tested
- [ ] Integration tests for Godot-specific code

---

## 📚 Documentation

### Code Comments
- [ ] XML documentation for public APIs
- [ ] Complex logic explained
- [ ] `// TODO:` with assignee and date
- [ ] No commented-out code (use git history)

```csharp
/// <summary>
/// Unlocks an achievement and syncs to all registered platforms.
/// </summary>
/// <param name="achievementId">The achievement identifier from the database.</param>
/// <returns>Task that completes when local unlock is done (platforms sync async).</returns>
public async Task Unlock(string achievementId)
{
    // ...
}
```

### README Updates
- [ ] New features documented
- [ ] Breaking changes noted
- [ ] Examples updated
- [ ] Migration guide provided (if needed)

---

## 🔐 Security

### Input Validation
- [ ] Achievement IDs validated
- [ ] File paths sanitized
- [ ] User input escaped for display

### Data Integrity
- [ ] JSON parsing errors handled
- [ ] File corruption handled gracefully
- [ ] Backup/recovery strategy for save data

---

## 🎨 Code Style

### Readability
- [ ] Methods under 50 lines (guideline, not rule)
- [ ] Single responsibility per method
- [ ] Meaningful variable names
- [ ] Magic numbers replaced with named constants
- [ ] Consistent indentation and spacing

### LINQ Usage
- [ ] LINQ not overused (readability > cleverness)
- [ ] No LINQ in performance-critical loops
- [ ] Query syntax vs method syntax consistent

---

## ✅ Pre-Commit Checklist

Before committing code:

- [ ] Code compiles without warnings
- [ ] All tests pass
- [ ] No debug/console logs left in
- [ ] Followed naming conventions
- [ ] Signals properly connected/disconnected
- [ ] Used Godot APIs (not .NET equivalents)
- [ ] AOT-compatible (no reflection)
- [ ] Resources properly disposed
- [ ] Error handling in place
- [ ] Documentation updated

---

## 🚨 Red Flags

Immediately flag these issues:

- 🚩 Signal connected but never disconnected
- 🚩 `GetNode()` called in `_Process()`
- 🚩 `System.Text.Json.JsonSerializer` usage
- 🚩 `System.IO.File` usage (instead of `FileAccess`)
- 🚩 Reflection (`Type.GetType()`, `Activator.CreateInstance()`)
- 🚩 Bare `catch` blocks swallowing exceptions
- 🚩 `File.Dispose()` never called
- 🚩 Blocking async calls (`.Result`, `.Wait()`)
- 🚩 Memory leaks (nodes not freed, event handlers not removed)

---

## 📝 Review Comments Template

### For Suggestions
```
💡 Consider using StringName here for better performance:
EmitSignal(SignalName.AchievementUnlocked, id);
```

### For Required Changes
```
🔴 This signal connection is never disconnected, causing a memory leak.
Add to _ExitTree():
_button.Pressed -= OnButtonPressed;
```

### For Best Practices
```
✨ Nice use of the provider pattern here! This makes it easy to add new platforms.
```

---

Use this checklist during code review to ensure consistency and quality across the codebase.
