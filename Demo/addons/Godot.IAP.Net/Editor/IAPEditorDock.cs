#if TOOLS
using System;
using System.Linq;

namespace Godot.IAP.Core.Editor;

/// <summary>
/// Editor dock for managing in-app products in the Godot editor
/// </summary>
[Tool]
public partial class IAPEditorDock : Control
{
    // List Panel Controls
    [Export] private LineEdit SearchLineEdit = null!;
    [Export] private Button AddProductButton = null!;
    [Export] private Button RemoveButton = null!;
    [Export] private Button DuplicateButton = null!;
    [Export] private Control NoItemsControl = null!;
    [Export] private ItemList ItemList = null!;
    [Export] private ScrollContainer ItemListScrollContainer = null!;

    // Details Panel Component
    [Export] private IAPEditorDetailsPanel DetailsPanel = null!;
    [Export] private ScrollContainer NoItemSelectedScroll = null!;

    // Bottom Bar Controls
    [Export] private Label CatalogPathLabel = null!;
    [Export] private MenuButton ImportMenuButton = null!;
    [Export] private MenuButton ExportMenuButton = null!;

    // Private Fields
    private ProductCatalog? _currentCatalog;
    private string _currentCatalogPath = string.Empty;
    private InAppProduct? _selectedProduct;
    private int _selectedIndex = -1;
    private ConfirmationDialog? _removeConfirmDialog;
    private EditorUndoRedoManager? _undoRedoManager;

    // Composed Components
    private IAPCrudOperations? _crudOperations;
    private IAPImportExportHandler? _importExportHandler;

    // Validation
    private System.Collections.Generic.Dictionary<InAppProduct, ProductValidationResult> _validationResults = new();
    private System.Collections.Generic.List<string> _duplicateInternalIds = new();
    private const string WARNING_PREFIX = "\u26a0 "; // ⚠ Unicode warning sign
    private const string ERROR_PREFIX = "\u274c "; // ❌ Unicode cross mark

    // Track last known catalog path for change detection
    private string _lastKnownCatalogPath = string.Empty;

    public override void _Ready()
    {
        // Create confirmation dialog for removing products
        _removeConfirmDialog = new ConfirmationDialog();
        _removeConfirmDialog.Confirmed += ConfirmRemoveProduct;
        AddChild(_removeConfirmDialog);

        // Initialize CRUD operations component
        _crudOperations = new IAPCrudOperations(
            getCatalogFunc: () => _currentCatalog,
            saveCatalog: SaveCatalog,
            refreshList: RefreshProductList,
            selectProductById: SelectProductById
        );

        // Initialize import/export handler
        _importExportHandler = new IAPImportExportHandler(
            getCatalogFunc: () => _currentCatalog,
            saveCatalog: SaveCatalog,
            refreshList: RefreshProductList,
            getParentNodeFunc: () => this
        );
        _importExportHandler.SetupMenuButtons(ImportMenuButton, ExportMenuButton);
        _importExportHandler.CreateFileDialogs(this);

        // Connect UI signals
        AddProductButton.Pressed += OnAddProductPressed;
        RemoveButton.Pressed += OnRemovePressed;
        DuplicateButton.Pressed += OnDuplicatePressed;
        SearchLineEdit.TextChanged += OnSearchTextChanged;
        ItemList.ItemSelected += OnItemSelected;

        // Connect details panel signals
        DetailsPanel.ProductDisplayNameChanged += OnProductDisplayNameChanged;
        DetailsPanel.ProductChanged += OnProductChanged;

        // Pass undo/redo manager if already set
        if (_undoRedoManager != null)
        {
            DetailsPanel.SetUndoRedoManager(_undoRedoManager);
            _crudOperations.SetUndoRedoManager(_undoRedoManager);
        }

        // Connect visibility change
        VisibilityChanged += OnVisibilityChanged;

        // Initialize button states
        UpdateButtonStates();

        // Set platform section visibility based on project settings
        UpdatePlatformVisibility();

        // Listen for project settings changes to update platform visibility
        ProjectSettings.Singleton.SettingsChanged += OnProjectSettingsChanged;

        // Load catalog from settings
        var savedPath = LoadCatalogPath();
        _lastKnownCatalogPath = savedPath;
        LoadCatalog(savedPath);
    }

