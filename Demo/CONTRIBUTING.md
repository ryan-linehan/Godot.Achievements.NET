# Contributing to Godot.Achievements.NET

Thank you for your interest in contributing! This guide will help you get started.

## Getting Started

### Prerequisites

- Godot 4.3+ with .NET support
- .NET 8.0 SDK or later
- Git
- (Optional) Visual Studio 2022, Rider, or VS Code with C# extension

### Clone the Repository

```bash
git clone https://github.com/ryan-linehan/Godot.Achievements.NET.git
cd Godot.Achievements.NET
```

### Open in Godot

1. Open Godot 4.3+
2. Import the `Demo` folder as a project
3. Enable the plugin in **Project > Project Settings > Plugins**

## Project Structure

```
Godot.Achievements.NET/
+-- Demo/
|   +-- addons/
|   |   +-- Godot.Achievements.Net/
|   |       +-- Core/                    # Core types and manager
|   |       |   +-- Achievement.cs
|   |       |   +-- AchievementDatabase.cs
|   |       |   +-- AchievementManager.cs
|   |       |   +-- AchievementLogger.cs
|   |       |   +-- AchievementSettings.cs
|   |       +-- Providers/               # Platform providers
|   |       |   +-- IAchievementProvider.cs
|   |       |   +-- AchievementProviderBase.cs
|   |       |   +-- Local/
|   |       |   +-- Steamworks/
|   |       |   +-- GameCenter/
|   |       |   +-- GooglePlay/
|   |       +-- Editor/                  # Editor dock UI
|   |       +-- Toast/                   # Toast notification system
|   |       +-- AchievementPlugin.cs     # Plugin entry point
|   +-- INTEGRATION_GUIDE.md
|   +-- CONTRIBUTING.md
|   +-- CHANGELOG.md
+-- README.md
```

## How to Contribute

### Reporting Bugs

