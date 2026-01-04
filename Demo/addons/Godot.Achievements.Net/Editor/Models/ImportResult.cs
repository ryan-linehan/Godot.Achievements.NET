#if TOOLS
namespace Godot.Achievements.Core.Editor;

/// <summary>
/// Result of an import operation
/// </summary>
public readonly struct ImportResult
{
    public bool Success { get; }
    public string? ErrorMessage { get; }
    public int ImportedCount { get; }
    public int UpdatedCount { get; }
    public int SkippedCount { get; }

    private ImportResult(bool success, string? errorMessage, int imported, int updated, int skipped)
    {
        Success = success;
        ErrorMessage = errorMessage;
        ImportedCount = imported;
        UpdatedCount = updated;
        SkippedCount = skipped;
    }

    public static ImportResult SuccessResult(int imported, int updated, int skipped)
        => new(true, null, imported, updated, skipped);

    public static ImportResult FailureResult(string errorMessage)
        => new(false, errorMessage, 0, 0, 0);

    public string GetSummary()
        => $"New: {ImportedCount}, Updated: {UpdatedCount}, Skipped: {SkippedCount}";
}
#endif
