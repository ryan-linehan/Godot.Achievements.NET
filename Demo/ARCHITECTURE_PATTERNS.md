# Architecture Patterns for Godot.Achievements.NET

This document outlines the key architectural patterns and design decisions for implementing the achievement system.

## 🎯 Core Architectural Principles

### 1. Local-First, Platform-Sync Architecture

**Pattern:** Local state is the source of truth, platforms are synchronized copies.

```
User Action
    ↓
Local Provider (user://achievements.json)  ← Source of Truth
    ↓
Parallel Sync to Platforms
    ├─→ Steam
    ├─→ Game Center
    ├─→ Google Play
    └─→ Custom Providers
```

**Implementation:**
```csharp
public async Task Unlock(string achievementId)
{
    // 1. Local first (always succeeds if valid)
    var localResult = await _localProvider.UnlockAchievement(achievementId);

    if (!localResult.Success)
    {
        GD.PushError($"Failed to unlock locally: {localResult.Error}");
        return;
    }

    // 2. Sync to platforms (can fail, queued for retry)
    await SyncToPlatforms(achievementId);

    // 3. Emit signal (only if newly unlocked)
    if (!localResult.WasAlreadyUnlocked)
    {
        EmitSignal(SignalName.AchievementUnlocked, achievementId);
    }
}
```

**Why:**
- Achievements never lost due to network issues
- Works offline
- Platform APIs are unreliable, local storage is not
- Simple conflict resolution: local always wins

---

### 2. Provider Pattern for Platform Abstraction

**Pattern:** Abstract platform-specific logic behind a common interface.

```csharp
public interface IAchievementProvider
{
    string ProviderName { get; }
    bool IsAvailable { get; }

    Task<AchievementUnlockResult> UnlockAchievement(string achievementId);
    Task<Achievement?> GetAchievement(string achievementId);
    Task<Achievement[]> GetAllAchievements();
    Task<float> GetProgress(string achievementId);
    Task SetProgress(string achievementId, float progress);
}
```

**Implementation Example:**
```csharp
public class SteamAchievementProvider : IAchievementProvider
{
    public string ProviderName => "Steam";
    public bool IsAvailable => SteamAPI.IsSteamRunning();

    public async Task<AchievementUnlockResult> UnlockAchievement(string achievementId)
    {
        var steamId = _database.GetById(achievementId)?.SteamId;

        if (string.IsNullOrEmpty(steamId))
            return new() { Success = false, Error = "No Steam ID mapping" };

        bool success = SteamUserStats.SetAchievement(steamId);
        if (success) SteamUserStats.StoreStats();

        return new() { Success = success };
    }
}
```

**Benefits:**
- Easy to add new platforms
- Platform code isolated
- Can mock for testing
- Conditional compilation keeps unused platforms out of build

---

### 3. Autoload Singleton Pattern

**Pattern:** AchievementManager as global singleton via Godot autoload.

```gdscript
# project.godot
[autoload]
Achievements="*res://addons/godot_achievements/AchievementManager.tscn"
```

```csharp
public partial class AchievementManager : Node
{
    public static AchievementManager Instance { get; private set; }

    public override void _EnterTree()
    {
        if (Instance != null)
        {
            GD.PushError("Multiple AchievementManager instances!");
            QueueFree();
            return;
        }

        Instance = this;
    }

    public override void _ExitTree()
    {
        if (Instance == this)
            Instance = null;
    }
}
```

**Why:**
- Easy access from anywhere: `AchievementManager.Instance.Unlock("id")`
- Survives scene changes
- Standard Godot pattern for game systems

---

### 4. Resource-Based Configuration

**Pattern:** Achievement definitions stored as Godot Resources.

```csharp
[GlobalClass]
public partial class Achievement : Resource
{
    [Export] public string Id { get; set; }
    [Export] public string DisplayName { get; set; }
    [Export] public string Description { get; set; }
    [Export] public Texture2D Icon { get; set; }
    // ... platform mappings
}

[GlobalClass]
public partial class AchievementDatabase : Resource
{
    [Export] public Godot.Collections.Array<Achievement> Achievements { get; set; }
}
```

**Saved as:** `res://achievements.tres` (version controlled)

