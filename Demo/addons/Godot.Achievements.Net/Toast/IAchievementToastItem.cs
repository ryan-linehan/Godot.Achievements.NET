using Godot.Achievements.Core;

namespace Godot.Achievements.Toast;

/// <summary>
/// Interface for achievement toast items.
/// Implement this interface on your custom toast Control to receive achievement data.
/// </summary>
/// <remarks>
/// <para>
/// To create a custom toast scene:
/// 1. Create a new scene with a Control-based root node (e.g., PanelContainer)
/// 2. Add a script that implements this interface
/// 3. Set the scene path in Project Settings > Addons > Achievements > Toast > Scene Path
/// </para>
/// <para>
/// IMPORTANT: If you want to preview your custom toast in the editor using the "Visualize Unlock"
/// button, your script MUST be marked with the [Tool] attribute. Without this attribute, the toast
/// will only work at runtime.
/// </para>
/// </remarks>
public interface IAchievementToastItem
{
    /// <summary>
    /// Called when the toast is displayed to populate it with achievement data.
    /// </summary>
    /// <param name="achievement">The achievement that was unlocked.</param>
    void Setup(Achievement achievement);
}