    public void SetUndoRedoManager(EditorUndoRedoManager undoRedoManager)
    {
        _undoRedoManager = undoRedoManager;
        DetailsPanel.SetUndoRedoManager(undoRedoManager);
        _crudOperations?.SetUndoRedoManager(undoRedoManager);
    }

    private void OnProjectSettingsChanged()
    {
        UpdatePlatformVisibility();
        CheckCatalogPathChanged();
        RefreshProductList(preserveSelection: true);
    }

    private void CheckCatalogPathChanged()
    {
        var currentPath = LoadCatalogPath();
        if (currentPath != _lastKnownCatalogPath)
        {
            _lastKnownCatalogPath = currentPath;
            LoadCatalog(currentPath);
        }
    }

    private void UpdatePlatformVisibility()
    {
        DetailsPanel.AppleVBox.Visible = GetPlatformEnabled(IAPSettings.AppleEnabled);
        DetailsPanel.GooglePlayVBox.Visible = GetPlatformEnabled(IAPSettings.GooglePlayEnabled);
    }

    public override void _ExitTree()
    {
        ProjectSettings.Singleton.SettingsChanged -= OnProjectSettingsChanged;
        VisibilityChanged -= OnVisibilityChanged;

        DetailsPanel.ProductDisplayNameChanged -= OnProductDisplayNameChanged;
        DetailsPanel.ProductChanged -= OnProductChanged;

        _importExportHandler?.Cleanup(ImportMenuButton, ExportMenuButton);

        if (_removeConfirmDialog != null)
        {
            _removeConfirmDialog.Confirmed -= ConfirmRemoveProduct;
            _removeConfirmDialog.QueueFree();
        }

        AddProductButton.Pressed -= OnAddProductPressed;
        RemoveButton.Pressed -= OnRemovePressed;
        DuplicateButton.Pressed -= OnDuplicatePressed;
        SearchLineEdit.TextChanged -= OnSearchTextChanged;
        ItemList.ItemSelected -= OnItemSelected;
    }

    private void OnVisibilityChanged()
    {
        if (Visible)
        {
            RefreshProductList(preserveSelection: true);
        }
    }

    private void UpdateButtonStates()
    {
        var hasSelection = _selectedProduct != null;
        RemoveButton.Disabled = !hasSelection;
        DuplicateButton.Disabled = !hasSelection;
    }

    private void OnProductDisplayNameChanged(InAppProduct product)
    {
        UpdateListItemForProduct(product);
    }

    private void OnProductChanged()
    {
        RefreshProductList(preserveSelection: true);
    }

    private void UpdateCatalogPathLabel()
    {
        if (_currentCatalog == null)
        {
            CatalogPathLabel.Text = "No catalog loaded";
        }
        else
        {
            var displayPath = ResolveUidToPath(_currentCatalogPath);
            CatalogPathLabel.Text = displayPath;
        }
    }

    private void UpdateListItemForProduct(InAppProduct? product)
    {
        if (product == null) return;

        var validationResult = ProductValidator.ValidateProduct(product);
        _validationResults[product] = validationResult;

        for (int i = 0; i < ItemList.ItemCount; i++)
        {
            var itemProduct = ItemList.GetItemMetadata(i).As<InAppProduct>();
            if (itemProduct == product)
            {
                var hasWarnings = validationResult.HasWarnings;
                var displayText = hasWarnings ? WARNING_PREFIX + product.DisplayName : product.DisplayName;

                ItemList.SetItemText(i, displayText);
                ItemList.SetItemIcon(i, product.Icon);
                ItemList.SetItemTooltip(i, hasWarnings ? validationResult.GetTooltipText() : string.Empty);
                break;
            }
        }
    }

    #region Catalog Operations

