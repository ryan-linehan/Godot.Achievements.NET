using Godot;
using Godot.Achievements.Core;
using System.Collections.Generic;

public partial class Main : CanvasLayer
{
    [Export] private Button UnlockAllButton = null!;
    [Export] private Button ResetAllButton = null!;
    [Export] private VBoxContainer ButtonsVBox = null!;
    [Export] private VBoxContainer CompletedVBox = null!;
    [Export] private VBoxContainer NotCompletedVBox = null!;

    // Progress Example UI
    [Export] private OptionButton ProgressAchievementSelect = null!;
    [Export] private SpinBox ProgressSpinBox = null!;
    [Export] private Button SetProgressButton = null!;
    [Export] private Button IncrementButton = null!;
    [Export] private Label CurrentProgressLabel = null!;

    private AchievementManager? _achievements;
    private readonly Dictionary<string, Button> _achievementButtons = new();

    public override void _Ready()
    {
        _achievements = GetNodeOrNull<AchievementManager>("/root/Achievements");

        if (_achievements == null)
        {
            GD.PushWarning("AchievementManager not found. Make sure the plugin is enabled.");
            return;
        }

        // Connect to achievement signals
        _achievements.AchievementUnlocked += OnAchievementUnlocked;
        _achievements.AchievementProgressChanged += OnAchievementProgressChanged;

        // Connect bulk action buttons
        UnlockAllButton.Pressed += OnUnlockAllPressed;
        ResetAllButton.Pressed += OnResetAllPressed;

        // Connect progress example buttons
        if (SetProgressButton != null)
            SetProgressButton.Pressed += OnSetProgressPressed;
        if (IncrementButton != null)
            IncrementButton.Pressed += OnIncrementPressed;
        if (ProgressAchievementSelect != null)
            ProgressAchievementSelect.ItemSelected += OnProgressAchievementSelected;

        // Create buttons for all achievements
        CreateAchievementButtons();

        // Populate achievement lists and progress dropdown
        RefreshAchievementLists();
        PopulateProgressDropdown();
    }

    public override void _ExitTree()
    {
        if (_achievements != null)
        {
            _achievements.AchievementUnlocked -= OnAchievementUnlocked;
            _achievements.AchievementProgressChanged -= OnAchievementProgressChanged;
        }
    }

    #region Progress Example

    /// <summary>
    /// Populate the dropdown with all incremental achievements
    /// </summary>
    private void PopulateProgressDropdown()
    {
        if (_achievements == null || ProgressAchievementSelect == null) return;

        ProgressAchievementSelect.Clear();

        var allAchievements = _achievements.GetAllAchievements();
        foreach (var achievement in allAchievements)
        {
            // Add all achievements, but highlight incremental ones
            var label = achievement.IsIncremental
                ? $"{achievement.DisplayName} (0/{achievement.MaxProgress})"
                : $"{achievement.DisplayName}";
            ProgressAchievementSelect.AddItem(label);
            ProgressAchievementSelect.SetItemMetadata(ProgressAchievementSelect.ItemCount - 1, achievement.Id);
        }

        if (ProgressAchievementSelect.ItemCount > 0)
        {
            ProgressAchievementSelect.Selected = 0;
            UpdateProgressDisplay();
        }
    }

    /// <summary>
    /// Update the progress display for the selected achievement
    /// </summary>
    private void UpdateProgressDisplay()
    {
        if (_achievements == null || ProgressAchievementSelect == null) return;
        if (ProgressAchievementSelect.Selected < 0) return;

        var achievementId = ProgressAchievementSelect.GetItemMetadata(ProgressAchievementSelect.Selected).AsString();
        var achievement = _achievements.GetAchievement(achievementId);

        if (achievement == null) return;

        // Update the dropdown text to show current progress
        var label = achievement.IsIncremental
            ? $"{achievement.DisplayName} ({achievement.CurrentProgress}/{achievement.MaxProgress})"
            : $"{achievement.DisplayName}";
        ProgressAchievementSelect.SetItemText(ProgressAchievementSelect.Selected, label);

        // Update progress label
        if (CurrentProgressLabel != null)
        {
            if (achievement.IsIncremental)
            {
                var percentage = achievement.MaxProgress > 0
                    ? (float)achievement.CurrentProgress / achievement.MaxProgress * 100f
                    : 0f;
                CurrentProgressLabel.Text = $"Progress: {achievement.CurrentProgress}/{achievement.MaxProgress} ({percentage:F0}%)";
            }
            else
            {
                CurrentProgressLabel.Text = achievement.IsUnlocked ? "Status: Unlocked" : "Status: Locked";
            }
        }

        // Update spinbox max value
        if (ProgressSpinBox != null && achievement.IsIncremental)
        {
            ProgressSpinBox.MaxValue = achievement.MaxProgress;
            ProgressSpinBox.Value = achievement.CurrentProgress;
        }
    }

    private void OnProgressAchievementSelected(long index)
    {
        UpdateProgressDisplay();
    }

