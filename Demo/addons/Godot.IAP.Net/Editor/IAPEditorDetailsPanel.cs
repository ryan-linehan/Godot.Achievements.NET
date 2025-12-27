#if TOOLS
using System.Collections.Generic;

namespace Godot.IAP.Core.Editor;

/// <summary>
/// Details panel for editing a selected product
/// </summary>
[Tool]
public partial class IAPEditorDetailsPanel : Control
{
    // Common Fields
    [Export] private LineEdit IdLineEdit = null!;
    [Export] private LineEdit DisplayNameLineEdit = null!;
    [Export] private TextEdit DescriptionTextEdit = null!;
    [Export] private TextureRect IconPreview = null!;
    [Export] private Button SelectIconButton = null!;
    [Export] private Button ClearIconButton = null!;
    [Export] private OptionButton TypeOptionButton = null!;

    // Subscription Options
    [Export] public VBoxContainer SubscriptionVBox = null!;
    [Export] private LineEdit SubscriptionGroupLineEdit = null!;

    // Platform Sections
    [Export] public FoldableContainer PlatformsContainer = null!;
    [Export] public VBoxContainer AppleVBox = null!;
    [Export] private LineEdit AppleProductIdLineEdit = null!;

    [Export] public VBoxContainer GooglePlayVBox = null!;
    [Export] private LineEdit GooglePlayProductIdLineEdit = null!;

    private InAppProduct? _currentProduct;
    private EditorUndoRedoManager? _undoRedoManager;
    private FileDialog? _iconFileDialog;
    private bool _isUpdatingUI;

    // Per-field validation labels
    private Label? _internalIdErrorLabel;
    private Label? _appleWarningLabel;
    private Label? _googlePlayWarningLabel;

    [Signal] public delegate void ProductDisplayNameChangedEventHandler(InAppProduct product);
    [Signal] public delegate void ProductChangedEventHandler();

    public InAppProduct? CurrentProduct
    {
        get => _currentProduct;
        set
        {
            _currentProduct = value;
            UpdateUI();
        }
    }

    public override void _Ready()
    {
        // Setup type dropdown
        TypeOptionButton.Clear();
        TypeOptionButton.AddItem("Non-Consumable", (int)ProductType.NonConsumable);
        TypeOptionButton.AddItem("Subscription", (int)ProductType.Subscription);

        // Connect signals
        IdLineEdit.TextChanged += OnIdChanged;
        DisplayNameLineEdit.TextChanged += OnDisplayNameChanged;
        DescriptionTextEdit.TextChanged += OnDescriptionChanged;
        TypeOptionButton.ItemSelected += OnTypeChanged;
        SubscriptionGroupLineEdit.TextChanged += OnSubscriptionGroupChanged;
        AppleProductIdLineEdit.TextChanged += OnAppleProductIdChanged;
        GooglePlayProductIdLineEdit.TextChanged += OnGooglePlayProductIdChanged;
        SelectIconButton.Pressed += OnSelectIconPressed;
        ClearIconButton.Pressed += OnClearIconPressed;

        // Create file dialog for icon selection
        _iconFileDialog = new FileDialog
        {
            FileMode = FileDialog.FileModeEnum.OpenFile,
            Access = FileDialog.AccessEnum.Resources,
            Title = "Select Product Icon"
        };
        _iconFileDialog.AddFilter("*.png, *.jpg, *.jpeg, *.svg", "Image Files");
        _iconFileDialog.FileSelected += OnIconFileSelected;
        AddChild(_iconFileDialog);

        // Create per-field validation labels
        _internalIdErrorLabel = CreateValidationLabel(IdLineEdit, isError: true);
        _appleWarningLabel = CreateValidationLabel(AppleProductIdLineEdit, isError: false);
        _googlePlayWarningLabel = CreateValidationLabel(GooglePlayProductIdLineEdit, isError: false);
    }

    private Label? CreateValidationLabel(Control? siblingControl, bool isError)
    {
        if (siblingControl == null) return null;

        var label = new Label();
        label.AddThemeColorOverride("font_color", isError ? new Color(1, 0.4f, 0.4f) : new Color(1, 0.75f, 0.3f));
        label.Visible = false;

        // Add after the sibling control
        var parent = siblingControl.GetParent();
        if (parent != null)
        {
            var siblingIndex = siblingControl.GetIndex();
            parent.AddChild(label);
            parent.MoveChild(label, siblingIndex + 1);
        }

        return label;
    }

    public override void _ExitTree()
    {
        IdLineEdit.TextChanged -= OnIdChanged;
        DisplayNameLineEdit.TextChanged -= OnDisplayNameChanged;
        DescriptionTextEdit.TextChanged -= OnDescriptionChanged;
        TypeOptionButton.ItemSelected -= OnTypeChanged;
        SubscriptionGroupLineEdit.TextChanged -= OnSubscriptionGroupChanged;
        AppleProductIdLineEdit.TextChanged -= OnAppleProductIdChanged;
        GooglePlayProductIdLineEdit.TextChanged -= OnGooglePlayProductIdChanged;
        SelectIconButton.Pressed -= OnSelectIconPressed;
        ClearIconButton.Pressed -= OnClearIconPressed;

        if (_iconFileDialog != null)
        {
            _iconFileDialog.FileSelected -= OnIconFileSelected;
            _iconFileDialog.QueueFree();
        }

        // Clean up validation labels
        _internalIdErrorLabel?.QueueFree();
        _appleWarningLabel?.QueueFree();
        _googlePlayWarningLabel?.QueueFree();
    }

    public void SetUndoRedoManager(EditorUndoRedoManager manager)
    {
        _undoRedoManager = manager;
    }

