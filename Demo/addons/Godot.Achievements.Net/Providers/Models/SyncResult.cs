namespace Godot.Achievements.Providers;

/// <summary>
/// Result of a sync operation (progress update, reset, etc.)
/// </summary>
public readonly struct SyncResult
{
    public bool Success { get; init; }
    public string? Message { get; init; }

    public static SyncResult SuccessResult(string? message = null) =>
        new() { Success = true, Message = message };

    public static SyncResult FailureResult(string message) =>
        new() { Success = false, Message = message };

    /// <summary>
    /// Implicit conversion to bool for easy success checks
    /// </summary>
    public static implicit operator bool(SyncResult result) => result.Success;
}
