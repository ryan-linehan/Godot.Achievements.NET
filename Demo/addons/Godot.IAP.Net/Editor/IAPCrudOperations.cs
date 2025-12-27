#if TOOLS
using System;

namespace Godot.IAP.Core.Editor;

/// <summary>
/// Handles CRUD operations for products with Undo/Redo support.
/// Uses composition pattern with Func/Action delegates for dependencies.
/// </summary>
public partial class IAPCrudOperations : RefCounted
{
    private readonly Func<ProductCatalog?> _getCatalogFunc;
    private readonly Action _saveCatalog;
    private readonly Action<bool> _refreshList;
    private readonly Action<string> _selectProductById;
    private EditorUndoRedoManager? _undoRedoManager;

    public IAPCrudOperations(
        Func<ProductCatalog?> getCatalogFunc,
        Action saveCatalog,
        Action<bool> refreshList,
        Action<string> selectProductById)
    {
        _getCatalogFunc = getCatalogFunc;
        _saveCatalog = saveCatalog;
        _refreshList = refreshList;
        _selectProductById = selectProductById;
    }

    public void SetUndoRedoManager(EditorUndoRedoManager? manager)
    {
        _undoRedoManager = manager;
    }

    #region Add Product

    public void CreateNewProduct()
    {
        var catalog = _getCatalogFunc();
        if (catalog == null)
        {
            IAPLogger.Warning(IAPLogger.Areas.Editor, "Cannot add product - no catalog loaded");
            return;
        }

        var uniqueId = GenerateUniqueId(catalog);
        var newProduct = new InAppProduct
        {
            Id = uniqueId,
            DisplayName = "New Product",
            Description = "Product description",
            Type = ProductType.NonConsumable,
            Icon = null,
            AppleProductId = string.Empty,
            GooglePlayProductId = string.Empty,
            SubscriptionGroupId = string.Empty,
            ExtraProperties = new Godot.Collections.Dictionary<string, Variant>()
        };

        if (_undoRedoManager != null)
        {
            _undoRedoManager.CreateAction("Add Product");
            _undoRedoManager.AddDoMethod(this, nameof(DoAddProduct), newProduct);
            _undoRedoManager.AddUndoMethod(this, nameof(UndoAddProduct), newProduct);
            _undoRedoManager.CommitAction();
        }
        else
        {
            DoAddProduct(newProduct);
        }
    }

    public void DoAddProduct(InAppProduct product)
    {
        var catalog = _getCatalogFunc();
        if (catalog == null) return;

        catalog.AddProduct(product);
        _saveCatalog();
        _refreshList(false);
        _selectProductById(product.Id);

        IAPLogger.Log(IAPLogger.Areas.Editor, $"Created new product: {product.Id}");
    }

    public void UndoAddProduct(InAppProduct product)
    {
        var catalog = _getCatalogFunc();
        if (catalog == null) return;

        catalog.RemoveProduct(product.Id);
        _saveCatalog();
        _refreshList(false);

        IAPLogger.Log(IAPLogger.Areas.Editor, $"Undid add product: {product.Id}");
    }

    #endregion

    #region Remove Product

    public void RemoveProduct(InAppProduct product)
    {
        var catalog = _getCatalogFunc();
        if (catalog == null) return;

        var originalIndex = catalog.Products.IndexOf(product);

        if (_undoRedoManager != null)
        {
            _undoRedoManager.CreateAction("Remove Product");
            _undoRedoManager.AddDoMethod(this, nameof(DoRemoveProduct), product);
            _undoRedoManager.AddUndoMethod(this, nameof(UndoRemoveProduct), product, originalIndex);
            _undoRedoManager.CommitAction();
        }
        else
        {
            DoRemoveProduct(product);
        }
    }

    public void DoRemoveProduct(InAppProduct product)
    {
        var catalog = _getCatalogFunc();
        if (catalog == null) return;

        catalog.RemoveProduct(product.Id);
        _saveCatalog();
        _refreshList(false);

        IAPLogger.Log(IAPLogger.Areas.Editor, $"Removed product: {product.Id}");
    }

    public void UndoRemoveProduct(InAppProduct product, int originalIndex)
    {
        var catalog = _getCatalogFunc();
        if (catalog == null) return;

        if (originalIndex >= 0 && originalIndex <= catalog.Products.Count)
        {
            catalog.Products.Insert(originalIndex, product);
        }
        else
        {
            catalog.Products.Add(product);
        }

        _saveCatalog();
        _refreshList(false);
        _selectProductById(product.Id);

        IAPLogger.Log(IAPLogger.Areas.Editor, $"Undid remove product: {product.Id}");
    }

    #endregion

    #region Duplicate Product

