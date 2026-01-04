#if GODOT_ANDROID
using System;

namespace Godot.Achievements.Providers.GooglePlay;

/// <summary>
/// Helper node to receive app lifecycle notifications.
/// </summary>
internal partial class AppResumeListener : Node
{
    public event Action? OnAppResumed;

    public override void _Notification(int what)
    {
        if (what == NotificationApplicationResumed)
        {
            OnAppResumed?.Invoke();
        }
    }
}
#endif