1. Check if the issue already exists in [GitHub Issues](https://github.com/ryan-linehan/Godot.Achievements.NET/issues)
2. If not, create a new issue with:
   - Clear title and description
   - Steps to reproduce
   - Expected vs actual behavior
   - Environment details (OS, Godot version, .NET version)
   - Code sample if applicable

### Suggesting Features

1. Open a GitHub issue with the `enhancement` label
2. Describe:
   - The problem this feature solves
   - Proposed solution
   - Alternative solutions considered
   - Any breaking changes

### Pull Requests

1. **Fork the repository**
2. **Create a feature branch** from `main`:
   ```bash
   git checkout -b feature/your-feature-name
   ```
3. **Make your changes**
   - Follow the coding guidelines below
   - Test in Godot editor
   - Update documentation
4. **Commit your changes**:
   ```bash
   git commit -m "Add feature: your feature description"
   ```
5. **Push to your fork**:
   ```bash
   git push origin feature/your-feature-name
   ```
6. **Open a Pull Request** on GitHub

## Coding Guidelines

### C# Style

Follow standard C# conventions:

- **PascalCase** for public members, classes, methods
- **camelCase** for private fields (with `_` prefix)
- **Descriptive names** - avoid abbreviations
- **XML documentation** for public APIs
- **Nullable reference types** enabled

Example:
```csharp
public class MyProvider : AchievementProviderBase
{
    private readonly AchievementDatabase _database;

    /// <summary>
    /// Unlocks the specified achievement.
    /// </summary>
    public override void UnlockAchievement(string achievementId)
    {
        // Implementation
    }
}
```

### File Organization

- One class per file
- File name matches class name
- Group related files in folders
- Keep platform-specific code in conditional compilation blocks:

```csharp
#if GODOT_WINDOWS || GODOT_PC
// Windows-specific code
#endif

#if GODOT_IOS
// iOS-specific code
#endif

#if GODOT_ANDROID
// Android-specific code
#endif
```

## Creating a New Platform Provider

To add support for a new platform (e.g., Epic Games Store):

### 1. Create Provider Class

Create `Providers/Epic/EpicAchievementProvider.cs`:

```csharp
#if GODOT_PC // Or appropriate platform condition
using System.Threading.Tasks;
using Godot.Achievements.Core;

namespace Godot.Achievements.Providers.Epic;

public partial class EpicAchievementProvider : AchievementProviderBase
{
    private readonly AchievementDatabase _database;

    public override string ProviderName => ProviderNames.Epic; // Add to ProviderNames.cs
    public override bool IsAvailable => /* check if Epic SDK is initialized */;

    public EpicAchievementProvider(AchievementDatabase database)
    {
        _database = database;
    }

    public override void UnlockAchievement(string achievementId)
    {
        var achievement = _database.GetById(achievementId);
        var epicId = achievement?.EpicId; // Add EpicId property to Achievement.cs

        // Call Epic SDK
        EpicSDK.UnlockAchievement(epicId);
        EmitAchievementUnlocked(achievementId, true);
    }

    // Implement other required methods...
}
#endif
```

### 2. Create Stub for Other Platforms

Create `Providers/Epic/EpicAchievementProvider.Stubs.cs`:

```csharp
#if !GODOT_PC
namespace Godot.Achievements.Providers.Epic;

public class EpicAchievementProvider : IAchievementProvider
{
    public static bool IsPlatformSupported => false;
    public string ProviderName => "Epic";
    public bool IsAvailable => false;

    // Stub implementations that do nothing...
}
#endif
```

### 3. Add Platform ID to Achievement

In `Core/Achievement.cs`, add:

```csharp
[Export]
public string EpicId { get; set; } = string.Empty;
```

### 4. Register Provider in AchievementManager

In `Core/AchievementManager.cs`, add to `CreateProviders()`:

```csharp
if (ProjectSettings.HasSetting(AchievementSettings.EpicEnabled) &&
    ProjectSettings.GetSetting(AchievementSettings.EpicEnabled).AsBool())
{
    if (EpicAchievementProvider.IsPlatformSupported)
    {
        _providers.Add(new EpicAchievementProvider(_database));
    }
}
```

### 5. Add Editor UI

In `Editor/AchievementEditorDetailsPanel.cs`, add fields for the Epic ID.

### 6. Update Documentation

- Add to README.md supported platforms table
- Update INTEGRATION_GUIDE.md with setup instructions

## AOT Compatibility

This project must remain AOT-compatible for iOS:

### Avoid:
- `System.Text.Json.JsonSerializer` (use `Godot.Json` instead)
- Reflection for type discovery
- Dynamic code generation
- `Activator.CreateInstance`

### Use:
- `Godot.Json` for serialization
- `Godot.Collections.Dictionary` for data structures
- Static type references
- Compile-time generics

## Commit Message Format

Use conventional commits:

```
<type>(<scope>): <subject>
```

Types:
- `feat`: New feature
- `fix`: Bug fix
- `docs`: Documentation only
- `refactor`: Code refactoring
- `chore`: Maintenance tasks

Examples:
```
feat(steam): Add progressive achievement support
fix(core): Fix null reference in GetAchievement
docs(readme): Update installation instructions
```

## Code Review Process

All PRs require:

1. **Code review** - At least one maintainer approval
2. **Documentation** - README and code comments updated
3. **Testing** - Verified in Godot editor

Reviewers will check:
- [ ] Code follows style guidelines
- [ ] AOT compatibility maintained
- [ ] Public APIs have XML documentation
- [ ] Platform-specific code properly isolated
- [ ] Signal handlers properly disconnected in `_ExitTree()`

## Getting Help

- **Questions**: Open a GitHub Discussion
- **Bugs**: Open a GitHub Issue

## License

By contributing, you agree that your contributions will be licensed under the MIT License.

## Thank You!

Your contributions make this project better for everyone!
