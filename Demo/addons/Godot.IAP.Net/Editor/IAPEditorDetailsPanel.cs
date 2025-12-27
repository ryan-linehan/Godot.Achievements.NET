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
    [Export] public VBoxContainer AppleVBox = null!;
    [Export] private LineEdit AppleProductIdLineEdit = null!;

    [Export] public VBoxContainer GooglePlayVBox = null!;
    [Export] private LineEdit GooglePlayProductIdLineEdit = null!;

    // Validation
    [Export] private Label ValidationLabel = null!;

    private InAppProduct? _currentProduct;
    private EditorUndoRedoManager? _undoRedoManager;
    private FileDialog? _iconFileDialog;
    private bool _isUpdatingUI;

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
        if (result == null && (duplicateIds == null || duplicateIds.Count == 0))
        {
            ValidationLabel.Text = "";
            ValidationLabel.Visible = false;
            return;
        }

        var warnings = new List<string>();

        if (_currentProduct != null && duplicateIds != null && duplicateIds.Contains(_currentProduct.Id))
        {
            warnings.Add($"Duplicate internal ID: {_currentProduct.Id}");
        }

        if (result != null)
        {
            warnings.AddRange(result.Warnings);
        }

        if (warnings.Count > 0)
        {
            ValidationLabel.Text = string.Join("\n", warnings);
            ValidationLabel.Visible = true;
        }
        else
        {
            ValidationLabel.Text = "";
            ValidationLabel.Visible = false;
        }
    }

    public void ClearValidation()
    {
        ValidationLabel.Text = "";
        ValidationLabel.Visible = false;
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
