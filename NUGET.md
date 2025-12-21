# NuGet Package Publishing Guide

This document explains how to build and publish Godot.Achievements.NET packages to NuGet.org.

## Prerequisites

- .NET 8.0 SDK
- NuGet account (https://www.nuget.org)
- API key from NuGet.org

## Building Packages

### Linux/macOS

```bash
# Build all projects
./build.sh

# Create NuGet packages
./pack.sh
```

### Windows

```cmd
# Build all projects
build.bat

# Create NuGet packages
pack.bat
```

Packages will be created in `./nupkgs/`:
- `Godot.Achievements.Core.1.0.0.nupkg`
- `Godot.Achievements.Steam.1.0.0.nupkg`
- `Godot.Achievements.iOS.1.0.0.nupkg`
- `Godot.Achievements.Android.1.0.0.nupkg`

## Testing Packages Locally

### 1. Add Local NuGet Source

```bash
# Add local package source
dotnet nuget add source ./nupkgs --name LocalAchievements

# List sources to verify
dotnet nuget list source
```

### 2. Test in a Godot Project

In your test Godot project's `.csproj`:

```xml
<ItemGroup>
  <PackageReference Include="Godot.Achievements.Core" Version="1.0.0" />
</ItemGroup>
```

Then restore:
```bash
dotnet restore
```

### 3. Remove Local Source (After Testing)

```bash
dotnet nuget remove source LocalAchievements
```

## Publishing to NuGet.org

### 1. Get API Key

1. Go to https://www.nuget.org
2. Sign in
3. Go to **API Keys** → **Create**
4. Configure:
   - **Key Name**: "Godot.Achievements Publishing"
   - **Glob Pattern**: `Godot.Achievements.*`
   - **Scopes**: Push new packages and versions
5. Copy the API key

### 2. Push Packages

```bash
# Set your API key (do this once)
export NUGET_API_KEY="your-api-key-here"

# Push Core package (publish this first as others depend on it)
dotnet nuget push nupkgs/Godot.Achievements.Core.1.0.0.nupkg \
    --api-key $NUGET_API_KEY \
    --source https://api.nuget.org/v3/index.json

# Wait for Core package to be indexed (~5-10 minutes)
# Then push platform packages

dotnet nuget push nupkgs/Godot.Achievements.Steam.1.0.0.nupkg \
    --api-key $NUGET_API_KEY \
    --source https://api.nuget.org/v3/index.json

dotnet nuget push nupkgs/Godot.Achievements.iOS.1.0.0.nupkg \
    --api-key $NUGET_API_KEY \
    --source https://api.nuget.org/v3/index.json

dotnet nuget push nupkgs/Godot.Achievements.Android.1.0.0.nupkg \
    --api-key $NUGET_API_KEY \
    --source https://api.nuget.org/v3/index.json
```

### Windows:
```cmd
set NUGET_API_KEY=your-api-key-here

dotnet nuget push nupkgs\Godot.Achievements.Core.1.0.0.nupkg --api-key %NUGET_API_KEY% --source https://api.nuget.org/v3/index.json
dotnet nuget push nupkgs\Godot.Achievements.Steam.1.0.0.nupkg --api-key %NUGET_API_KEY% --source https://api.nuget.org/v3/index.json
dotnet nuget push nupkgs\Godot.Achievements.iOS.1.0.0.nupkg --api-key %NUGET_API_KEY% --source https://api.nuget.org/v3/index.json
dotnet nuget push nupkgs\Godot.Achievements.Android.1.0.0.nupkg --api-key %NUGET_API_KEY% --source https://api.nuget.org/v3/index.json
```

## Version Updates

To publish a new version:

### 1. Update Version Numbers

Edit all `.csproj` files and update `<Version>`:

```xml
<Version>1.1.0</Version>
```

### 2. Update Dependencies

In platform packages, update the Core dependency version:

```xml
<ItemGroup>
  <ProjectReference Include="../Godot.Achievements.Core/Godot.Achievements.Core.csproj" />
</ItemGroup>
```

This will automatically use the correct version when building.

### 3. Build and Pack

```bash
./pack.sh  # or pack.bat on Windows
```

### 4. Push New Version

```bash
dotnet nuget push nupkgs/Godot.Achievements.Core.1.1.0.nupkg --api-key $NUGET_API_KEY --source https://api.nuget.org/v3/index.json
# ... repeat for other packages
```

## Package Validation

Before publishing, validate your packages:

```bash
# Install validation tool
dotnet tool install -g dotnet-validate

# Validate packages
dotnet validate package local nupkgs/Godot.Achievements.Core.1.0.0.nupkg
dotnet validate package local nupkgs/Godot.Achievements.Steam.1.0.0.nupkg
dotnet validate package local nupkgs/Godot.Achievements.iOS.1.0.0.nupkg
dotnet validate package local nupkgs/Godot.Achievements.Android.1.0.0.nupkg
```

## Package Contents

Each package includes:

### Core Package
- `lib/net8.0/Godot.Achievements.Core.dll`
- All source files compiled into the DLL
- Editor plugin integration

### Platform Packages
- `lib/net8.0/Godot.Achievements.[Platform].dll`
- Platform-specific provider implementation
- Autoload scripts

## Troubleshooting

### "Package already exists"

You cannot overwrite an existing package version. Either:
- Increment the version number
- Unlist the package on NuGet.org and upload new version

### "Package dependencies not found"

Wait 5-10 minutes after uploading Core package before uploading platform packages. NuGet needs time to index.

### "Invalid package"

Check:
- [ ] Version numbers are valid (semantic versioning)
- [ ] All required metadata is present (authors, description, license)
- [ ] Package builds successfully

### "Authentication failed"

Check:
- [ ] API key is correct
- [ ] API key has not expired
- [ ] API key has "Push" permission

## Best Practices

1. **Test locally first** - Always test packages with local source before publishing
2. **Version carefully** - Follow semantic versioning (MAJOR.MINOR.PATCH)
3. **Publish Core first** - Platform packages depend on Core
4. **Wait for indexing** - Allow time between Core and platform package uploads
5. **Tag releases** - Create Git tags for each version published
6. **Update README** - Keep installation instructions up to date

## Release Checklist

- [ ] Update version in all `.csproj` files
- [ ] Update CHANGELOG.md
- [ ] Run `./build.sh` - verify all projects build
- [ ] Run `./pack.sh` - create packages
- [ ] Test packages locally in a Godot project
- [ ] Validate packages with `dotnet validate`
- [ ] Commit and push changes to Git
- [ ] Create Git tag (e.g., `v1.0.0`)
- [ ] Push Core package to NuGet
- [ ] Wait 10 minutes for indexing
- [ ] Push platform packages to NuGet
- [ ] Verify packages appear on NuGet.org
- [ ] Update GitHub release notes

## Support

For issues with NuGet packages:
- Check package status: https://www.nuget.org/packages/Godot.Achievements.Core
- NuGet documentation: https://learn.microsoft.com/en-us/nuget/
