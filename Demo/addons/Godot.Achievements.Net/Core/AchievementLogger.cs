namespace Godot.Achievements.Core;

/// <summary>
/// Centralized logging for the achievements system.
/// All logs are prefixed with [Achievements] for easy filtering.
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
    /// Log an informational message
    /// </summary>
    public static void Log(string area, string message)
    {
        GD.Print($"[Achievements] [{area}] {message}");
    }

    /// <summary>
    /// Log a warning message
    /// </summary>
    public static void Warning(string area, string message)
    {
        GD.PushWarning($"[Achievements] [{area}] {message}");
    }

    /// <summary>
    /// Log an error message
    /// </summary>
    public static void Error(string area, string message)
    {
        GD.PushError($"[Achievements] [{area}] {message}");
    }

    /// <summary>
    /// Log an informational message without an area (for simple messages)
    /// </summary>
    public static void Log(string message)
    {
        GD.Print($"[Achievements] {message}");
    }

    /// <summary>
    /// Log a warning message without an area
    /// </summary>
    public static void Warning(string message)
    {
        GD.PushWarning($"[Achievements] {message}");
    }

    /// <summary>
    /// Log an error message without an area
    /// </summary>
    public static void Error(string message)
    {
        GD.PushError($"[Achievements] {message}");
    }
}
