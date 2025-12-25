namespace Godot.Achievements.Core;

/// <summary>
/// Centralized project settings paths and defaults for the achievements system.
/// Use these constants instead of hardcoding settings paths.
/// </summary>
public static class AchievementSettings
{
    /// <summary>
    /// Base path for all achievement settings in Project Settings
    /// </summary>
    public const string BasePath = "addons/achievements";

    #region Database Settings

    /// <summary>
    /// Path to the achievement database resource file
    /// </summary>
    public const string DatabasePath = "addons/achievements/database_path";

    /// <summary>
    /// Default database path if not configured
    /// </summary>
    public const string DefaultDatabasePath = "res://addons/Godot.Achievements.Net/_achievements/_achievements.tres";

    #endregion

    #region Platform Settings

    /// <summary>
    /// Whether Steam integration is enabled
    /// </summary>
    public const string SteamEnabled = "addons/achievements/platforms/steam_enabled";

    /// <summary>
    /// Whether Game Center integration is enabled
    /// </summary>
    public const string GameCenterEnabled = "addons/achievements/platforms/gamecenter_enabled";

    /// <summary>
    /// Whether Google Play Games integration is enabled
    /// </summary>
    public const string GooglePlayEnabled = "addons/achievements/platforms/googleplay_enabled";

    #endregion

    #region Toast Settings

    /// <summary>
    /// Path to the toast scene (.tscn) to use for notifications
    /// </summary>
    public const string ToastScenePath = "addons/achievements/toast/scene_path";

    /// <summary>
    /// Default toast scene path
    /// </summary>
    public const string DefaultToastScenePath = "res://addons/Godot.Achievements.Net/AchievementToastItem.tscn";

    /// <summary>
    /// Toast position on screen (enum value)
    /// </summary>
    public const string ToastPosition = "addons/achievements/toast/position";

    /// <summary>
    /// How long toasts are displayed (in seconds)
    /// </summary>
    public const string ToastDisplayDuration = "addons/achievements/toast/display_duration";

    /// <summary>
    /// Default toast display duration in seconds
    /// </summary>
    public const float DefaultToastDisplayDuration = 5.0f;

    /// <summary>
    /// Path to the unlock sound file
    /// </summary>
    public const string ToastUnlockSound = "addons/achievements/toast/unlock_sound";

    #endregion

    #region Sync Settings

    /// <summary>
    /// Maximum number of retry attempts before abandoning a sync (0 = infinite)
    /// </summary>
    public const string SyncMaxRetryCount = "addons/achievements/sync/max_retry_count";

    /// <summary>
    /// Default max retry count
    /// </summary>
    public const int DefaultSyncMaxRetryCount = 5;

    #endregion
}
