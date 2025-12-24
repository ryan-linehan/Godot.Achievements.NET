using Godot;
using Godot.Achievements.Core;

namespace Godot.Achievements.Steam;

/// <summary>
/// Autoload node that automatically registers the Steam achievement provider
/// Add this to project autoloads to enable Steam achievements
/// </summary>
public partial class SteamAchievementAutoload : Node
{
#if GODOT_PC || GODOT_WINDOWS || GODOT_LINUX || GODOT_MACOS || GODOT_X11 || GODOT_OSX
    public override void _Ready()
    {

        // Get the achievement manager
        var manager = GetNodeOrNull<AchievementManager>("/root/Achievements");
        if (manager == null)
        {
            GD.PushError("[Steam] AchievementManager not found in autoloads. Add it before SteamAchievements.");
            return;
        }

        if (manager.Database == null)
        {
            GD.PushError("[Steam] AchievementManager has no database assigned.");
            return;
        }

        // Register Steam provider
        var steamProvider = new SteamAchievementProvider(manager.Database);
        manager.RegisterProvider(steamProvider);

        GD.Print("[Steam] SteamAchievementProvider registered");

    }
#endif
}
