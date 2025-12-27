namespace Godot.IAP.Core;

/// <summary>
/// Centralized logging for the IAP system.
/// All logs are prefixed with [IAP] for easy filtering.
/// Respects the log level configured in project settings.
/// </summary>
public static class IAPLogger
{
    /// <summary>
    /// Log areas for consistent categorization
    /// </summary>
    public static class Areas
    {
        public const string Core = "Core";
        public const string Editor = "Editor";
        public const string Purchase = "Purchase";
        public const string Restore = "Restore";
        public const string Catalog = "Catalog";
        public const string Subscription = "Subscription";
    }

    /// <summary>
    /// Gets the current log level from project settings
    /// </summary>
    private static LogLevel CurrentLogLevel
    {
        get
        {
            if (!ProjectSettings.HasSetting(IAPSettings.LogLevel))
            {
                return IAPSettings.DefaultLogLevel;
            }
            return (LogLevel)ProjectSettings.GetSetting(IAPSettings.LogLevel).AsInt32();
        }
    }

    /// <summary>
    /// Log an informational message
    /// </summary>
    public static void Log(string area, string message)
    {
        if (CurrentLogLevel <= LogLevel.Info)
        {
            GD.Print($"[IAP] [{area}] {message}");
        }
    }

    /// <summary>
    /// Log a warning message
    /// </summary>
    public static void Warning(string area, string message)
    {
        if (CurrentLogLevel <= LogLevel.Warning)
        {
            GD.PushWarning($"[IAP] [{area}] {message}");
        }
    }

    /// <summary>
    /// Log an error message
    /// </summary>
    public static void Error(string area, string message)
    {
        if (CurrentLogLevel <= LogLevel.Error)
        {
            GD.PushError($"[IAP] [{area}] {message}");
        }
    }

    /// <summary>
    /// Log an informational message without an area (for simple messages)
    /// </summary>
    public static void Log(string message)
    {
        if (CurrentLogLevel <= LogLevel.Info)
        {
            GD.Print($"[IAP] {message}");
        }
    }

    /// <summary>
    /// Log a warning message without an area
    /// </summary>
    public static void Warning(string message)
    {
        if (CurrentLogLevel <= LogLevel.Warning)
        {
            GD.PushWarning($"[IAP] {message}");
        }
    }

    /// <summary>
    /// Log an error message without an area
    /// </summary>
    public static void Error(string message)
    {
        if (CurrentLogLevel <= LogLevel.Error)
        {
            GD.PushError($"[IAP] {message}");
        }
    }
}
