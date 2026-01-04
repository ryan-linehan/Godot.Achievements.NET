#if TOOLS
using System.Collections.Generic;
using Godot.Achievements.Core;
using Godot.Achievements.Toast;

namespace Godot.Achievements.Core.Editor;

/// <summary>
/// Editor-only toast preview system for visualizing achievement unlocks in the editor.
/// This is separate from the runtime AchievementToastContainer to maintain separation of concerns.
/// </summary>
[Tool]
public partial class EditorToastPreview : CanvasLayer
{
    private MarginContainer? _marginContainer;
    private VBoxContainer? _toastVBox;
    private AudioStreamPlayer? _audioPlayer;

    private string _toastScenePath = AchievementSettings.DefaultToastScenePath;
    private ToastPosition _position = ToastPosition.TopRight;
    private float _displayDuration = AchievementSettings.DefaultToastDisplayDuration;
    private string _unlockSoundPath = string.Empty;
    private AudioStream? _unlockSound;
    private PackedScene? _toastScene;
    private readonly List<ToastEntry> _activeToasts = new();

    private class ToastEntry
    {
        public Control Toast { get; set; } = null!;
        public Tween? Tween { get; set; }
    }

    public override void _Ready()
    {
        Layer = 100; // Draw on top of everything

        LoadSettings();

        // Load the toast scene
        if (string.IsNullOrEmpty(_toastScenePath) || !ResourceLoader.Exists(_toastScenePath))
        {
            // Fall back to default if custom path doesn't exist
            _toastScenePath = AchievementSettings.DefaultToastScenePath;
        }

        _toastScene = GD.Load<PackedScene>(_toastScenePath);
        if (_toastScene == null)
        {
            AchievementLogger.Error(AchievementLogger.Areas.Editor, $"Failed to load toast scene: {_toastScenePath}");
            return;
        }

        // Create the container hierarchy programmatically
        CreateContainerHierarchy();
        ApplyPosition();
    }

    private void CreateContainerHierarchy()
    {
        _marginContainer = new MarginContainer();
        AddChild(_marginContainer);

        _toastVBox = new VBoxContainer();
        _toastVBox.AddThemeConstantOverride("separation", 8);
        _marginContainer.AddChild(_toastVBox);

        // Create audio player for unlock sound
        _audioPlayer = new AudioStreamPlayer();
        AddChild(_audioPlayer);
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

        if (ProjectSettings.HasSetting(AchievementSettings.ToastUnlockSound))
        {
            var newSoundPath = ProjectSettings.GetSetting(AchievementSettings.ToastUnlockSound).AsString();
            if (newSoundPath != _unlockSoundPath)
            {
                _unlockSoundPath = newSoundPath;
                _unlockSound = null;
                if (!string.IsNullOrEmpty(_unlockSoundPath) && ResourceLoader.Exists(_unlockSoundPath))
                {
                    _unlockSound = GD.Load<AudioStream>(_unlockSoundPath);
                }
            }
        }
    }

