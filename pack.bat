@echo off
REM NuGet pack script for Godot.Achievements.NET (Windows)

echo ==================================
echo Packing Godot.Achievements.NET
echo ==================================

REM Create output directory for packages
if not exist nupkgs mkdir nupkgs

REM Build first
call build.bat

REM Pack all projects
echo Packing Core package...
dotnet pack src\Godot.Achievements.Core\Godot.Achievements.Core.csproj --configuration Release --no-build --output nupkgs

echo Packing Steam package...
dotnet pack src\Godot.Achievements.Steam\Godot.Achievements.Steam.csproj --configuration Release --no-build --output nupkgs

echo Packing iOS package...
dotnet pack src\Godot.Achievements.iOS\Godot.Achievements.iOS.csproj --configuration Release --no-build --output nupkgs

echo Packing Android package...
dotnet pack src\Godot.Achievements.Android\Godot.Achievements.Android.csproj --configuration Release --no-build --output nupkgs

echo.
echo ==================================
echo Packing completed successfully!
echo ==================================
echo NuGet packages are in .\nupkgs\
dir nupkgs