    public void DuplicateProduct(InAppProduct source)
    {
        var catalog = _getCatalogFunc();
        if (catalog == null) return;

        var duplicate = CloneProduct(source);

        // Ensure unique ID
        var baseId = duplicate.Id;
        var counter = 1;
        while (catalog.GetById(duplicate.Id) != null)
        {
            duplicate.Id = counter == 1 ? baseId : $"{baseId.Replace("_copy", "")}_{counter}_copy";
            counter++;
        }

        if (_undoRedoManager != null)
        {
            _undoRedoManager.CreateAction("Duplicate Product");
            _undoRedoManager.AddDoMethod(this, nameof(DoDuplicateProduct), duplicate, source.Id);
            _undoRedoManager.AddUndoMethod(this, nameof(UndoDuplicateProduct), duplicate);
            _undoRedoManager.CommitAction();
        }
        else
        {
            DoDuplicateProduct(duplicate, source.Id);
        }
    }

    public void DoDuplicateProduct(InAppProduct duplicate, string sourceId)
    {
        var catalog = _getCatalogFunc();
        if (catalog == null) return;

        catalog.AddProduct(duplicate);
        _saveCatalog();
        _refreshList(false);
        _selectProductById(duplicate.Id);

        IAPLogger.Log(IAPLogger.Areas.Editor, $"Duplicated product: {sourceId} -> {duplicate.Id}");
    }

    public void UndoDuplicateProduct(InAppProduct duplicate)
    {
        var catalog = _getCatalogFunc();
        if (catalog == null) return;

        catalog.RemoveProduct(duplicate.Id);
        _saveCatalog();
        _refreshList(false);

        IAPLogger.Log(IAPLogger.Areas.Editor, $"Undid duplicate product: {duplicate.Id}");
    }

    private static InAppProduct CloneProduct(InAppProduct original)
    {
        return new InAppProduct
        {
            Id = $"{original.Id}_copy",
            DisplayName = $"{original.DisplayName} (Copy)",
            Description = original.Description,
            Type = original.Type,
            Icon = original.Icon,
            AppleProductId = original.AppleProductId,
            GooglePlayProductId = original.GooglePlayProductId,
            SubscriptionGroupId = original.SubscriptionGroupId,
            ExtraProperties = new Godot.Collections.Dictionary<string, Variant>(original.ExtraProperties)
        };
    }

    #endregion

    #region Move Product

    public void MoveProductUp(InAppProduct product)
    {
        var catalog = _getCatalogFunc();
        if (catalog == null) return;

        var currentIndex = catalog.Products.IndexOf(product);
        if (currentIndex <= 0) return;

        if (_undoRedoManager != null)
        {
            _undoRedoManager.CreateAction("Move Product Up");
            _undoRedoManager.AddDoMethod(this, nameof(DoMoveProduct), product, currentIndex - 1);
            _undoRedoManager.AddUndoMethod(this, nameof(DoMoveProduct), product, currentIndex);
            _undoRedoManager.CommitAction();
        }
        else
        {
            DoMoveProduct(product, currentIndex - 1);
        }
    }

    public void MoveProductDown(InAppProduct product)
    {
        var catalog = _getCatalogFunc();
        if (catalog == null) return;

        var currentIndex = catalog.Products.IndexOf(product);
        if (currentIndex < 0 || currentIndex >= catalog.Products.Count - 1) return;

        if (_undoRedoManager != null)
        {
            _undoRedoManager.CreateAction("Move Product Down");
            _undoRedoManager.AddDoMethod(this, nameof(DoMoveProduct), product, currentIndex + 1);
            _undoRedoManager.AddUndoMethod(this, nameof(DoMoveProduct), product, currentIndex);
            _undoRedoManager.CommitAction();
        }
        else
        {
            DoMoveProduct(product, currentIndex + 1);
        }
    }

    public void DoMoveProduct(InAppProduct product, int toIndex)
    {
        var catalog = _getCatalogFunc();
        if (catalog == null) return;

        var currentIndex = catalog.Products.IndexOf(product);
        if (currentIndex < 0) return;

        catalog.Products.RemoveAt(currentIndex);
        catalog.Products.Insert(toIndex, product);

        _saveCatalog();
        _refreshList(true);
        IAPLogger.Log(IAPLogger.Areas.Editor, $"Moved product: {product.DisplayName}");
    }

    #endregion

    #region Helpers

    private static string GenerateUniqueId(ProductCatalog catalog)
    {
        var counter = 1;
        string id;
        do
        {
            id = $"product_{counter:D2}";
            counter++;
            if (counter > 9999)
            {
                id = $"product_{Guid.NewGuid():N}";
                break;
            }
        } while (catalog.GetById(id) != null);
        return id;
    }

    #endregion
}
#endif
