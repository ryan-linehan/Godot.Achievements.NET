#!/bin/bash
# NuGet pack script for Godot.Achievements.NET

set -e  # Exit on error

echo "=================================="
echo "Packing Godot.Achievements.NET"
echo "=================================="

# Create output directory for packages
mkdir -p nupkgs

# Build first
./build.sh

# Pack all projects
echo "Packing Core package..."
dotnet pack src/Godot.Achievements.Core/Godot.Achievements.Core.csproj \
    --configuration Release \
    --no-build \
    --output nupkgs

echo "Packing Steam package..."
dotnet pack src/Godot.Achievements.Steam/Godot.Achievements.Steam.csproj \
    --configuration Release \
    --no-build \
    --output nupkgs

echo "Packing iOS package..."
dotnet pack src/Godot.Achievements.iOS/Godot.Achievements.iOS.csproj \
    --configuration Release \
    --no-build \
    --output nupkgs

echo "Packing Android package..."
dotnet pack src/Godot.Achievements.Android/Godot.Achievements.Android.csproj \
    --configuration Release \
    --no-build \
    --output nupkgs

echo ""
echo "=================================="
echo "Packing completed successfully!"
echo "=================================="
echo "NuGet packages are in ./nupkgs/"
ls -lh nupkgs/
