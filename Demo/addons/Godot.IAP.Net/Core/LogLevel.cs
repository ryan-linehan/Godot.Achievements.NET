namespace Godot.IAP.Core;

/// <summary>
/// Log levels for the IAP system
/// </summary>
public enum LogLevel
{
    /// <summary>
    /// Show all messages including informational
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
