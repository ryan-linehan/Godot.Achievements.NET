using Godot.Achievements.Core;

namespace Godot.Achievements.Toast;

/// <summary>
/// Default toast item for displaying achievement unlock notifications.
/// Users can create custom toast scenes by implementing a Control with a Setup(Achievement) method.
/// </summary>
/// <remarks>
/// <para>
/// To create a custom toast scene:
/// 1. Create a new scene with a Control-based root node (e.g., PanelContainer)
/// 2. Add a script that implements a public void Setup(Achievement achievement) method
/// 3. Set the scene path in Project Settings > Addons > Achievements > Toast > Scene Path
/// </para>
/// <para>
/// IMPORTANT: If you want to preview your custom toast in the editor using the "Visualize Unlock"
/// button, your script MUST be marked with the [Tool] attribute. Without this attribute, the toast
/// will only work at runtime.
/// </para>
/// </remarks>
[Tool]
public partial class AchievementToastItem : PanelContainer
{
    [Export]
    private TextureRect IconRect = null!;

    [Export]
    private Label TitleLabel = null!;

    [Export]
    private Label DescriptionLabel = null!;

    /// <summary>
    /// Populates the toast with achievement data.
    /// Custom toast scenes must implement this method.
    /// </summary>
    public void Setup(Achievement achievement)
    {
        var translatedText = Tr("ACHIEVEMENT_UNLOCKED") == "ACHIEVEMENT_UNLOCKED"
            ? "Achievement Unlocked"
            : Tr("ACHIEVEMENT_UNLOCKED");
        TitleLabel.Text = $"{translatedText}: {Tr(achievement.DisplayName ?? string.Empty)}";
        DescriptionLabel.Text = Tr(achievement.Description ?? string.Empty);

        if (achievement.Icon != null)
        {
            IconRect.Texture = achievement.Icon;
            IconRect.Visible = true;
        }
        else
        {
            IconRect.Visible = false;
        }
    }
}
