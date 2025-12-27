#if TOOLS
using System;
using System.Linq;
using Godot.Collections;

namespace Godot.IAP.Core.Editor;

/// <summary>
/// Handles import/export operations for the IAP catalog
/// </summary>
public class IAPImportExportHandler
{
    private readonly Func<ProductCatalog?> _getCatalogFunc;
    private readonly Action _saveCatalog;
    private readonly Action<bool> _refreshList;
    private readonly Func<Node> _getParentNodeFunc;

    private FileDialog? _importFileDialog;
    private FileDialog? _exportFileDialog;

    public IAPImportExportHandler(
        Func<ProductCatalog?> getCatalogFunc,
        Action saveCatalog,
        Action<bool> refreshList,
        Func<Node> getParentNodeFunc)
    {
        _getCatalogFunc = getCatalogFunc;
        _saveCatalog = saveCatalog;
        _refreshList = refreshList;
        _getParentNodeFunc = getParentNodeFunc;
    }

    public void SetupMenuButtons(MenuButton importMenuButton, MenuButton exportMenuButton)
    {
        var importPopup = importMenuButton.GetPopup();
        importPopup.Clear();
        importPopup.AddItem("Import from JSON...", 0);
        importPopup.IdPressed += OnImportMenuItemPressed;

        var exportPopup = exportMenuButton.GetPopup();
        exportPopup.Clear();
        exportPopup.AddItem("Export to JSON...", 0);
        exportPopup.IdPressed += OnExportMenuItemPressed;
    }

    public void CreateFileDialogs(Node parent)
    {
        _importFileDialog = new FileDialog
        {
            FileMode = FileDialog.FileModeEnum.OpenFile,
            Access = FileDialog.AccessEnum.Filesystem,
            Title = "Import Products from JSON"
        };
        _importFileDialog.AddFilter("*.json", "JSON Files");
        _importFileDialog.FileSelected += OnImportFileSelected;
        parent.AddChild(_importFileDialog);

        _exportFileDialog = new FileDialog
        {
            FileMode = FileDialog.FileModeEnum.SaveFile,
            Access = FileDialog.AccessEnum.Filesystem,
            Title = "Export Products to JSON"
        };
        _exportFileDialog.AddFilter("*.json", "JSON Files");
        _exportFileDialog.FileSelected += OnExportFileSelected;
        parent.AddChild(_exportFileDialog);
    }

    public void Cleanup(MenuButton importMenuButton, MenuButton exportMenuButton)
    {
        importMenuButton.GetPopup().IdPressed -= OnImportMenuItemPressed;
        exportMenuButton.GetPopup().IdPressed -= OnExportMenuItemPressed;

        if (_importFileDialog != null)
        {
            _importFileDialog.FileSelected -= OnImportFileSelected;
            _importFileDialog.QueueFree();
        }

        if (_exportFileDialog != null)
        {
            _exportFileDialog.FileSelected -= OnExportFileSelected;
            _exportFileDialog.QueueFree();
        }
    }

    private void OnImportMenuItemPressed(long id)
    {
        _importFileDialog?.PopupCentered(new Vector2I(800, 600));
    }

    private void OnExportMenuItemPressed(long id)
    {
        _exportFileDialog?.PopupCentered(new Vector2I(800, 600));
    }

    private void OnImportFileSelected(string path)
    {
        var catalog = _getCatalogFunc();
        if (catalog == null) return;

        try
        {
            var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
            if (file == null)
            {
                IAPLogger.Error(IAPLogger.Areas.Editor, $"Failed to open file: {path}");
                return;
            }

            var jsonText = file.GetAsText();
            file.Close();

            var json = new Json();
            var error = json.Parse(jsonText);
            if (error != Error.Ok)
            {
                IAPLogger.Error(IAPLogger.Areas.Editor, $"Failed to parse JSON: {json.GetErrorMessage()}");
                return;
            }

            var data = json.Data.AsGodotArray<Dictionary>();
            int importedCount = 0;

            foreach (var productData in data)
            {
                var product = new InAppProduct
                {
                    Id = productData.TryGetValue("id", out var id) ? id.AsString() : "",
                    DisplayName = productData.TryGetValue("displayName", out var name) ? name.AsString() : "",
                    Description = productData.TryGetValue("description", out var desc) ? desc.AsString() : "",
                    AppleProductId = productData.TryGetValue("appleProductId", out var appleId) ? appleId.AsString() : "",
                    GooglePlayProductId = productData.TryGetValue("googlePlayProductId", out var googleId) ? googleId.AsString() : "",
                    SubscriptionGroupId = productData.TryGetValue("subscriptionGroupId", out var subGroup) ? subGroup.AsString() : ""
                };

                if (productData.TryGetValue("type", out var typeVal))
                {
                    product.Type = typeVal.AsString().ToLower() == "subscription"
                        ? ProductType.Subscription
                        : ProductType.NonConsumable;
                }

                if (!string.IsNullOrEmpty(product.Id) && catalog.GetById(product.Id) == null)
                {
                    catalog.Products.Add(product);
                    importedCount++;
                }
            }

            _saveCatalog();
            _refreshList(false);

            IAPLogger.Log(IAPLogger.Areas.Editor, $"Imported {importedCount} products from {path}");
        }
        catch (Exception ex)
        {
            IAPLogger.Error(IAPLogger.Areas.Editor, $"Import failed: {ex.Message}");
        }
    }

    private void OnExportFileSelected(string path)
    {
        var catalog = _getCatalogFunc();
        if (catalog == null) return;

        try
        {
            var productsArray = new Godot.Collections.Array();

            foreach (var product in catalog.Products)
            {
                var productDict = new Dictionary
                {
                    ["id"] = product.Id,
                    ["displayName"] = product.DisplayName,
                    ["description"] = product.Description,
                    ["type"] = product.Type.ToString(),
                    ["appleProductId"] = product.AppleProductId,
                    ["googlePlayProductId"] = product.GooglePlayProductId,
                    ["subscriptionGroupId"] = product.SubscriptionGroupId
                };
                productsArray.Add(productDict);
            }

            var jsonText = Json.Stringify(productsArray, "\t");

            var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
            if (file == null)
            {
                IAPLogger.Error(IAPLogger.Areas.Editor, $"Failed to write file: {path}");
                return;
            }

            file.StoreString(jsonText);
            file.Close();

            IAPLogger.Log(IAPLogger.Areas.Editor, $"Exported {catalog.Products.Count} products to {path}");
        }
        catch (Exception ex)
        {
            IAPLogger.Error(IAPLogger.Areas.Editor, $"Export failed: {ex.Message}");
        }
    }
}
#endif
