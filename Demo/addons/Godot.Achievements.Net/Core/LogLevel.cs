namespace Godot.Achievements.Core;

/// <summary>
/// Log level for the achievements system logging.
/// Messages at or above the configured level will be displayed.
/// </summary>
public enum LogLevel
{
    /// <summary>
    /// Show all messages including debug/info level
    /// </summary>
    Info = 0,

    /// <summary>
    /// Show warnings and errors only
    /// </summary>
    Warning = 1,

    /// <summary>
    /// Show errors only
    /// </summary>
    Error = 2,

    /// <summary>
    /// Disable all logging
    /// </summary>
    None = 3
}
