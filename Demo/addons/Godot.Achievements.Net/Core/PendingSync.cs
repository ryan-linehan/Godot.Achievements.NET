namespace Godot.Achievements.Core;

/// <summary>
/// Represents a pending sync operation to be retried
/// </summary>
internal class PendingSync
{
    public required string AchievementId { get; init; }
    public required IAchievementProvider Provider { get; init; }
    public required SyncType Type { get; init; }
    public int CurrentProgress { get; init; }

    /// <summary>
    /// Number of times this sync has been attempted
    /// </summary>
    public int RetryCount { get; set; }
}