**Benefits:**
- Editable in Godot inspector
- Version controlled
- Type-safe
- No manual parsing

---

### 5. Event-Driven Communication

**Pattern:** Use signals for loose coupling between systems.

```csharp
// Manager emits events
[Signal]
public delegate void AchievementUnlockedEventHandler(string achievementId);

[Signal]
public delegate void ProgressChangedEventHandler(string achievementId, float progress);

// Systems listen
public partial class StatsTracker : Node
{
    public override void _Ready()
    {
        var manager = GetNode<AchievementManager>("/root/Achievements");
        manager.AchievementUnlocked += OnAchievementUnlocked;
    }

    public override void _ExitTree()
    {
        var manager = GetNode<AchievementManager>("/root/Achievements");
        manager.AchievementUnlocked -= OnAchievementUnlocked;
    }

    private void OnAchievementUnlocked(string achievementId)
    {
        // Track in analytics, update UI, etc.
    }
}
```

**Don't:**
```csharp
// ❌ BAD - Tight coupling
public class AchievementManager
{
    private ToastManager _toastManager;
    private AnalyticsManager _analyticsManager;

    public void Unlock(string id)
    {
        _toastManager.Show(id);      // Requires ToastManager
        _analyticsManager.Track(id); // Requires AnalyticsManager
    }
}
```

---

## 🔄 Common Patterns

### Retry Queue Pattern

**Problem:** Platform APIs fail due to network issues.

**Solution:** Queue failed operations for retry.

```csharp
public partial class AchievementManager : Node
{
    private Dictionary<IAchievementProvider, HashSet<string>> _retryQueue = new();
    private Timer _retryTimer;

    public override void _Ready()
    {
        _retryTimer = new Timer
        {
            WaitTime = 30.0,
            Autostart = true
        };
        _retryTimer.Timeout += OnRetryTimeout;
        AddChild(_retryTimer);
    }

    private void QueueForRetry(IAchievementProvider provider, string achievementId)
    {
        if (!_retryQueue.ContainsKey(provider))
            _retryQueue[provider] = new HashSet<string>();

        _retryQueue[provider].Add(achievementId);
    }

    private async void OnRetryTimeout()
    {
        foreach (var (provider, achievements) in _retryQueue.ToArray())
        {
            var successful = new List<string>();

            foreach (var id in achievements)
            {
                var result = await provider.UnlockAchievement(id);
                if (result.Success)
                    successful.Add(id);
            }

            foreach (var id in successful)
                achievements.Remove(id);

            if (achievements.Count == 0)
                _retryQueue.Remove(provider);
        }
    }
}
```

---

### Factory Pattern for Platform Registration

**Problem:** Can't use reflection for AOT compatibility.

**Solution:** Explicit factory methods with conditional compilation.

```csharp
// In each platform package
#if GODOT_PC || GODOT_WINDOWS
public partial class SteamAchievementAutoload : Node
{
    public override void _Ready()
    {
        var manager = GetNode<AchievementManager>("/root/Achievements");
        manager.RegisterProvider(CreateProvider(manager.Database));
    }

    private static SteamAchievementProvider CreateProvider(AchievementDatabase database)
    {
        return new SteamAchievementProvider(database);
    }
}
#endif
```

---

### Command Pattern for Undo/Redo (Editor)

**Pattern:** Editor operations should be undoable.

```csharp
public class AddAchievementCommand : EditorUndoRedoCommand
{
    private AchievementDatabase _database;
    private Achievement _achievement;
    private int _index;

    public override void DoOperation()
    {
        _index = _database.Achievements.Count;
        _database.Achievements.Add(_achievement);
    }

    public override void UndoOperation()
    {
        _database.Achievements.RemoveAt(_index);
    }
}
```

---

### Strategy Pattern for Custom Toasts

**Pattern:** Allow users to customize toast behavior without modifying core.

```csharp
public partial class AchievementManager : Node
{
    [Export] public PackedScene ToastScene { get; set; }

    private void ShowToast(Achievement achievement)
    {
        if (ToastScene == null)
            ToastScene = GD.Load<PackedScene>("res://addons/godot_achievements/ToastDefault.tscn");

        var toast = ToastScene.Instantiate<AchievementToast>();
        GetTree().Root.AddChild(toast);
        toast.Show(achievement);
    }
}
```

