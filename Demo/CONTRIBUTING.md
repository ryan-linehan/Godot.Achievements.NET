# Contributing to Godot.Achievements.NET

Thank you for your interest in contributing! This guide will help you get started.

## Getting Started

### Prerequisites

- .NET 8.0 SDK or later
- Git
- (Optional) Visual Studio 2022, Rider, or VS Code with C# extension
- (Optional) Godot 4.3+ for testing

### Clone the Repository

```bash
git clone https://github.com/ryan-linehan/Godot.Achievements.NET.git
cd Godot.Achievements.NET
```

### Build the Project

```bash
# Linux/macOS
./build.sh

# Windows
build.bat
```

## Project Structure

```
Godot.Achievements.NET/
├── src/
│   ├── Godot.Achievements.Core/       # Core package (required)
│   ├── Godot.Achievements.Steam/      # Steam provider
│   ├── Godot.Achievements.iOS/        # iOS Game Center provider
│   └── Godot.Achievements.Android/    # Android Google Play provider
├── examples/                          # Example usage code
├── addons/                            # Godot editor plugin files
├── DESIGN.md                          # Architecture documentation
├── ARCHITECTURE_PATTERNS.md           # Design patterns used
├── CODE_REVIEW_CHECKLIST.md          # Code review guidelines
└── COMMON_PITFALLS.md                # Known issues and solutions
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
   - Add tests if applicable
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
public class AchievementProvider : IAchievementProvider
{
    private readonly AchievementDatabase _database;

    /// <summary>
    /// Unlocks the specified achievement
    /// </summary>
    public async Task<AchievementUnlockResult> UnlockAchievement(string achievementId)
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
```

### Documentation

- **XML comments** for all public APIs
- **README.md** for each package
- **Code comments** for complex logic
- **DESIGN.md** updates for architectural changes

### Testing

- Test your changes in a real Godot project
- Verify multi-platform if applicable
- Test edge cases (null values, empty strings, etc.)

## Creating a New Platform Provider

To add support for a new platform (e.g., Epic Games Store):

### 1. Create Package Structure

```bash
mkdir -p src/Godot.Achievements.YourPlatform
```

### 2. Create .csproj

```xml
<Project Sdk="Godot.NET.Sdk/4.3.0">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <PackageId>Godot.Achievements.YourPlatform</PackageId>
    <Version>1.0.0</Version>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="../Godot.Achievements.Core/Godot.Achievements.Core.csproj" />
  </ItemGroup>
</Project>
```

### 3. Implement Provider

```csharp
#if GODOT_PLATFORM_CONDITION
using Godot.Achievements.Core;

namespace Godot.Achievements.YourPlatform;

public class YourPlatformAchievementProvider : IAchievementProvider
{
    public string ProviderName => "Your Platform";
    public bool IsAvailable => /* check if SDK is initialized */;

    // Implement interface methods...
}
#endif
```

### 4. Create Autoload

```csharp
#if GODOT_PLATFORM_CONDITION
public partial class YourPlatformAutoload : Node
{
    public override void _Ready()
    {
        var manager = GetNode<AchievementManager>("/root/Achievements");
        manager.RegisterProvider(new YourPlatformAchievementProvider(manager.Database));
    }
}
#endif
```

### 5. Add README

Create `src/Godot.Achievements.YourPlatform/README.md` with:
- Installation instructions
- Configuration steps
- Platform-specific setup
- Example usage

### 6. Update Solution

Add to `Godot.Achievements.NET.sln`:
```
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Godot.Achievements.YourPlatform", "src\Godot.Achievements.YourPlatform\Godot.Achievements.YourPlatform.csproj"
EndProject
```

### 7. Update Build Scripts

Add to `build.sh` and `build.bat`:
```bash
dotnet build src/Godot.Achievements.YourPlatform/Godot.Achievements.YourPlatform.csproj --configuration Release
```

## AOT Compatibility

This project must remain AOT-compatible for iOS and console platforms:

### ❌ Avoid:
- `System.Text.Json.JsonSerializer` (use `Godot.Json` instead)
- Reflection for type discovery
- Dynamic code generation
- `Activator.CreateInstance`

### ✅ Use:
- `Godot.Json` for serialization
- `Godot.Collections.Dictionary` for data structures
- Static type references
- Compile-time generics

## Commit Message Format

Use conventional commits:

```
<type>(<scope>): <subject>

<body>

<footer>
```

Types:
- `feat`: New feature
- `fix`: Bug fix
- `docs`: Documentation only
- `style`: Code style changes (formatting)
- `refactor`: Code refactoring
- `test`: Adding tests
- `chore`: Maintenance tasks

Examples:
```
feat(steam): Add progressive achievement support
fix(core): Fix null reference in GetAchievement
docs(readme): Update installation instructions
```

## Code Review Process

All PRs require:

1. **Passing builds** - All projects must compile
2. **Code review** - At least one maintainer approval
3. **Documentation** - README and code comments updated
4. **No breaking changes** - Unless version bump to next major

Reviewers will check:
- [ ] Code follows style guidelines
- [ ] AOT compatibility maintained
- [ ] Public APIs have XML documentation
- [ ] No unnecessary dependencies added
- [ ] Platform-specific code properly isolated
- [ ] Changes align with project architecture

## Getting Help

- **Questions**: Open a GitHub Discussion
- **Bugs**: Open a GitHub Issue
- **Chat**: (Discord link TBD)

## License

By contributing, you agree that your contributions will be licensed under the MIT License.

## Thank You!

Your contributions make this project better for everyone. We appreciate your time and effort! 🎉
