using System;

namespace Godot.Achievements.Core.RuntimeDebug;

/// <summary>
/// A single achievement item in the debug panel list.
/// Displays achievement status and allows unlock/reset actions.
/// </summary>
public partial class AchievementDebugListItem : PanelContainer
{
    [Signal]
    public delegate void ActionRequestedEventHandler(string achievementId, bool isUnlock);

    [Export] private TextureRect IconRect = null!;
    [Export] private Label NameLabel = null!;
    [Export] private Label StatusLabel = null!;
    [Export] private ProgressBar ProgressBar = null!;
    [Export] private Button ActionButton = null!;

    private string _achievementId = string.Empty;
    private bool _isUnlocked;
    private bool _canPerformActions;

    public override void _Ready()
    {
        ActionButton.Pressed += OnActionButtonPressed;
    }

    public override void _ExitTree()
    {
        ActionButton.Pressed -= OnActionButtonPressed;
    }

    /// <summary>
    /// Setup the list item with achievement data
    /// </summary>
    /// <param name="achievement">The achievement to display</param>
    /// <param name="canPerformActions">Whether actions (unlock/reset) are allowed on this provider</param>
    public void Setup(Achievement achievement, bool canPerformActions)
    {
        _achievementId = achievement.Id;
        _isUnlocked = achievement.IsUnlocked;
        _canPerformActions = canPerformActions;

        // Set icon
        if (achievement.Icon != null)
        {
            IconRect.Texture = achievement.Icon;
        }

        // Set name
        NameLabel.Text = achievement.DisplayName;

        // Set status based on unlock state
        if (achievement.IsUnlocked)
        {
            StatusLabel.Text = achievement.UnlockedAt.HasValue
                ? $"Unlocked: {achievement.UnlockedAt.Value:g}"
                : "Unlocked";
            StatusLabel.Modulate = Colors.Green;
            ProgressBar.Visible = false;
        }
        else if (achievement.IsIncremental && achievement.CurrentProgress > 0)
        {
            StatusLabel.Text = $"Progress: {achievement.CurrentProgress}/{achievement.MaxProgress}";
            StatusLabel.Modulate = Colors.Yellow;
            ProgressBar.Visible = true;
            ProgressBar.MaxValue = achievement.MaxProgress;
            ProgressBar.Value = achievement.CurrentProgress;
        }
        else
        {
            StatusLabel.Text = "Locked";
            StatusLabel.Modulate = new Color(0.7f, 0.7f, 0.7f);
            ProgressBar.Visible = false;
        }

        // Configure action button
        if (canPerformActions)
        {
            ActionButton.Visible = true;
            ActionButton.Text = achievement.IsUnlocked ? "Reset" : "Unlock";
            ActionButton.Disabled = false;
        }
        else
        {
            ActionButton.Visible = true;
            ActionButton.Text = achievement.IsUnlocked ? "Synced" : "Not Synced";
            ActionButton.Disabled = true;
        }

        // Dim the item if locked and no actions available
        Modulate = achievement.IsUnlocked ? Colors.White : new Color(0.9f, 0.9f, 0.9f);
    }

    private void OnActionButtonPressed()
    {
        if (!_canPerformActions) return;

        // Emit signal to request action (unlock if locked, reset if unlocked)
        EmitSignal(SignalName.ActionRequested, _achievementId, !_isUnlocked);
    }
}
