# Changelog

All notable changes to Godot.Achievements.NET will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Initial implementation of core achievement system
- Steam achievement provider
- iOS Game Center achievement provider
- Android Google Play Games achievement provider
- Editor plugin with achievement management dock
- Default toast notification system
- Local-first sync strategy with automatic retry
- AOT-compatible architecture using Godot's Json class
- NuGet packaging support
- Build scripts for cross-platform development
- Comprehensive documentation and examples

## [1.0.0] - TBD

### Added
- Core achievement system (`Godot.Achievements.Core`)
  - `Achievement` resource class with platform ID mappings
  - `AchievementDatabase` resource with validation
  - `IAchievementProvider` interface for platform abstraction
  - `AchievementManager` singleton with sync logic
  - `LocalAchievementProvider` for JSON persistence
  - `AchievementToast` for default notifications
  - Editor plugin and dock UI

- Platform Providers
  - `Godot.Achievements.Steam` - Steam achievements via Steamworks.NET
  - `Godot.Achievements.iOS` - Game Center achievements
  - `Godot.Achievements.Android` - Google Play Games achievements

- Features
  - Local-first achievement storage (`user://achievements.json`)
  - Automatic platform sync with retry queue
  - Progressive achievement support (0.0 to 1.0 progress)
  - Signals for achievement events
  - Custom platform provider extensibility
  - Platform-specific autoload registration
  - Conditional compilation for platform targeting

- Documentation
  - README with quick start and API reference
  - Platform-specific READMEs for each provider
  - DESIGN.md with architecture details
  - ARCHITECTURE_PATTERNS.md
  - CODE_REVIEW_CHECKLIST.md
  - COMMON_PITFALLS.md
  - CONTRIBUTING.md
  - NUGET.md for package publishing
  - Example usage code

### Technical Details
- Target framework: .NET 8.0
- Godot version: 4.3+
- License: MIT
- AOT compatible: Yes

## Version History

### Version Numbering

We follow [Semantic Versioning](https://semver.org/):
- **MAJOR** version: Incompatible API changes
- **MINOR** version: Backwards-compatible functionality
- **PATCH** version: Backwards-compatible bug fixes

### Release Process

1. Update version in all `.csproj` files
2. Update CHANGELOG.md
3. Create Git tag (e.g., `v1.0.0`)
4. Build and pack NuGet packages
5. Publish to NuGet.org
6. Create GitHub release with notes

## Future Releases

### Planned for 1.1.0
- [ ] Enhanced editor UI with icon preview
- [ ] Achievement import/export (CSV)
- [ ] Runtime debug panel for testing
- [ ] Achievement unlock sound effects
- [ ] Custom toast themes

### Planned for 1.2.0
- [ ] Xbox Live achievements
- [ ] PlayStation Network achievements
- [ ] Nintendo Switch achievements
- [ ] Discord Rich Presence integration

### Planned for 2.0.0
- [ ] Achievement categories/groups
- [ ] Secret achievements
- [ ] Time-limited achievements
- [ ] Leaderboards integration
- [ ] Cloud save synchronization

## Breaking Changes

None yet (pre-1.0.0).

## Migration Guides

Will be added when breaking changes are introduced.

## Support

For questions and issues:
- GitHub Issues: https://github.com/ryan-linehan/Godot.Achievements.NET/issues
- Discussions: https://github.com/ryan-linehan/Godot.Achievements.NET/discussions
