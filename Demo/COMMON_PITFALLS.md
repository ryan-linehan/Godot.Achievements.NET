# Common Pitfalls & How to Avoid Them

This document catalogs common mistakes when developing Godot C# plugins and how to avoid them.

---

## 🔴 Critical Issues

### 1. Signal Memory Leaks

**Problem:** Signals not disconnected cause memory leaks and ghost callbacks.

```csharp
// ❌ WRONG - Memory leak!
public partial class MyNode : Control
{
    public override void _Ready()
    {
        GetNode<Button>("MyButton").Pressed += OnButtonPressed;
        // Signal never disconnected!
    }

    private void OnButtonPressed()
    {
        GD.Print("Button pressed");
    }
}
```

**Solution:**
```csharp
// ✅ CORRECT
public partial class MyNode : Control
{
    private Button _button;

    public override void _Ready()
    {
        _button = GetNode<Button>("MyButton");
        _button.Pressed += OnButtonPressed;
    }

    public override void _ExitTree()
    {
        // ALWAYS disconnect signals!
        if (_button != null)
        {
            _button.Pressed -= OnButtonPressed;
        }
    }

    private void OnButtonPressed()
    {
        GD.Print("Button pressed");
    }
}
```

**Why it matters:**
- Scene reloads don't garbage collect signal connections
- Can cause callbacks on deleted nodes
- Leads to `ObjectDisposedException`
- Memory leaks accumulate over time

---

### 2. Using System.IO Instead of FileAccess

**Problem:** `System.IO` doesn't understand Godot's virtual filesystem.

```csharp
// ❌ WRONG - Won't work with user:// or res://
using var stream = File.OpenRead("user://save.json");
var text = File.ReadAllText("res://config.json");
```

**Solution:**
```csharp
// ✅ CORRECT - Use Godot's FileAccess
using var file = FileAccess.Open("user://save.json", FileAccess.ModeFlags.Read);
var text = file.GetAsText();

// Check existence
if (FileAccess.FileExists("user://save.json"))
{
    // ...
}
```

**Why it matters:**
- `user://` maps to different paths per platform
- `res://` is read-only and packed in exported games
- Permissions handled correctly
- Works on mobile/console platforms

---

### 3. Reflection in AOT Builds

**Problem:** iOS and some consoles use AOT compilation that strips reflection.

```csharp
// ❌ WRONG - Breaks on iOS!
var type = Type.GetType("MyAchievementProvider");
var instance = Activator.CreateInstance(type);
var method = type.GetMethod("Unlock");
method.Invoke(instance, new[] { "achievement_id" });
```

**Solution:**
```csharp
// ✅ CORRECT - Direct instantiation
var instance = new MyAchievementProvider();
instance.Unlock("achievement_id");

// Or use interface polymorphism
IAchievementProvider provider = GetProvider();
provider.Unlock("achievement_id");
```

**Why it matters:**
- Code works in editor but crashes on iOS
- Hard to debug (often fails at runtime)
- No warning at compile time

---

### 4. Using System.Text.Json Instead of Godot.Json

**Problem:** `System.Text.Json` uses reflection and doesn't integrate with Godot's Variant system.

```csharp
// ❌ WRONG - Not AOT-safe!
var json = JsonSerializer.Serialize(_achievements);
var data = JsonSerializer.Deserialize<Achievement>(json);
```

**Solution:**
```csharp
// ✅ CORRECT - Use Godot's Json class
var jsonString = Json.Stringify(_achievements, "\t");

var json = new Json();
var error = json.Parse(jsonString);
if (error == Error.Ok)
{
    var data = json.Data.AsGodotDictionary();
}
```

**Why it matters:**
- AOT compatibility (iOS, consoles)
- Integrates with Godot's Variant system
- Works with Godot.Collections types
- No reflection needed

---

### 5. GetNode() in Constructor or _EnterTree()

**Problem:** Scene tree not fully ready during construction.

```csharp
// ❌ WRONG - Scene tree not ready!
public MyNode()
{
    _button = GetNode<Button>("MyButton");  // Crash!
}

// ❌ ALSO WRONG - Children might not exist yet
public override void _EnterTree()
{
    _label = GetNode<Label>("VBox/Label");  // Crash!
}
```

**Solution:**
```csharp
// ✅ CORRECT - Wait for _Ready()
public override void _Ready()
{
    _button = GetNode<Button>("MyButton");
    _label = GetNode<Label>("VBox/Label");
}
```

**Lifecycle order:**
1. Constructor - Node created, scene tree NOT ready
2. `_EnterTree()` - Added to tree, children might not exist
3. `_Ready()` - Everything ready, safe to GetNode

---

## ⚠️ Common Mistakes

### 6. Forgetting to Free Nodes

**Problem:** Manually created nodes never freed.

```csharp
// ❌ WRONG - Node never freed!
public void AddItem(string text)
{
    var label = new Label { Text = text };
    container.AddChild(label);
    // Label will exist forever!
}
```

