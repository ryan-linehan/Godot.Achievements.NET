namespace Godot.IAP.Core;

/// <summary>
/// Centralized project settings paths and defaults for the IAP system.
/// Use these constants instead of hardcoding settings paths.
/// </summary>
public static class IAPSettings
{
    /// <summary>
    /// Base path for all IAP settings in Project Settings
    /// </summary>
    public const string BasePath = "addons/iap";

    #region Database Settings

    /// <summary>
    /// Path to the product catalog resource file
    /// </summary>
    public const string CatalogPath = "addons/iap/catalog_path";

    /// <summary>
    /// Default catalog path if not configured
    /// </summary>
    public const string DefaultCatalogPath = "res://addons/Godot.IAP.Net/_products/_products.tres";

    #endregion

    #region Platform Settings

    /// <summary>
    /// Whether Apple App Store integration is enabled
    /// </summary>
    public const string AppleEnabled = "addons/iap/platforms/apple_enabled";

    /// <summary>
    /// Whether Google Play Billing integration is enabled
    /// </summary>
    public const string GooglePlayEnabled = "addons/iap/platforms/googleplay_enabled";

    #endregion

    #region Logging Settings

    /// <summary>
    /// Log level for the IAP system (Info, Warning, Error, None)
    /// </summary>
    public const string LogLevel = "addons/iap/log_level";

    /// <summary>
    /// Default log level (Info = show all messages)
    /// </summary>
    public const LogLevel DefaultLogLevel = Core.LogLevel.Info;

    #endregion
}
