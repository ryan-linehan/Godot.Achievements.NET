#!/bin/bash
# Build script for Godot.Achievements.NET

set -e  # Exit on error

echo "=================================="
echo "Building Godot.Achievements.NET"
echo "=================================="

# Clean previous builds
echo "Cleaning previous builds..."
dotnet clean Godot.Achievements.NET.sln --configuration Release

# Restore dependencies
echo "Restoring dependencies..."
dotnet restore Godot.Achievements.NET.sln

# Build all projects in Release mode
echo "Building Core package..."
dotnet build src/Godot.Achievements.Core/Godot.Achievements.Core.csproj --configuration Release --no-restore

echo "Building Steam package..."
dotnet build src/Godot.Achievements.Steam/Godot.Achievements.Steam.csproj --configuration Release --no-restore

echo "Building iOS package..."
dotnet build src/Godot.Achievements.iOS/Godot.Achievements.iOS.csproj --configuration Release --no-restore

echo "Building Android package..."
dotnet build src/Godot.Achievements.Android/Godot.Achievements.Android.csproj --configuration Release --no-restore

echo ""
echo "=================================="
echo "Build completed successfully!"
echo "=================================="