---

## 🏗️ Layer Architecture

```
┌─────────────────────────────────────────┐
│         User Game Code                  │  ← AchievementManager.Instance.Unlock()
├─────────────────────────────────────────┤
│      Achievement Manager (Core)         │  ← Orchestration, signals, retry logic
├─────────────────────────────────────────┤
│         Provider Interface              │  ← IAchievementProvider abstraction
├───────┬───────┬───────┬─────────────────┤
│ Local │ Steam │  iOS  │  Custom Plugins │  ← Platform implementations
└───────┴───────┴───────┴─────────────────┘
        ↓       ↓       ↓
    user://  Steam  Game
    .json    API    Center
```

**Responsibilities:**

**User Game Code:**
- Calls simple API: `Unlock()`, `SetProgress()`
- Listens to signals for UI updates

**Achievement Manager:**
- Orchestrates unlock flow
- Manages retry queue
- Emits signals
- Loads achievement database

**Provider Interface:**
- Defines contract for platforms
- Ensures consistency

**Platform Providers:**
- Implement platform-specific logic
- Handle platform authentication
- Map achievement IDs

---

## 🚫 Anti-Patterns to Avoid

### ❌ Don't: Manager Knows About Platforms

```csharp
// BAD - Manager shouldn't know about Steam directly
public class AchievementManager
{
    public void Unlock(string id)
    {
        if (SteamAPI.IsSteamRunning())  // ❌ Tight coupling
        {
            SteamUserStats.SetAchievement(id);
        }
    }
}
```

**Instead:** Use provider abstraction.

---

### ❌ Don't: Synchronous Platform Calls

```csharp
// BAD - Blocking the main thread
public void Unlock(string id)
{
    var result = _steamProvider.UnlockAchievement(id).Result;  // ❌ Blocks!
}
```

**Instead:** Use async/await properly.

---

### ❌ Don't: Global Mutable State

```csharp
// BAD - Static mutable state
public static class AchievementCache
{
    public static Dictionary<string, Achievement> Achievements = new();  // ❌
}
```

**Instead:** Encapsulate state in AchievementManager.

---

### ❌ Don't: Circular Dependencies

```csharp
// BAD - Circular reference
public class AchievementManager
{
    private ToastManager _toastManager;
}

public class ToastManager
{
    private AchievementManager _achievementManager;  // ❌ Circular
}
```

**Instead:** Use signals for communication.

---

## 📏 Design Principles

1. **Single Responsibility:** Each provider handles one platform
2. **Open/Closed:** Open for extension (new providers), closed for modification (core interface)
3. **Dependency Inversion:** Depend on `IAchievementProvider`, not concrete implementations
4. **Interface Segregation:** Separate concerns (unlock, progress, query)
5. **Liskov Substitution:** All providers interchangeable through interface

---

## 🎓 Example: Complete Unlock Flow

```csharp
// 1. User code
await AchievementManager.Instance.Unlock("boss_defeated");

// 2. Manager orchestrates
public async Task Unlock(string achievementId)
{
    // Local first (source of truth)
    var localResult = await _localProvider.UnlockAchievement(achievementId);

    // Sync to platforms in parallel
    var tasks = _platformProviders.Select(async p =>
    {
        try
        {
            var result = await p.UnlockAchievement(achievementId);
            if (!result.Success)
                QueueForRetry(p, achievementId);
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[{p.ProviderName}] {ex.Message}");
            QueueForRetry(p, achievementId);
        }
    });

    await Task.WhenAll(tasks);

    // Emit signal for UI/systems
    if (!localResult.WasAlreadyUnlocked)
        EmitSignal(SignalName.AchievementUnlocked, achievementId);
}

// 3. Toast listens and shows UI
private void OnAchievementUnlocked(string achievementId)
{
    var achievement = _database.GetById(achievementId);
    ShowToast(achievement);
}
```

---

This architecture provides:
- ✅ Reliability (local-first)
- ✅ Extensibility (provider pattern)
- ✅ Testability (interfaces)
- ✅ Performance (async, retry queue)
- ✅ Maintainability (separation of concerns)