    private void LoadCatalog(string path)
    {
        if (!ResourceLoader.Exists(path))
        {
            IAPLogger.Warning(IAPLogger.Areas.Editor, $"Catalog not found at {path}");
            _currentCatalog = null;
            _currentCatalogPath = string.Empty;
            CatalogPathLabel.Text = "No catalog loaded";
            RefreshProductList();
            return;
        }

        ProductCatalog? resource = null;
        try
        {
            resource = ResourceLoader.Load<ProductCatalog>(path);
        }
        catch (System.InvalidCastException ex)
        {
            IAPLogger.Error(IAPLogger.Areas.Editor, $"Resource at {path} is not a ProductCatalog: {ex.Message}");
            var dialog = new AcceptDialog();
            dialog.DialogText = $"The file at:\n{path}\n\nis not a valid ProductCatalog resource.";
            AddChild(dialog);
            dialog.PopupCentered();
            return;
        }

        if (resource == null)
        {
            IAPLogger.Error(IAPLogger.Areas.Editor, $"Failed to load catalog from {path}");
            return;
        }

        _currentCatalog = resource;
        _currentCatalogPath = path;
        SaveCatalogPath(path);
        UpdateCatalogPathLabel();
        RefreshProductList();

        IAPLogger.Log(IAPLogger.Areas.Editor, $"Loaded catalog from {path}");
    }

    public void SaveCatalog()
    {
        if (_currentCatalog == null)
        {
            IAPLogger.Warning(IAPLogger.Areas.Editor, "No catalog loaded to save");
            return;
        }

        var savePath = ResolveUidToPath(_currentCatalogPath);
        var saveError = ResourceSaver.Save(_currentCatalog, savePath);
        if (saveError != Error.Ok)
        {
            IAPLogger.Error(IAPLogger.Areas.Editor, $"Failed to save catalog: {saveError}");
            return;
        }

        IAPLogger.Log(IAPLogger.Areas.Editor, $"Catalog saved to {savePath}");
        _currentCatalog.EmitChanged();
        _currentCatalog.NotifyPropertyListChanged();
    }

    private string LoadCatalogPath()
    {
        if (ProjectSettings.HasSetting(IAPSettings.CatalogPath))
        {
            var path = ProjectSettings.GetSetting(IAPSettings.CatalogPath).AsString();
            return string.IsNullOrEmpty(path) ? IAPSettings.DefaultCatalogPath : path;
        }
        return IAPSettings.DefaultCatalogPath;
    }

    private static string ResolveUidToPath(string path)
    {
        if (path.StartsWith("uid://"))
        {
            var uid = ResourceUid.TextToId(path);
            if (ResourceUid.HasId(uid))
            {
                return ResourceUid.GetIdPath(uid);
            }
        }
        return path;
    }

    private void SaveCatalogPath(string path)
    {
        ProjectSettings.SetSetting(IAPSettings.CatalogPath, path);
        ProjectSettings.Save();
    }

    private static bool GetPlatformEnabled(string settingKey)
    {
        if (ProjectSettings.HasSetting(settingKey))
        {
            return ProjectSettings.GetSetting(settingKey).AsBool();
        }
        return false;
    }

    #endregion

    #region List Management

