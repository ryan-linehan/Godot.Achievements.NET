using Godot.Achievements.Core;

namespace Godot.Achievements.Toast;

/// <summary>
/// Default toast item for displaying achievement unlock notifications.
/// Users can create custom toast scenes by implementing <see cref="IAchievementToastItem"/>.
/// </summary>
[Tool]
public partial class AchievementToastItem : PanelContainer, IAchievementToastItem
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