    /// <summary>
    /// Set the progress to the exact value in the spinbox
    /// </summary>
    private async void OnSetProgressPressed()
    {
        if (_achievements == null || ProgressAchievementSelect == null || ProgressSpinBox == null) return;
        if (ProgressAchievementSelect.Selected < 0) return;

        var achievementId = ProgressAchievementSelect.GetItemMetadata(ProgressAchievementSelect.Selected).AsString();
        var newProgress = (int)ProgressSpinBox.Value;

        GD.Print($"[Demo] Setting progress for '{achievementId}' to {newProgress}");
        await _achievements.SetProgress(achievementId, newProgress);

        UpdateProgressDisplay();
        RefreshAll();
    }

    /// <summary>
    /// Increment the progress by 1 - demonstrates typical game usage
    /// </summary>
    private async void OnIncrementPressed()
    {
        if (_achievements == null || ProgressAchievementSelect == null) return;
        if (ProgressAchievementSelect.Selected < 0) return;

        var achievementId = ProgressAchievementSelect.GetItemMetadata(ProgressAchievementSelect.Selected).AsString();
        var achievement = _achievements.GetAchievement(achievementId);

        if (achievement == null) return;

        // Increment by 1 (typical game usage pattern)
        var newProgress = achievement.CurrentProgress + 1;

        GD.Print($"[Demo] Incrementing '{achievementId}' from {achievement.CurrentProgress} to {newProgress}");
        await _achievements.SetProgress(achievementId, newProgress);

        UpdateProgressDisplay();
        RefreshAll();
    }

    private void OnAchievementProgressChanged(string achievementId, int currentProgress, int maxProgress)
    {
        GD.Print($"[Demo] Progress changed for '{achievementId}': {currentProgress}/{maxProgress}");
        UpdateProgressDisplay();
    }

    #endregion

    #region Bulk Actions

    private async void OnUnlockAllPressed()
    {
        if (_achievements == null) return;

        var allAchievements = _achievements.GetAllAchievements();
        foreach (var achievement in allAchievements)
        {
            if (!achievement.IsUnlocked)
            {
                await _achievements.Unlock(achievement.Id);
            }
        }

        RefreshAll();
    }

    private async void OnResetAllPressed()
    {
        if (_achievements == null) return;

        await _achievements.ResetAllAchievements();
        RefreshAll();
        PopulateProgressDropdown(); // Refresh dropdown to show reset progress
    }

    #endregion

    #region Achievement Buttons

    private void CreateAchievementButtons()
    {
        if (_achievements == null) return;

        var allAchievements = _achievements.GetAllAchievements();

        foreach (var achievement in allAchievements)
        {
            var button = new Button();
            UpdateButtonState(button, achievement);

            // Capture achievement ID for the lambda
            var achievementId = achievement.Id;
            button.Pressed += () => OnAchievementButtonPressed(achievementId);

            ButtonsVBox.AddChild(button);
            _achievementButtons[achievement.Id] = button;
        }
    }

    private void UpdateButtonState(Button button, Achievement achievement)
    {
        if (achievement.IsUnlocked)
        {
            button.Text = $"Reset: {achievement.DisplayName}";
        }
        else if (achievement.IsIncremental && achievement.CurrentProgress > 0)
        {
            button.Text = $"Unlock: {achievement.DisplayName} ({achievement.CurrentProgress}/{achievement.MaxProgress})";
        }
        else
        {
            button.Text = $"Unlock: {achievement.DisplayName}";
        }
    }

    private async void OnAchievementButtonPressed(string achievementId)
    {
        if (_achievements == null) return;

        var achievement = _achievements.GetAchievement(achievementId);
        if (achievement == null) return;

        if (achievement.IsUnlocked)
        {
            await _achievements.ResetAchievement(achievementId);
        }
        else
        {
            await _achievements.Unlock(achievementId);
        }

        // Refresh button state and lists
        RefreshAll();
    }

    private void OnAchievementUnlocked(string achievementId, Achievement achievement)
    {
        GD.Print($"[Demo] Achievement unlocked: {achievement.DisplayName}");
        RefreshAll();
    }

    #endregion

    #region Refresh UI

    private void RefreshAll()
    {
        RefreshButtonStates();
        RefreshAchievementLists();
    }

    private void RefreshButtonStates()
    {
        if (_achievements == null) return;

        foreach (var kvp in _achievementButtons)
        {
            var achievement = _achievements.GetAchievement(kvp.Key);
            if (achievement != null)
            {
                UpdateButtonState(kvp.Value, achievement);
            }
        }
    }

    private void RefreshAchievementLists()
    {
        if (_achievements == null) return;

        // Clear existing children
        foreach (var child in CompletedVBox.GetChildren())
            child.QueueFree();
        foreach (var child in NotCompletedVBox.GetChildren())
            child.QueueFree();

        // Get all achievements and sort into completed/not completed
        var allAchievements = _achievements.GetAllAchievements();

        foreach (var achievement in allAchievements)
        {
            var label = new Label();

            if (achievement.IsIncremental && !achievement.IsUnlocked)
            {
                label.Text = $"{achievement.DisplayName} ({achievement.CurrentProgress}/{achievement.MaxProgress})";
            }
            else
            {
                label.Text = achievement.DisplayName;
            }

            if (achievement.IsUnlocked)
                CompletedVBox.AddChild(label);
            else
                NotCompletedVBox.AddChild(label);
        }
    }

    #endregion
}
