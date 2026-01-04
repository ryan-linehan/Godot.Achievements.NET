#if TOOLS
namespace Godot.Achievements.Core.Editor;

/// <summary>
/// Result of an export operation
/// </summary>
public readonly struct ExportResult
{
    public bool Success { get; }
    public string? ErrorMessage { get; }
    public int ExportedCount { get; }

    private ExportResult(bool success, string? errorMessage, int exported)
    {
        Success = success;
        ErrorMessage = errorMessage;
        ExportedCount = exported;
    }

    public static ExportResult SuccessResult(int exported)
        => new(true, null, exported);

    public static ExportResult FailureResult(string errorMessage)
        => new(false, errorMessage, 0);
}
#endif
