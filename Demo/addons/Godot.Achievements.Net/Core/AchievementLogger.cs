namespace Godot.Achievements.Core;

/// <summary>
/// Centralized logging for the achievements system.
/// All logs are prefixed with [Achievements] for easy filtering.
/// Respects the log level configured in project settings.
/// </summary>
public static class AchievementLogger
{
    /// <summary>
    /// Log areas for consistent categorization
    /// </summary>
    public static class Areas
    {
        public const string Core = "Core";
        public const string Toast = "Toast";
        public const string Editor = "Editor";
        public const string Sync = "Sync";
        public const string Database = "Database";
    }

    /// <summary>
    /// Gets the current log level from project settings
    /// </summary>
    private static LogLevel CurrentLogLevel
    {
        get
        {
            if (!ProjectSettings.HasSetting(AchievementSettings.LogLevel))
            {
                return AchievementSettings.DefaultLogLevel;
            }
            return (LogLevel)ProjectSettings.GetSetting(AchievementSettings.LogLevel).AsInt32();
        }
    }

    /// <summary>
    /// Log an informational message
    /// </summary>
    public static void Log(string area, string message)
    {
        if (CurrentLogLevel <= LogLevel.Info)
        {
            GD.Print($"[Achievements] [{area}] {message}");
        }
    }

    /// <summary>
    /// Log a warning message
    /// </summary>
    public static void Warning(string area, string message)
    {
        if (CurrentLogLevel <= LogLevel.Warning)
        {
            GD.PushWarning($"[Achievements] [{area}] {message}");
        }
    }

    /// <summary>
    /// Log an error message
    /// </summary>
    public static void Error(string area, string message)
    {
        if (CurrentLogLevel <= LogLevel.Error)
        {
            GD.PushError($"[Achievements] [{area}] {message}");
        }
    }

    /// <summary>
    /// Log an informational message without an area (for simple messages)
    /// </summary>
    public static void Log(string message)
    {
        if (CurrentLogLevel <= LogLevel.Info)
        {
            GD.Print($"[Achievements] {message}");
        }
    }

    /// <summary>
    /// Log a warning message without an area
    /// </summary>
    public static void Warning(string message)
    {
        if (CurrentLogLevel <= LogLevel.Warning)
        {
            GD.PushWarning($"[Achievements] {message}");
        }
    }

    /// <summary>
    /// Log an error message without an area
    /// </summary>
    public static void Error(string message)
    {
        if (CurrentLogLevel <= LogLevel.Error)
        {
            GD.PushError($"[Achievements] {message}");
        }
    }
}
