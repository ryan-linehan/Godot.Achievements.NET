#if GODOT_ANDROID
using Godot;
using Godot.Achievements.Core;

namespace Godot.Achievements.Android;

/// <summary>
/// Autoload node that automatically registers the Google Play Games achievement provider
/// Add this to project autoloads to enable Android Google Play achievements
/// </summary>
public partial class GooglePlayAchievementAutoload : Node
{
    public override void _Ready()
    {
        // Get the achievement manager
        var manager = GetNodeOrNull<AchievementManager>("/root/Achievements");
        if (manager == null)
        {
            GD.PushError("[GooglePlay] AchievementManager not found in autoloads. Add it before GooglePlayAchievements.");
            return;
        }

        if (manager.Database == null)
        {
            GD.PushError("[GooglePlay] AchievementManager has no database assigned.");
            return;
        }

        // Register Google Play Games provider
        var googlePlayProvider = new GooglePlayAchievementProvider(manager.Database);
        manager.RegisterProvider(googlePlayProvider);

        GD.Print("[GooglePlay] GooglePlayAchievementProvider registered");
    }
}
#endif