**Solution:**
```csharp
// ✅ CORRECT - Track and free nodes
private List<Label> _labels = new();

public void AddItem(string text)
{
    var label = new Label { Text = text };
    container.AddChild(label);
    _labels.Add(label);
}

public void Clear()
{
    foreach (var label in _labels)
    {
        label.QueueFree();
    }
    _labels.Clear();
}

// Or iterate children
public void Clear()
{
    foreach (var child in container.GetChildren())
    {
        child.QueueFree();
    }
}
```

---

### 7. Hardcoding Paths Instead of Using Exports

**Problem:** Values not configurable, testing difficult.

```csharp
// ❌ WRONG - Hardcoded!
public partial class AchievementToast : Control
{
    public override void _Ready()
    {
        var duration = 3.0f;  // Can't change in editor
        var sound = GD.Load<AudioStream>("res://sounds/unlock.wav");  // Hardcoded
    }
}
```

**Solution:**
```csharp
// ✅ CORRECT - Use exports
public partial class AchievementToast : Control
{
    [Export] public float DisplayDuration { get; set; } = 3.0f;
    [Export] public AudioStream UnlockSound { get; set; }

    // No _Ready() needed, values set by editor or defaults
}
```

---

### 8. Not Caching GetNode Results

**Problem:** Repeatedly calling GetNode is slow.

```csharp
// ❌ WRONG - GetNode every frame!
public override void _Process(double delta)
{
    GetNode<Label>("Score").Text = $"Score: {_score}";  // Slow!
    GetNode<ProgressBar>("Health").Value = _health;     // Slow!
}
```

**Solution:**
```csharp
// ✅ CORRECT - Cache in _Ready()
private Label _scoreLabel;
private ProgressBar _healthBar;

public override void _Ready()
{
    _scoreLabel = GetNode<Label>("Score");
    _healthBar = GetNode<ProgressBar>("Health");
}

public override void _Process(double delta)
{
    _scoreLabel.Text = $"Score: {_score}";
    _healthBar.Value = _health;
}
```

---

### 9. Mixing Godot and .NET Collections for Exports

**Problem:** C# collections don't export to Godot.

```csharp
// ❌ WRONG - Won't show in editor!
[Export] public List<string> Items { get; set; }
[Export] public Dictionary<string, int> Scores { get; set; }
```

**Solution:**
```csharp
// ✅ CORRECT - Use Godot collections for exports
[Export] public Godot.Collections.Array<string> Items { get; set; }
[Export] public Godot.Collections.Dictionary<string, int> Scores { get; set; }

// Use C# collections internally if needed
private List<string> _internalList = new();
```

---

### 10. Not Disposing FileAccess

**Problem:** File handles leak, can lock files.

```csharp
// ❌ WRONG - File never closed!
public void SaveData(string data)
{
    var file = FileAccess.Open("user://save.json", FileAccess.ModeFlags.Write);
    file.StoreString(data);
    // File handle leaked!
}
```

**Solution:**
```csharp
// ✅ CORRECT - Use 'using' statement
public void SaveData(string data)
{
    using var file = FileAccess.Open("user://save.json", FileAccess.ModeFlags.Write);
    file.StoreString(data);
}  // Automatically disposed
```

---

## 🟡 Performance Issues

### 11. Allocating in _Process()

**Problem:** Creating objects every frame causes GC pressure.

```csharp
// ❌ WRONG - Allocation every frame!
public override void _Process(double delta)
{
    var list = new List<Enemy>();  // GC pressure!
    foreach (var enemy in GetTree().GetNodesInGroup("enemies"))
    {
        list.Add((Enemy)enemy);
    }
    // Process enemies...
}
```

**Solution:**
```csharp
// ✅ CORRECT - Reuse collections
private List<Enemy> _enemyCache = new();

public override void _Process(double delta)
{
    _enemyCache.Clear();  // Reuse, don't recreate
    foreach (var enemy in GetTree().GetNodesInGroup("enemies"))
    {
        _enemyCache.Add((Enemy)enemy);
    }
    // Process enemies...
}
```

---

### 12. Unnecessary String Concatenation

**Problem:** String concatenation in loops creates garbage.

```csharp
// ❌ WRONG - Creates many strings
string result = "";
foreach (var item in items)
{
    result += item.Name + ", ";  // Allocates new string each time!
}
```

**Solution:**
```csharp
// ✅ CORRECT - Use StringBuilder
var sb = new StringBuilder();
foreach (var item in items)
{
    sb.Append(item.Name);
    sb.Append(", ");
}
string result = sb.ToString();

// Or LINQ for simple cases
string result = string.Join(", ", items.Select(i => i.Name));
```

---

### 13. Blocking Async Calls

**Problem:** Using `.Result` or `.Wait()` blocks the main thread.

```csharp
// ❌ WRONG - Blocks UI thread!
public void UnlockAchievement(string id)
{
    var result = _provider.UnlockAchievement(id).Result;  // Freezes game!
}
```

**Solution:**
```csharp
// ✅ CORRECT - Use async/await
public async void UnlockAchievement(string id)
{
    var result = await _provider.UnlockAchievement(id);
}

// Or for event handlers
private async void OnButtonPressed()
{
    await UnlockAchievement("achievement_id");
}
```

---

## 🔵 Design Issues

