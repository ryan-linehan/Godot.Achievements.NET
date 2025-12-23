using System.Collections.Generic;

namespace Godot.Achievements.Core.RuntimeDebug;

/// <summary>
/// Runtime debug panel for testing achievements.
/// Shows tabs for each registered provider with achievement status.
/// Allows unlock/reset actions on the Local provider.
/// </summary>
public partial class AchievementDebugPanel : PanelContainer
{
    private const string ListItemScenePath = "res://addons/Godot.Achievements.Net/RuntimeDebug/AchievementDebugListItem.tscn";

    [Export] private TabContainer TabContainer = null!;
    [Export] private Button RefreshButton = null!;
    [Export] private Button CloseButton = null!;

    private PackedScene? _listItemScene;
    private AchievementManager? _achievements;
    private readonly Dictionary<string, VBoxContainer> _providerLists = new();

    public override void _Ready()
    {
        _achievements = GetNodeOrNull<AchievementManager>("/root/Achievements");

        if (_achievements == null)
        {
            GD.PushWarning("[DebugPanel] AchievementManager not found");
            return;
        }

        // Load the list item scene
        if (ResourceLoader.Exists(ListItemScenePath))
        {
            _listItemScene = GD.Load<PackedScene>(ListItemScenePath);
        }
        else
        {
            GD.PushError($"[DebugPanel] List item scene not found: {ListItemScenePath}");
            return;
        }

        // Connect signals
        RefreshButton.Pressed += OnRefreshPressed;
        CloseButton.Pressed += OnClosePressed;
        _achievements.AchievementUnlocked += OnAchievementUnlocked;
        _achievements.AchievementProgressChanged += OnProgressChanged;
        _achievements.ProviderRegistered += OnProviderRegistered;

        // Build initial UI
        BuildProviderTabs();
        RefreshAllTabs();
    }

    public override void _ExitTree()
    {
        RefreshButton.Pressed -= OnRefreshPressed;
        CloseButton.Pressed -= OnClosePressed;

        if (_achievements != null)
        {
            _achievements.AchievementUnlocked -= OnAchievementUnlocked;
            _achievements.AchievementProgressChanged -= OnProgressChanged;
            _achievements.ProviderRegistered -= OnProviderRegistered;
        }
    }

    /// <summary>
    /// Build tabs for each registered provider
    /// </summary>
    private void BuildProviderTabs()
    {
        if (_achievements == null) return;

        // Clear existing tabs
        foreach (var child in TabContainer.GetChildren())
        {
            child.QueueFree();
        }
        _providerLists.Clear();

        // Create tab for each provider
        var providers = _achievements.GetRegisteredProviders();

        foreach (var provider in providers)
        {
            var tabContent = CreateProviderTab(provider);
            TabContainer.AddChild(tabContent);
            _providerLists[provider.ProviderName] = tabContent.GetNode<VBoxContainer>("ScrollContainer/AchievementList");
        }

        // If no providers, show a message
        if (providers.Count == 0)
        {
            var emptyLabel = new Label
            {
                Text = "No providers registered.\nMake sure AchievementManager is set up correctly.",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Name = "NoProviders"
            };
            TabContainer.AddChild(emptyLabel);
        }
    }