    private void UpdateUI()
    {
        _isUpdatingUI = true;

        if (_currentProduct == null)
        {
            IdLineEdit.Text = "";
            DisplayNameLineEdit.Text = "";
            DescriptionTextEdit.Text = "";
            IconPreview.Texture = null;
            TypeOptionButton.Selected = 0;
            SubscriptionGroupLineEdit.Text = "";
            AppleProductIdLineEdit.Text = "";
            GooglePlayProductIdLineEdit.Text = "";
            _isUpdatingUI = false;
            return;
        }

        IdLineEdit.Text = _currentProduct.Id;
        DisplayNameLineEdit.Text = _currentProduct.DisplayName;
        DescriptionTextEdit.Text = _currentProduct.Description;
        IconPreview.Texture = _currentProduct.Icon;
        TypeOptionButton.Selected = (int)_currentProduct.Type;
        SubscriptionGroupLineEdit.Text = _currentProduct.SubscriptionGroupId;
        AppleProductIdLineEdit.Text = _currentProduct.AppleProductId;
        GooglePlayProductIdLineEdit.Text = _currentProduct.GooglePlayProductId;

        // Show/hide subscription options based on type
        SubscriptionVBox.Visible = _currentProduct.Type == ProductType.Subscription;

        _isUpdatingUI = false;
    }

    public void UpdateValidation(ProductValidationResult? result, List<string>? duplicateIds)
    {
        // Update internal ID error label
        if (_internalIdErrorLabel != null)
        {
            var isMissing = _currentProduct != null
                && string.IsNullOrWhiteSpace(_currentProduct.Id);

            var hasDuplicateId = _currentProduct != null
                && duplicateIds != null
                && !string.IsNullOrWhiteSpace(_currentProduct.Id)
                && duplicateIds.Contains(_currentProduct.Id);

            _internalIdErrorLabel.Visible = isMissing || hasDuplicateId;
            _internalIdErrorLabel.Text = isMissing ? "\u274c Required" : (hasDuplicateId ? "\u274c Duplicate" : string.Empty);
        }

        // Update platform warning labels based on validation result
        UpdatePlatformWarningLabel(_appleWarningLabel, result, "Apple");
        UpdatePlatformWarningLabel(_googlePlayWarningLabel, result, "Google Play");
    }

    private void UpdatePlatformWarningLabel(Label? label, ProductValidationResult? validationResult, string platformName)
    {
        if (label == null) return;

        string? warningText = null;
        if (validationResult != null)
        {
            foreach (var warning in validationResult.Warnings)
            {
                if (warning.Contains(platformName))
                {
                    warningText = warning.Contains("missing") ? $"\u26a0 {platformName} integration enabled but {platformName} Product ID is missing" : "\u26a0 Duplicate";
                    break;
                }
            }
        }

        label.Visible = warningText != null;
        label.Text = warningText ?? string.Empty;
    }

    public void ClearValidation()
    {
        if (_internalIdErrorLabel != null)
        {
            _internalIdErrorLabel.Visible = false;
            _internalIdErrorLabel.Text = string.Empty;
        }
        if (_appleWarningLabel != null)
        {
            _appleWarningLabel.Visible = false;
            _appleWarningLabel.Text = string.Empty;
        }
        if (_googlePlayWarningLabel != null)
        {
            _googlePlayWarningLabel.Visible = false;
            _googlePlayWarningLabel.Text = string.Empty;
        }
    }

    #region Event Handlers

    private void OnIdChanged(string newText)
    {
        if (_isUpdatingUI || _currentProduct == null) return;
        _currentProduct.Id = newText;
        EmitSignal(SignalName.ProductChanged);
    }

    private void OnDisplayNameChanged(string newText)
    {
        if (_isUpdatingUI || _currentProduct == null) return;
        _currentProduct.DisplayName = newText;
        EmitSignal(SignalName.ProductDisplayNameChanged, _currentProduct);
    }

    private void OnDescriptionChanged()
    {
        if (_isUpdatingUI || _currentProduct == null) return;
        _currentProduct.Description = DescriptionTextEdit.Text;
        EmitSignal(SignalName.ProductChanged);
    }

    private void OnTypeChanged(long index)
    {
        if (_isUpdatingUI || _currentProduct == null) return;
        _currentProduct.Type = (ProductType)index;
        SubscriptionVBox.Visible = _currentProduct.Type == ProductType.Subscription;
        EmitSignal(SignalName.ProductChanged);
    }

    private void OnSubscriptionGroupChanged(string newText)
    {
        if (_isUpdatingUI || _currentProduct == null) return;
        _currentProduct.SubscriptionGroupId = newText;
        EmitSignal(SignalName.ProductChanged);
    }

    private void OnAppleProductIdChanged(string newText)
    {
        if (_isUpdatingUI || _currentProduct == null) return;
        _currentProduct.AppleProductId = newText;
        EmitSignal(SignalName.ProductChanged);
    }

    private void OnGooglePlayProductIdChanged(string newText)
    {
        if (_isUpdatingUI || _currentProduct == null) return;
        _currentProduct.GooglePlayProductId = newText;
        EmitSignal(SignalName.ProductChanged);
    }

    private void OnSelectIconPressed()
    {
        _iconFileDialog?.PopupCentered(new Vector2I(800, 600));
    }

    private void OnIconFileSelected(string path)
    {
        if (_currentProduct == null) return;

        var texture = GD.Load<Texture2D>(path);
        if (texture != null)
        {
            _currentProduct.Icon = texture;
            IconPreview.Texture = texture;
            EmitSignal(SignalName.ProductChanged);
        }
    }

    private void OnClearIconPressed()
    {
        if (_currentProduct == null) return;
        _currentProduct.Icon = null;
        IconPreview.Texture = null;
        EmitSignal(SignalName.ProductChanged);
    }

    #endregion
}
#endif