### 14. Tight Coupling

**Problem:** Classes depend directly on concrete implementations.

```csharp
// ❌ WRONG - Tight coupling
public class AchievementManager
{
    private SteamAchievementProvider _steamProvider;
    private ToastManager _toastManager;

    public void Unlock(string id)
    {
        _steamProvider.Unlock(id);
        _toastManager.ShowToast(id);
    }
}
```

**Solution:**
```csharp
// ✅ CORRECT - Dependency injection + signals
public class AchievementManager
{
    private List<IAchievementProvider> _providers;  // Interface

    [Signal]
    public delegate void AchievementUnlockedEventHandler(string id);

    public void Unlock(string id)
    {
        foreach (var provider in _providers)
            provider.Unlock(id);

        EmitSignal(SignalName.AchievementUnlocked, id);  // Decoupled
    }
}

// Toast listens to signal
public class ToastManager
{
    public override void _Ready()
    {
        AchievementManager.AchievementUnlocked += ShowToast;
    }
}
```

---

### 15. God Object Anti-Pattern

**Problem:** One class doing everything.

```csharp
// ❌ WRONG - Does too much!
public class AchievementManager
{
    public void Unlock(string id) { }
    public void ShowToast(string id) { }
    public void PlaySound(string id) { }
    public void SaveToFile(string path) { }
    public void SyncToSteam(string id) { }
    public void UpdateUI(string id) { }
    // ... 50 more methods
}
```

**Solution:**
```csharp
// ✅ CORRECT - Separation of concerns
public class AchievementManager  // Orchestration only
{
    private List<IAchievementProvider> _providers;

    public void Unlock(string id)
    {
        // Delegate to providers
        foreach (var p in _providers)
            p.Unlock(id);

        // Emit events for other systems
        EmitSignal(SignalName.AchievementUnlocked, id);
    }
}

public class ToastManager  // UI only
{
    public void ShowToast(Achievement ach) { }
}

public class SteamProvider : IAchievementProvider  // Steam only
{
    public void Unlock(string id) { }
}
```

---

## 🟢 Best Practices Violations

### 16. Not Using StringName for Signals

**Problem:** String allocations for every signal emission.

```csharp
// ❌ WRONG - Allocates strings
EmitSignal("AchievementUnlocked", achievementId);
EmitSignal("ProgressChanged", id, progress);
```

**Solution:**
```csharp
// ✅ CORRECT - Use StringName
EmitSignal(SignalName.AchievementUnlocked, achievementId);
EmitSignal(SignalName.ProgressChanged, id, progress);
```

---

### 17. Ignoring Error Codes

**Problem:** Not checking return values or error codes.

```csharp
// ❌ WRONG - Ignores errors!
var json = new Json();
json.Parse(jsonString);
var data = json.Data.AsGodotDictionary();  // Might be null!
```

**Solution:**
```csharp
// ✅ CORRECT - Check error codes
var json = new Json();
var error = json.Parse(jsonString);

if (error != Error.Ok)
{
    GD.PushError($"JSON parse failed: {json.GetErrorMessage()}");
    return;
}

var data = json.Data.AsGodotDictionary();
```

---

### 18. Not Validating User Input

**Problem:** Assuming input is always valid.

```csharp
// ❌ WRONG - No validation!
public void UnlockAchievement(string id)
{
    var achievement = _database.GetById(id);
    achievement.IsUnlocked = true;  // NullReferenceException if not found!
}
```

**Solution:**
```csharp
// ✅ CORRECT - Validate input
public void UnlockAchievement(string id)
{
    if (string.IsNullOrEmpty(id))
    {
        GD.PushError("Achievement ID is null or empty");
        return;
    }

    var achievement = _database.GetById(id);
    if (achievement == null)
    {
        GD.PushError($"Achievement '{id}' not found in database");
        return;
    }

    achievement.IsUnlocked = true;
}
```

---

## 📊 Detection & Prevention

### Static Analysis
Use these tools to catch issues:
- **Rider/ReSharper:** Built-in code analysis
- **SonarLint:** Free Godot-aware linting
- **Code reviews:** Use the checklist

### Common Warning Signs
🚩 Memory usage growing over time → Signal leak
🚩 Slow frame rate → Allocations in _Process()
🚩 Crashes on iOS → Reflection usage
🚩 File not found errors → Using wrong file API
🚩 `ObjectDisposedException` → Signal callback on freed node

### Testing Strategies
1. **Scene reloading test:** Reload scene 10x, check memory
2. **Platform test:** Test on actual iOS device, not just editor
3. **Stress test:** Spam unlock, check for leaks
4. **Offline test:** Disconnect network, verify offline queue works

---

## 🎓 Learning Resources

**Godot C# Best Practices:**
- https://docs.godotengine.org/en/stable/tutorials/scripting/c_sharp/
- https://github.com/godotengine/godot-proposals

**Common Patterns:**
- Provider pattern for platform abstraction
- Event-driven with signals for decoupling
- Resource pattern for data definition
- Singleton via autoload for managers

---

Remember: **If it works in the editor but crashes on export, it's likely AOT or file path issues.**
