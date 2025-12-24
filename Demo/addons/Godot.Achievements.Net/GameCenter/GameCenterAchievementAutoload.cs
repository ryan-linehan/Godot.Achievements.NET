using Godot;
using Godot.Achievements.Core;

namespace Godot.Achievements.iOS;

/// <summary>
/// Autoload node that automatically registers the Game Center achievement provider
/// Add this to project autoloads to enable iOS Game Center achievements
/// </summary>
public partial class GameCenterAchievementAutoload : Node
{
#if GODOT_IOS

    public override void _Ready()
    {
        // Get the achievement manager
        var manager = GetNodeOrNull<AchievementManager>("/root/Achievements");
        if (manager == null)
        {
            GD.PushError("[GameCenter] AchievementManager not found in autoloads. Add it before GameCenterAchievements.");
            return;
        }

        if (manager.Database == null)
        {
            GD.PushError("[GameCenter] AchievementManager has no database assigned.");
            return;
        }

        // Register Game Center provider
        var gameCenterProvider = new GameCenterAchievementProvider(manager.Database);
        manager.RegisterProvider(gameCenterProvider);

        GD.Print("[GameCenter] GameCenterAchievementProvider registered");

    }
#endif
}
