using System;
using System.Linq;

namespace Godot.IAP.Core;

/// <summary>
/// Catalog of in-app products - configured in the editor and saved as a resource file
/// </summary>
[Tool]
[GlobalClass]
public partial class ProductCatalog : Resource
{
    [Export]
    public Godot.Collections.Array<InAppProduct> Products { get; set; } = new();

    /// <summary>
    /// Get a product by its internal ID
    /// </summary>
    public InAppProduct? GetById(string id)
    {
        return Products.FirstOrDefault(p => p.Id == id);
    }

    /// <summary>
    /// Get a product by its Apple product ID
    /// </summary>
    public InAppProduct? GetByAppleId(string appleProductId)
    {
        return Products.FirstOrDefault(p => p.AppleProductId == appleProductId);
    }

    /// <summary>
    /// Get a product by its Google Play product ID
    /// </summary>
    public InAppProduct? GetByGooglePlayId(string googlePlayProductId)
    {
        return Products.FirstOrDefault(p => p.GooglePlayProductId == googlePlayProductId);
    }

    /// <summary>
    /// Add a new product to the catalog
    /// </summary>
    public void AddProduct(InAppProduct product)
    {
        if (GetById(product.Id) != null)
        {
            IAPLogger.Warning(IAPLogger.Areas.Catalog, $"Product with ID '{product.Id}' already exists");
            return;
        }

        Products.Add(product);
    }

    /// <summary>
    /// Remove a product from the catalog
    /// </summary>
    public bool RemoveProduct(string id)
    {
        var product = GetById(id);
        if (product == null)
            return false;

        Products.Remove(product);
        return true;
    }

    /// <summary>
    /// Get all products of a specific type
    /// </summary>
    public InAppProduct[] GetByType(ProductType type)
    {
        return Products.Where(p => p.Type == type).ToArray();
    }

    /// <summary>
    /// Get all subscription products
    /// </summary>
    public InAppProduct[] GetSubscriptions()
    {
        return GetByType(ProductType.Subscription);
    }

    /// <summary>
    /// Get all non-consumable products
    /// </summary>
    public InAppProduct[] GetNonConsumables()
    {
        return GetByType(ProductType.NonConsumable);
    }

    /// <summary>
    /// Validate the catalog for duplicate IDs and missing required fields
    /// </summary>
    public string[] Validate()
    {
        var errors = new System.Collections.Generic.List<string>();

        // Check for duplicate internal IDs
        var duplicateIds = Products
            .Where(p => !string.IsNullOrWhiteSpace(p.Id))
            .GroupBy(p => p.Id)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key);

        foreach (var id in duplicateIds)
        {
            errors.Add($"Duplicate product ID: '{id}'");
        }

        // Check for missing required fields
        for (int i = 0; i < Products.Count; i++)
        {
            var product = Products[i];

            if (string.IsNullOrWhiteSpace(product.Id))
                errors.Add($"Product at index {i} has no ID");

            if (string.IsNullOrWhiteSpace(product.DisplayName))
                errors.Add($"Product '{product.Id}' has no display name");
        }

        return errors.ToArray();
    }
}
