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

    private AchievementManager? _achievements;
    private readonly Dictionary<string, Button> _achievementButtons = new();
    private readonly Dictionary<string, HBoxContainer> _incrementalContainers = new();

    public override void _Ready()
    {
        _achievements = GetNodeOrNull<AchievementManager>("/root/Achievements");

        if (_achievements == null)
        {
            GD.PushWarning("AchievementManager not found. Make sure the plugin is enabled.");
            return;
        }

        // Connect to achievement unlocked signal to refresh the lists
        _achievements.AchievementUnlocked += OnAchievementUnlocked;

        // Connect bulk action buttons
        UnlockAllButton.Pressed += OnUnlockAllPressed;
        ResetAllButton.Pressed += OnResetAllPressed;

        // Create buttons for all achievements
        CreateAchievementButtons();

        // Populate achievement lists
        RefreshAchievementLists();
    }

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
    }

    private void CreateAchievementButtons()
    {
        if (_achievements == null) return;

        var allAchievements = _achievements.GetAllAchievements();

        foreach (var achievement in allAchievements)
        {
            // Capture achievement ID for the lambda
            var achievementId = achievement.Id;

            if (achievement.IsIncremental)
            {
                // Create a container for incremental achievements
                var container = new HBoxContainer();
                container.AddThemeConstantOverride("separation", 8);

                var button = new Button();
                button.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                UpdateButtonState(button, achievement);
                button.Pressed += () => OnAchievementButtonPressed(achievementId);

                var incrementButton = new Button();
                incrementButton.Text = "+1";
                incrementButton.TooltipText = "Increment progress";
                incrementButton.Pressed += () => OnIncrementPressed(achievementId);

                container.AddChild(button);
                container.AddChild(incrementButton);
                ButtonsVBox.AddChild(container);

                _achievementButtons[achievement.Id] = button;
                _incrementalContainers[achievement.Id] = container;
            }
            else
            {
                var button = new Button();
                UpdateButtonState(button, achievement);
                button.Pressed += () => OnAchievementButtonPressed(achievementId);

                ButtonsVBox.AddChild(button);
                _achievementButtons[achievement.Id] = button;
            }
        }
    }

    private void UpdateButtonState(Button button, Achievement achievement)
    {
        if (achievement.IsUnlocked)
        {
            button.Text = $"Reset: {achievement.DisplayName}";
        }
        else if (achievement.IsIncremental)
        {
            button.Text = $"Unlock: {achievement.DisplayName} ({achievement.CurrentProgress}/{achievement.MaxProgress})";
        }
        else
        {
            button.Text = $"Unlock: {achievement.DisplayName}";
        }
    }

    private async void OnIncrementPressed(string achievementId)
    {
        if (_achievements == null) return;

        var achievement = _achievements.GetAchievement(achievementId);
        if (achievement == null) return;

        var newProgress = achievement.CurrentProgress + 1;
        await _achievements.SetProgress(achievementId, newProgress);

        RefreshAll();
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
        RefreshAll();
    }

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
}