    private void ApplyPosition()
    {
        if (_marginContainer == null) return;

        // Reset anchors and margins
        _marginContainer.AnchorLeft = 0;
        _marginContainer.AnchorTop = 0;
        _marginContainer.AnchorRight = 0;
        _marginContainer.AnchorBottom = 0;
        _marginContainer.OffsetLeft = 0;
        _marginContainer.OffsetTop = 0;
        _marginContainer.OffsetRight = 0;
        _marginContainer.OffsetBottom = 0;

        const float margin = 20f;

        switch (_position)
        {
            case ToastPosition.TopLeft:
                _marginContainer.AnchorLeft = 0;
                _marginContainer.AnchorTop = 0;
                _marginContainer.OffsetLeft = margin;
                _marginContainer.OffsetTop = margin;
                _marginContainer.GrowHorizontal = Control.GrowDirection.End;
                _marginContainer.GrowVertical = Control.GrowDirection.End;
                break;

            case ToastPosition.TopCenter:
                _marginContainer.AnchorLeft = 0.5f;
                _marginContainer.AnchorTop = 0;
                _marginContainer.OffsetTop = margin;
                _marginContainer.GrowHorizontal = Control.GrowDirection.Both;
                _marginContainer.GrowVertical = Control.GrowDirection.End;
                break;

            case ToastPosition.TopRight:
                _marginContainer.AnchorLeft = 1;
                _marginContainer.AnchorTop = 0;
                _marginContainer.OffsetRight = -margin;
                _marginContainer.OffsetTop = margin;
                _marginContainer.GrowHorizontal = Control.GrowDirection.Begin;
                _marginContainer.GrowVertical = Control.GrowDirection.End;
                break;

            case ToastPosition.BottomLeft:
                _marginContainer.AnchorLeft = 0;
                _marginContainer.AnchorTop = 1;
                _marginContainer.OffsetLeft = margin;
                _marginContainer.OffsetBottom = -margin;
                _marginContainer.GrowHorizontal = Control.GrowDirection.End;
                _marginContainer.GrowVertical = Control.GrowDirection.Begin;
                break;

            case ToastPosition.BottomCenter:
                _marginContainer.AnchorLeft = 0.5f;
                _marginContainer.AnchorTop = 1;
                _marginContainer.OffsetBottom = -margin;
                _marginContainer.GrowHorizontal = Control.GrowDirection.Both;
                _marginContainer.GrowVertical = Control.GrowDirection.Begin;
                break;

            case ToastPosition.BottomRight:
                _marginContainer.AnchorLeft = 1;
                _marginContainer.AnchorTop = 1;
                _marginContainer.OffsetRight = -margin;
                _marginContainer.OffsetBottom = -margin;
                _marginContainer.GrowHorizontal = Control.GrowDirection.Begin;
                _marginContainer.GrowVertical = Control.GrowDirection.Begin;
                break;
        }
    }

    /// <summary>
    /// Show a toast notification preview for an achievement
    /// </summary>
    public void ShowToast(Achievement achievement)
    {
        // Reload settings each time to pick up any changes from Project Settings
        var previousScenePath = _toastScenePath;
        var previousPosition = _position;
        LoadSettings();

        // Reload scene if path changed
        if (_toastScenePath != previousScenePath)
        {
            if (string.IsNullOrEmpty(_toastScenePath) || !ResourceLoader.Exists(_toastScenePath))
            {
                _toastScenePath = AchievementSettings.DefaultToastScenePath;
            }
            _toastScene = GD.Load<PackedScene>(_toastScenePath);
        }

        // Reapply position if changed
        if (_position != previousPosition)
        {
            ApplyPosition();
        }

        if (_toastScene == null || _toastVBox == null) return;

        var toast = _toastScene.Instantiate<Control>();
        if (toast == null)
        {
            AchievementLogger.Error(AchievementLogger.Areas.Editor, "Failed to instantiate toast scene.");
            return;
        }

        // Call Setup on the toast via interface or fallback to reflection
        if (toast is IAchievementToastItem toastItem)
        {
            toastItem.Setup(achievement);
        }
        else if (toast.HasMethod("Setup"))
        {
            toast.Call("Setup", achievement);
        }
        else
        {
            AchievementLogger.Warning(AchievementLogger.Areas.Editor, $"Toast scene does not implement {nameof(IAchievementToastItem)} or have a Setup method.");
        }

        // For bottom positions, add at the beginning so newest appears at bottom
        bool isBottomPosition = _position >= ToastPosition.BottomLeft;
        if (isBottomPosition)
        {
            _toastVBox.AddChild(toast);
            _toastVBox.MoveChild(toast, 0);
        }
        else
        {
            _toastVBox.AddChild(toast);
        }

        // Start hidden
        toast.Modulate = new Color(1, 1, 1, 0);

        var entry = new ToastEntry { Toast = toast };
        _activeToasts.Add(entry);

        // Play unlock sound if configured
        if (_unlockSound != null && _audioPlayer != null)
        {
            _audioPlayer.Stream = _unlockSound;
            _audioPlayer.Play();
        }

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
#endif
