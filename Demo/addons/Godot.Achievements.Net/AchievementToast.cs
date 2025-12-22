using System;

namespace Godot.Achievements.Core;

/// <summary>
/// Default toast notification system for achievement unlocks
/// Automatically displays achievement notifications in the top-right corner
/// Add as an autoload to enable toast notifications
/// </summary>
public partial class AchievementToast : CanvasLayer
{
    [Export] public bool Enabled { get; set; } = true;
    [Export] public float DisplayDuration { get; set; } = 5.0f;
    [Export] public Vector2 ToastSize { get; set; } = new Vector2(400, 100);
    [Export] public Color BackgroundColor { get; set; } = new Color(0.1f, 0.1f, 0.1f, 0.9f);
    [Export] public Color TitleColor { get; set; } = new Color(1.0f, 0.84f, 0.0f); // Gold
    [Export] public Color DescriptionColor { get; set; } = Colors.White;

    private PanelContainer? _toastPanel;
    private Label? _titleLabel;
    private Label? _descriptionLabel;
    private TextureRect? _iconRect;
    private Tween? _currentTween;
    private bool _isShowing = false;

    public override void _Ready()
    {
        Layer = 100; // Draw on top of everything

        // Connect to achievement unlocked signal
        var manager = GetNodeOrNull<AchievementManager>("/root/Achievements");
        if (manager != null)
        {
            manager.AchievementUnlocked += OnAchievementUnlocked;
        }
        else
        {
            GD.PushWarning("[AchievementToast] AchievementManager not found. Toasts will not be displayed.");
        }

        // Create toast UI
        _toastPanel = new PanelContainer();
        _toastPanel.CustomMinimumSize = ToastSize;
        AddChild(_toastPanel);

        // Create background style
        var styleBox = new StyleBoxFlat();
        styleBox.BgColor = BackgroundColor;
        styleBox.CornerRadiusTopLeft = 8;
        styleBox.CornerRadiusTopRight = 8;
        styleBox.CornerRadiusBottomLeft = 8;
        styleBox.CornerRadiusBottomRight = 8;
        styleBox.ContentMarginLeft = 16;
        styleBox.ContentMarginRight = 16;
        styleBox.ContentMarginTop = 12;
        styleBox.ContentMarginBottom = 12;
        _toastPanel.AddThemeStyleboxOverride("panel", styleBox);

        // Create horizontal container for icon and text
        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", 12);
        _toastPanel.AddChild(hbox);

        // Create icon
        _iconRect = new TextureRect();
        _iconRect.CustomMinimumSize = new Vector2(64, 64);
        _iconRect.ExpandMode = TextureRect.ExpandModeEnum.FitWidthProportional;
        _iconRect.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        hbox.AddChild(_iconRect);

        // Create text container
        var vbox = new VBoxContainer();
        vbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        hbox.AddChild(vbox);

        // Create title label
        _titleLabel = new Label();
        _titleLabel.AddThemeFontSizeOverride("font_size", 20);
        _titleLabel.AddThemeColorOverride("font_color", TitleColor);
        vbox.AddChild(_titleLabel);

        // Create description label
        _descriptionLabel = new Label();
        _descriptionLabel.AddThemeFontSizeOverride("font_size", 14);
        _descriptionLabel.AddThemeColorOverride("font_color", DescriptionColor);
        _descriptionLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        vbox.AddChild(_descriptionLabel);

        // Position toast off-screen (top-right)
        UpdatePosition(false);

        // Initially hide
        _toastPanel.Modulate = new Color(1, 1, 1, 0);
    }

    /// <summary>
    /// Handler for achievement unlocked signal
    /// </summary>
    private void OnAchievementUnlocked(string achievementId, Achievement achievement)
    {
        if (!Enabled)
            return;

        ShowToast(achievement);
    }

    /// <summary>
    /// Show a toast notification for an achievement
    /// </summary>
    public void ShowToast(Achievement achievement)
    {
        if (!Enabled)
            return;

        if (_isShowing)
        {
            // Queue the toast if one is already showing
            CallDeferred(nameof(ShowToastDeferred), achievement);
            return;
        }

        ShowToastDeferred(achievement);
    }

    private void ShowToastDeferred(Achievement achievement)
    {
        if (_titleLabel == null || _descriptionLabel == null || _iconRect == null || _toastPanel == null)
            return;

        _isShowing = true;

        // Set text content
        _titleLabel.Text = $"🏆 {achievement.DisplayName}";
        _descriptionLabel.Text = achievement.Description;

        // Set icon
        if (achievement.Icon != null)
        {
            _iconRect.Texture = achievement.Icon;
            _iconRect.Visible = true;
        }
        else
        {
            _iconRect.Visible = false;
        }

        // Cancel any existing tween to prevent animation conflicts
        _currentTween?.Kill();

        // Create a parallel tween to animate fade and slide simultaneously
        _currentTween = CreateTween();
        _currentTween.SetParallel(true);

        // Fade in animation: alpha 0 -> 1 over 0.3s
        _currentTween.TweenProperty(_toastPanel, "modulate:a", 1.0f, 0.3f)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.Out);

        // Slide in animation: offscreen (right) -> visible position over 0.3s
        UpdatePosition(false); // Set initial offscreen position
        _currentTween.TweenMethod(Callable.From<bool>(UpdatePosition), false, true, 0.3f)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.Out);

        // Wait for DisplayDuration, then fade out
        // Chain() creates a sequential animation after the parallel animations complete
        _currentTween.Chain()
            .TweenInterval(DisplayDuration);
        _currentTween.TweenProperty(_toastPanel, "modulate:a", 0.0f, 0.3f)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.In);

        _currentTween.Finished += OnToastFinished;
    }

    /// <summary>
    /// Updates the toast panel position based on visibility state
    /// Called by tween animation to smoothly slide toast in from right edge
    /// </summary>
    /// <param name="visible">If true, positions at visible location; if false, positions off-screen to the right</param>
    private void UpdatePosition(bool visible)
    {
        if (_toastPanel == null)
            return;

        var viewport = GetViewport();
        if (viewport == null)
            return;

        var viewportSize = viewport.GetVisibleRect().Size;
        var margin = 20f;

        if (visible)
        {
            // Visible position (top-right, with margin)
            _toastPanel.Position = new Vector2(
                viewportSize.X - ToastSize.X - margin,
                margin
            );
        }
        else
        {
            // Hidden position (off-screen to the right)
            _toastPanel.Position = new Vector2(
                viewportSize.X + 50,
                margin
            );
        }
    }

    /// <summary>
    /// Handler called when toast animation completes
    /// Resets the showing flag to allow next toast to display
    /// </summary>
    private void OnToastFinished()
    {
        _isShowing = false;
    }
}
