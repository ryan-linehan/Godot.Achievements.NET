using System.Collections.Generic;
using Godot.Achievements.Core;

namespace Godot.Achievements.Toast;

/// <summary>
/// Container that manages multiple achievement toast notifications.
/// Handles positioning, animations, and lifecycle of toast items.
/// </summary>
public partial class AchievementToastContainer : CanvasLayer
{
    [Export]
    private MarginContainer MarginContainer = null!;

    [Export]
    private VBoxContainer ToastVBox = null!;

    private string _toastScenePath = AchievementSettings.DefaultToastScenePath;
    private ToastPosition _position = ToastPosition.TopRight;
    private float _displayDuration = AchievementSettings.DefaultToastDisplayDuration;
    private PackedScene? _toastScene;
    private readonly List<ToastEntry> _activeToasts = new();
    private AudioStreamPlayer? _audioPlayer;
    private AudioStream? _unlockSound;

    private class ToastEntry
    {
        public Control Toast { get; set; } = null!;
        public Tween? Tween { get; set; }
    }

    public override void _Ready()
    {
        Layer = 100; // Draw on top of everything

        LoadSettings();

        // If scene path is empty, toast system is disabled
        if (string.IsNullOrEmpty(_toastScenePath))
        {
            AchievementLogger.Log(AchievementLogger.Areas.Toast, "Toast system disabled (empty scene path).");
            return;
        }

        // Load the toast scene
        if (!ResourceLoader.Exists(_toastScenePath))
        {
            AchievementLogger.Error(AchievementLogger.Areas.Toast, $"Toast scene not found: {_toastScenePath}");
            return;
        }

        _toastScene = GD.Load<PackedScene>(_toastScenePath);
        if (_toastScene == null)
        {
            AchievementLogger.Error(AchievementLogger.Areas.Toast, $"Failed to load toast scene: {_toastScenePath}");
            return;
        }

        // Apply position to container
        ApplyPosition();

        // Connect to achievement manager
        var manager = GetNodeOrNull<AchievementManager>("/root/Achievements");
        if (manager != null)
        {
            manager.AchievementUnlocked += OnAchievementUnlocked;
        }
        else
        {
            AchievementLogger.Warning(AchievementLogger.Areas.Toast, "AchievementManager not found. Toasts will not be displayed.");
        }
    }

    private void LoadSettings()
    {
        if (ProjectSettings.HasSetting(AchievementSettings.ToastScenePath))
        {
            _toastScenePath = ProjectSettings.GetSetting(AchievementSettings.ToastScenePath).AsString();
        }

        if (ProjectSettings.HasSetting(AchievementSettings.ToastPosition))
        {
            _position = (ToastPosition)ProjectSettings.GetSetting(AchievementSettings.ToastPosition).AsInt32();
        }

        if (ProjectSettings.HasSetting(AchievementSettings.ToastDisplayDuration))
        {
            _displayDuration = (float)ProjectSettings.GetSetting(AchievementSettings.ToastDisplayDuration).AsDouble();
        }

        // Load unlock sound if configured
        if (ProjectSettings.HasSetting(AchievementSettings.ToastUnlockSound))
        {
            var soundPath = ProjectSettings.GetSetting(AchievementSettings.ToastUnlockSound).AsString();
            if (!string.IsNullOrEmpty(soundPath) && ResourceLoader.Exists(soundPath))
            {
                _unlockSound = GD.Load<AudioStream>(soundPath);
                if (_unlockSound != null)
                {
                    _audioPlayer = new AudioStreamPlayer();
                    _audioPlayer.Stream = _unlockSound;
                    AddChild(_audioPlayer);
                }
            }
        }
    }

