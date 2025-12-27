using System.Threading.Tasks;
using Godot.Achievements.Core;

namespace Godot.Achievements.Providers;

/// <summary>
/// Abstract base class for achievement providers that provides Godot signal support.
/// Providers inherit from RefCounted to enable Godot signals for sync operation results.
/// </summary>
public abstract partial class AchievementProviderBase : RefCounted, IAchievementProvider
{
    #region Signals

    /// <summary>
    /// Emitted when an achievement unlock operation completes.
    /// </summary>
    /// <param name="achievementId">The achievement ID that was unlocked</param>
    /// <param name="success">Whether the operation succeeded</param>
    /// <param name="errorMessage">Error message if failed, empty string if successful</param>
    [Signal]
    public delegate void AchievementUnlockedEventHandler(string achievementId, bool success, string errorMessage);

    /// <summary>
    /// Emitted when a progress increment operation completes.
    /// </summary>
    /// <param name="achievementId">The achievement ID that was updated</param>
    /// <param name="newProgress">The new progress value after the increment</param>
    /// <param name="success">Whether the operation succeeded</param>
    /// <param name="errorMessage">Error message if failed, empty string if successful</param>
    [Signal]
    public delegate void ProgressIncrementedEventHandler(string achievementId, int newProgress, bool success, string errorMessage);

    /// <summary>
    /// Emitted when an achievement reset operation completes.
    /// </summary>
    /// <param name="achievementId">The achievement ID that was reset</param>
    /// <param name="success">Whether the operation succeeded</param>
    /// <param name="errorMessage">Error message if failed, empty string if successful</param>
    [Signal]
    public delegate void AchievementResetEventHandler(string achievementId, bool success, string errorMessage);

    /// <summary>
    /// Emitted when a reset all achievements operation completes.
    /// </summary>
    /// <param name="success">Whether the operation succeeded</param>
    /// <param name="errorMessage">Error message if failed, empty string if successful</param>
    [Signal]
    public delegate void AllAchievementsResetEventHandler(bool success, string errorMessage);

    #endregion

    #region Signal Helper Methods

    /// <summary>
    /// Emit the AchievementUnlocked signal.
    /// </summary>
    protected void EmitAchievementUnlocked(string achievementId, bool success, string? errorMessage = null)
        => EmitSignal(SignalName.AchievementUnlocked, achievementId, success, errorMessage ?? "");

    /// <summary>
    /// Emit the ProgressIncremented signal.
    /// </summary>
    protected void EmitProgressIncremented(string achievementId, int newProgress, bool success, string? errorMessage = null)
        => EmitSignal(SignalName.ProgressIncremented, achievementId, newProgress, success, errorMessage ?? "");

    /// <summary>
    /// Emit the AchievementReset signal.
    /// </summary>
    protected void EmitAchievementReset(string achievementId, bool success, string? errorMessage = null)
        => EmitSignal(SignalName.AchievementReset, achievementId, success, errorMessage ?? "");

    /// <summary>
    /// Emit the AllAchievementsReset signal.
    /// </summary>
    protected void EmitAllAchievementsReset(bool success, string? errorMessage = null)
        => EmitSignal(SignalName.AllAchievementsReset, success, errorMessage ?? "");

    #endregion

    #region IAchievementProvider - Abstract Members

    /// <inheritdoc />
    public abstract string ProviderName { get; }

    /// <inheritdoc />
    public abstract bool IsAvailable { get; }

    /// <inheritdoc />
    public abstract void UnlockAchievement(string achievementId);

    /// <inheritdoc />
    public abstract void IncrementProgress(string achievementId, int amount);

    /// <inheritdoc />
    public abstract void ResetAchievement(string achievementId);

    /// <inheritdoc />
    public abstract void ResetAllAchievements();

    /// <inheritdoc />
    public abstract Task<AchievementUnlockResult> UnlockAchievementAsync(string achievementId);

    /// <inheritdoc />
    public abstract Task<int> GetProgressAsync(string achievementId);

    /// <inheritdoc />
    public abstract Task<SyncResult> IncrementProgressAsync(string achievementId, int amount);

    /// <inheritdoc />
    public abstract Task<SyncResult> ResetAchievementAsync(string achievementId);

    /// <inheritdoc />
    public abstract Task<SyncResult> ResetAllAchievementsAsync();

    #endregion
}