    private void RefreshProductList(bool preserveSelection = false)
    {
        var previousSelection = preserveSelection ? _selectedProduct : null;

        ItemList.Clear();
        _selectedProduct = null;
        _selectedIndex = -1;

        if (_currentCatalog == null || _currentCatalog.Products.Count == 0)
        {
            _validationResults.Clear();
            NoItemsControl.Visible = true;
            ItemList.Visible = false;
            ItemListScrollContainer.Visible = false;
            DetailsPanel.Visible = false;
            NoItemSelectedScroll.Visible = true;
            UpdateButtonStates();
            return;
        }

        _validationResults = ProductValidator.ValidateCatalog(_currentCatalog);
        _duplicateInternalIds = ProductValidator.GetDuplicateInternalIds(_currentCatalog);

        NoItemsControl.Visible = false;
        ItemList.Visible = true;
        ItemListScrollContainer.Visible = true;

        var searchText = SearchLineEdit.Text?.ToLower() ?? string.Empty;
        var filteredProducts = _currentCatalog.Products
            .Where(p => string.IsNullOrEmpty(searchText)
                || (p.DisplayName?.ToLower().Contains(searchText) ?? false)
                || (p.Id?.ToLower().Contains(searchText) ?? false))
            .ToList();

        if (filteredProducts.Count == 0)
        {
            NoItemsControl.Visible = true;
            ItemList.Visible = false;
            ItemListScrollContainer.Visible = false;
            DetailsPanel.Visible = false;
            NoItemSelectedScroll.Visible = true;
            UpdateButtonStates();
            return;
        }

        for (int i = 0; i < filteredProducts.Count; i++)
        {
            var product = filteredProducts[i];

            var hasError = string.IsNullOrWhiteSpace(product.Id)
                || _duplicateInternalIds.Contains(product.Id);
            var hasWarnings = _validationResults.TryGetValue(product, out var validationResult) && validationResult.HasWarnings;

            string displayText;
            if (hasError)
                displayText = ERROR_PREFIX + product.DisplayName;
            else if (hasWarnings)
                displayText = WARNING_PREFIX + product.DisplayName;
            else
                displayText = product.DisplayName;

            var index = ItemList.AddItem(displayText, product.Icon);
            ItemList.SetItemMetadata(index, product);

            if (hasError)
            {
                var errorText = string.IsNullOrWhiteSpace(product.Id)
                    ? "Internal ID is required"
                    : $"Duplicate internal ID: {product.Id}";
                ItemList.SetItemTooltip(index, errorText);
            }
            else if (hasWarnings && validationResult != null)
            {
                ItemList.SetItemTooltip(index, validationResult.GetTooltipText());
            }

            if (product == previousSelection)
            {
                ItemList.Select(index);
                _selectedProduct = product;
                _selectedIndex = index;
            }
        }

        if (_selectedProduct != null)
        {
            DetailsPanel.Visible = true;
            NoItemSelectedScroll.Visible = false;
            DetailsPanel.CurrentProduct = _selectedProduct;
            UpdateDetailsPanelValidation();
        }
        else
        {
            DetailsPanel.Visible = false;
            NoItemSelectedScroll.Visible = true;
            DetailsPanel.ClearValidation();
        }

        UpdateButtonStates();
    }

    private void UpdateDetailsPanelValidation()
    {
        if (_selectedProduct == null || _currentCatalog == null)
        {
            DetailsPanel.ClearValidation();
            return;
        }

        _validationResults.TryGetValue(_selectedProduct, out var validationResult);
        DetailsPanel.UpdateValidation(validationResult, _duplicateInternalIds);
    }

    private void OnSearchTextChanged(string newText)
    {
        RefreshProductList(preserveSelection: true);
    }

    private void OnItemSelected(long index)
    {
        var targetProduct = ItemList.GetItemMetadata((int)index).As<InAppProduct>();
        SelectProduct((int)index, targetProduct);
    }

    private void SelectProduct(int index, InAppProduct product)
    {
        _selectedIndex = index;
        _selectedProduct = product;

        DetailsPanel.Visible = true;
        NoItemSelectedScroll.Visible = false;
        DetailsPanel.CurrentProduct = _selectedProduct;
        UpdateDetailsPanelValidation();

        UpdateButtonStates();
    }

    #endregion

    #region CRUD Operations

    private void OnAddProductPressed()
    {
        _crudOperations?.CreateNewProduct();
    }

    private void SelectProductById(string id)
    {
        for (int i = 0; i < ItemList.ItemCount; i++)
        {
            var product = ItemList.GetItemMetadata(i).As<InAppProduct>();
            if (product?.Id == id)
            {
                ItemList.Select(i);
                SelectProduct(i, product);
                break;
            }
        }
    }

    private void OnRemovePressed()
    {
        if (_selectedProduct == null)
        {
            IAPLogger.Warning(IAPLogger.Areas.Editor, "No product selected to remove");
            return;
        }

        if (_removeConfirmDialog != null)
        {
            _removeConfirmDialog.DialogText = $"Are you sure you want to remove the product:\n\n'{_selectedProduct.DisplayName}' ({_selectedProduct.Id}).";
            _removeConfirmDialog.PopupCentered();
        }
    }

    private void ConfirmRemoveProduct()
    {
        if (_selectedProduct == null)
            return;

        var productToRemove = _selectedProduct;
        _selectedProduct = null;
        _selectedIndex = -1;
        _crudOperations?.RemoveProduct(productToRemove);
    }

    private void OnDuplicatePressed()
    {
        if (_selectedProduct == null)
        {
            IAPLogger.Warning(IAPLogger.Areas.Editor, "No product selected to duplicate");
            return;
        }

        _crudOperations?.DuplicateProduct(_selectedProduct);
    }

    #endregion
}
#endif