    private void ApplyPosition()
    {
        // Reset anchors and margins
        MarginContainer.AnchorLeft = 0;
        MarginContainer.AnchorTop = 0;
        MarginContainer.AnchorRight = 0;
        MarginContainer.AnchorBottom = 0;
        MarginContainer.OffsetLeft = 0;
        MarginContainer.OffsetTop = 0;
        MarginContainer.OffsetRight = 0;
        MarginContainer.OffsetBottom = 0;

        const float margin = 20f;

        switch (_position)
        {
            case ToastPosition.TopLeft:
                MarginContainer.AnchorLeft = 0;
                MarginContainer.AnchorTop = 0;
                MarginContainer.OffsetLeft = margin;
                MarginContainer.OffsetTop = margin;
                MarginContainer.GrowHorizontal = Control.GrowDirection.End;
                MarginContainer.GrowVertical = Control.GrowDirection.End;
                break;

            case ToastPosition.TopCenter:
                MarginContainer.AnchorLeft = 0.5f;
                MarginContainer.AnchorTop = 0;
                MarginContainer.OffsetTop = margin;
                MarginContainer.GrowHorizontal = Control.GrowDirection.Both;
                MarginContainer.GrowVertical = Control.GrowDirection.End;
                break;

            case ToastPosition.TopRight:
                MarginContainer.AnchorLeft = 1;
                MarginContainer.AnchorTop = 0;
                MarginContainer.OffsetRight = -margin;
                MarginContainer.OffsetTop = margin;
                MarginContainer.GrowHorizontal = Control.GrowDirection.Begin;
                MarginContainer.GrowVertical = Control.GrowDirection.End;
                break;

            case ToastPosition.BottomLeft:
                MarginContainer.AnchorLeft = 0;
                MarginContainer.AnchorTop = 1;
                MarginContainer.OffsetLeft = margin;
                MarginContainer.OffsetBottom = -margin;
                MarginContainer.GrowHorizontal = Control.GrowDirection.End;
                MarginContainer.GrowVertical = Control.GrowDirection.Begin;
                break;

            case ToastPosition.BottomCenter:
                MarginContainer.AnchorLeft = 0.5f;
                MarginContainer.AnchorTop = 1;
                MarginContainer.OffsetBottom = -margin;
                MarginContainer.GrowHorizontal = Control.GrowDirection.Both;
                MarginContainer.GrowVertical = Control.GrowDirection.Begin;
                break;

            case ToastPosition.BottomRight:
                MarginContainer.AnchorLeft = 1;
                MarginContainer.AnchorTop = 1;
                MarginContainer.OffsetRight = -margin;
                MarginContainer.OffsetBottom = -margin;
                MarginContainer.GrowHorizontal = Control.GrowDirection.Begin;
                MarginContainer.GrowVertical = Control.GrowDirection.Begin;
                break;
        }
    }

    private void OnAchievementUnlocked(string achievementId, Achievement achievement)
    {
        // Play unlock sound if configured
        _audioPlayer?.Play();

        ShowToast(achievement);
    }

    /// <summary>
    /// Show a toast notification for an achievement
    /// </summary>
    public void ShowToast(Achievement achievement)
    {
        if (_toastScene == null) return;

        var toast = _toastScene.Instantiate<Control>();
        if (toast == null)
        {
            AchievementLogger.Error(AchievementLogger.Areas.Toast, "Failed to instantiate toast scene.");
            return;
        }

        // Call Setup on the toast via interface or fallback to reflection
        if (toast is IAchievementToastItem toastItem)
        {
            toastItem.Setup(achievement);
        }
        else if (toast.HasMethod("Setup"))
        {
            // Fallback for legacy custom toasts that don't implement the interface
            toast.Call("Setup", achievement);
        }
        else
        {
            AchievementLogger.Warning(AchievementLogger.Areas.Toast, $"Toast scene does not implement {nameof(IAchievementToastItem)} or have a Setup method.");
        }

        // For bottom positions, add at the beginning so newest appears at bottom
        bool isBottomPosition = _position >= ToastPosition.BottomLeft;
        if (isBottomPosition)
        {
            ToastVBox.AddChild(toast);
            ToastVBox.MoveChild(toast, 0);
        }
        else
        {
            ToastVBox.AddChild(toast);
        }

        // Start hidden
        toast.Modulate = new Color(1, 1, 1, 0);

        var entry = new ToastEntry { Toast = toast };
        _activeToasts.Add(entry);

        // Animate in
        var tween = CreateTween();
        entry.Tween = tween;

        // Fade in
        tween.TweenProperty(toast, "modulate:a", 1.0f, 0.3f)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.Out);

        // Wait for display duration
        tween.TweenInterval(_displayDuration);

        // Fade out
        tween.TweenProperty(toast, "modulate:a", 0.0f, 0.3f)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.In);

        // Remove when done
        tween.TweenCallback(Callable.From(() => RemoveToast(entry)));
    }

    private void RemoveToast(ToastEntry entry)
    {
        if (!_activeToasts.Contains(entry)) return;

        _activeToasts.Remove(entry);
        entry.Tween?.Kill();
        entry.Toast.QueueFree();

        // Animate remaining toasts to slide into place
        // The VBoxContainer handles repositioning automatically,
        // but we can add a smooth transition by tweening the container's children
        AnimateRemainingToasts();
    }

    private void AnimateRemainingToasts()
    {
        // The VBoxContainer automatically repositions children when one is removed.
        // For smooth sliding, we could track positions and animate, but the VBox
        // handles this well enough for most cases. If more sophisticated animation
        // is needed, we'd need to manually position children outside the VBox.
        // For now, the fade-out of the removed toast provides visual feedback.
    }

    public override void _ExitTree()
    {
        // Clean up all active toasts
        foreach (var entry in _activeToasts)
        {
            entry.Tween?.Kill();
        }
        _activeToasts.Clear();
    }
}