    /// <summary>
    /// Create the tab content for a provider
    /// </summary>
    private VBoxContainer CreateProviderTab(IAchievementProvider provider)
    {
        var tabRoot = new VBoxContainer
        {
            Name = provider.ProviderName
        };

        // Status header
        var statusHeader = new HBoxContainer { Name = "StatusHeader" };
        tabRoot.AddChild(statusHeader);

        var statusLabel = new Label
        {
            Text = "Status: ",
            Name = "StatusLabelPrefix"
        };
        statusHeader.AddChild(statusLabel);

        var statusValue = new Label
        {
            Name = "StatusValue",
            Text = provider.IsAvailable ? "Available" : "Unavailable",
            Modulate = provider.IsAvailable ? Colors.Green : Colors.Red
        };
        statusHeader.AddChild(statusValue);

        // Provider name for Local shows it can unlock
        if (provider.ProviderName == "Local")
        {
            var actionHint = new Label
            {
                Text = " (Can unlock/reset achievements)",
                Modulate = Colors.Yellow
            };
            statusHeader.AddChild(actionHint);
        }

        // Separator
        var separator = new HSeparator();
        tabRoot.AddChild(separator);

        // Scroll container for achievements
        var scroll = new ScrollContainer
        {
            Name = "ScrollContainer",
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollModeEnum.Disabled
        };
        tabRoot.AddChild(scroll);

        // Achievement list container
        var achievementList = new VBoxContainer
        {
            Name = "AchievementList",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        achievementList.AddThemeConstantOverride("separation", 4);
        scroll.AddChild(achievementList);

        return tabRoot;
    }

    /// <summary>
    /// Refresh all provider tabs with current achievement data
    /// </summary>
    private void RefreshAllTabs()
    {
        if (_achievements == null || _listItemScene == null) return;

        var providers = _achievements.GetRegisteredProviders();
        var allAchievements = _achievements.GetAllAchievements();

        foreach (var provider in providers)
        {
            if (!_providerLists.TryGetValue(provider.ProviderName, out var list))
                continue;

            // Clear existing items
            foreach (var child in list.GetChildren())
            {
                child.QueueFree();
            }

            // Add achievement items
            // Only Local provider can perform actions
            var canPerformActions = provider.ProviderName == "Local";

            foreach (var achievement in allAchievements)
            {
                var item = _listItemScene.Instantiate<AchievementDebugListItem>();
                item.Setup(achievement, canPerformActions);

                if (canPerformActions)
                {
                    item.ActionRequested += OnItemActionRequested;
                }

                list.AddChild(item);
            }

            // Update status label
            var tabRoot = list.GetParent().GetParent() as VBoxContainer;
            var statusValue = tabRoot?.GetNodeOrNull<Label>("StatusHeader/StatusValue");
            if (statusValue != null)
            {
                statusValue.Text = provider.IsAvailable ? "Available" : "Unavailable";
                statusValue.Modulate = provider.IsAvailable ? Colors.Green : Colors.Red;
            }
        }
    }

    /// <summary>
    /// Refresh a specific tab
    /// </summary>
    private void RefreshTab(string providerName)
    {
        if (_achievements == null || _listItemScene == null) return;

        if (!_providerLists.TryGetValue(providerName, out var list))
            return;

        var allAchievements = _achievements.GetAllAchievements();
        var canPerformActions = providerName == "Local";

        // Clear existing items
        foreach (var child in list.GetChildren())
        {
            if (child is AchievementDebugListItem item)
            {
                item.ActionRequested -= OnItemActionRequested;
            }
            child.QueueFree();
        }

        // Add achievement items
        foreach (var achievement in allAchievements)
        {
            var item = _listItemScene.Instantiate<AchievementDebugListItem>();
            item.Setup(achievement, canPerformActions);

            if (canPerformActions)
            {
                item.ActionRequested += OnItemActionRequested;
            }

            list.AddChild(item);
        }
    }

    #region Event Handlers

    private void OnRefreshPressed()
    {
        BuildProviderTabs();
        RefreshAllTabs();
        GD.Print("[DebugPanel] Refreshed all tabs");
    }

    private void OnClosePressed()
    {
        Hide();
    }

    private async void OnItemActionRequested(string achievementId, bool isUnlock)
    {
        if (_achievements == null) return;

        if (isUnlock)
        {
            GD.Print($"[DebugPanel] Unlocking achievement: {achievementId}");
            await _achievements.Unlock(achievementId);
        }
        else
        {
            GD.Print($"[DebugPanel] Resetting achievement: {achievementId}");
            await _achievements.ResetAchievement(achievementId);
        }

        // Refresh the Local tab to show updated state
        RefreshTab("Local");
    }

    private void OnAchievementUnlocked(string achievementId, Achievement achievement)
    {
        // Refresh to show updated state
        RefreshAllTabs();
    }

    private void OnProgressChanged(string achievementId, int currentProgress, int maxProgress)
    {
        // Refresh to show updated progress
        RefreshAllTabs();
    }

    private void OnProviderRegistered(string providerName)
    {
        // Rebuild tabs when new provider registers
        BuildProviderTabs();
        RefreshAllTabs();
    }

    #endregion

    #region Public API

    /// <summary>
    /// Show the debug panel
    /// </summary>
    public void ShowPanel()
    {
        Show();
        RefreshAllTabs();
    }

    /// <summary>
    /// Toggle visibility of the debug panel
    /// </summary>
    public void TogglePanel()
    {
        if (Visible)
        {
            Hide();
        }
        else
        {
            ShowPanel();
        }
    }

    #endregion
}
