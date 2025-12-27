#if TOOLS
using System.Collections.Generic;
using Godot;
using Godot.IAP.Core;

namespace Godot.IAP.Core.Editor;

/// <summary>
/// Field identifiers for validation warnings
/// </summary>
public static class ValidationFields
{
    public const string InternalId = "InternalId";
    public const string DisplayName = "DisplayName";
    public const string AppleProductId = "AppleProductId";
    public const string GooglePlayProductId = "GooglePlayProductId";
    public const string SubscriptionGroupId = "SubscriptionGroupId";
}

/// <summary>
/// Types of validation warnings
/// </summary>
public enum ValidationWarningType
{
    Missing,
    Duplicate
}

/// <summary>
/// Represents validation warnings for a single product
/// </summary>
public class ProductValidationResult
{
    public InAppProduct Product { get; }
    public List<string> Warnings { get; } = new();

    /// <summary>
    /// Field-specific warnings keyed by ValidationFields constants
    /// </summary>
    public Dictionary<string, ValidationWarningType> FieldWarnings { get; } = new();

    public ProductValidationResult(InAppProduct product)
    {
        Product = product;
    }

    public bool HasWarnings => Warnings.Count > 0;

    public void AddWarning(string warning)
    {
        Warnings.Add(warning);
    }

    /// <summary>
    /// Add a warning associated with a specific field
    /// </summary>
    public void AddFieldWarning(string fieldKey, ValidationWarningType warningType, string message)
    {
        FieldWarnings[fieldKey] = warningType;
        Warnings.Add(message);
    }

    public string GetTooltipText()
    {
        if (!HasWarnings) return string.Empty;
        return string.Join("\n", Warnings);
    }
}

/// <summary>
/// Validates products against enabled platform integrations
/// </summary>
public static class ProductValidator
{
    /// <summary>
    /// Validate a single product and return validation result with warnings
    /// </summary>
    public static ProductValidationResult ValidateProduct(InAppProduct product)
    {
        var result = new ProductValidationResult(product);

        // Check for missing internal ID
        if (string.IsNullOrWhiteSpace(product.Id))
        {
            result.AddFieldWarning(ValidationFields.InternalId, ValidationWarningType.Missing, "Missing internal ID");
        }

        // Check for missing display name
        if (string.IsNullOrWhiteSpace(product.DisplayName))
        {
            result.AddFieldWarning(ValidationFields.DisplayName, ValidationWarningType.Missing, "Missing display name");
        }

        // Check platform IDs based on enabled integrations
        if (GetPlatformEnabled(IAPSettings.AppleEnabled) && string.IsNullOrWhiteSpace(product.AppleProductId))
        {
            result.AddFieldWarning(ValidationFields.AppleProductId, ValidationWarningType.Missing, "Apple integration enabled but Apple Product ID is missing");
        }

        if (GetPlatformEnabled(IAPSettings.GooglePlayEnabled) && string.IsNullOrWhiteSpace(product.GooglePlayProductId))
        {
            result.AddFieldWarning(ValidationFields.GooglePlayProductId, ValidationWarningType.Missing, "Google Play integration enabled but Google Play Product ID is missing");
        }

        // Subscription-specific validation
        if (product.Type == ProductType.Subscription && string.IsNullOrWhiteSpace(product.SubscriptionGroupId))
        {
            result.AddFieldWarning(ValidationFields.SubscriptionGroupId, ValidationWarningType.Missing, "Subscription product has no Subscription Group ID");
        }

        return result;
    }

    /// <summary>
    /// Validate all products in a catalog
    /// </summary>
    public static Dictionary<InAppProduct, ProductValidationResult> ValidateCatalog(ProductCatalog catalog)
    {
        var results = new Dictionary<InAppProduct, ProductValidationResult>();

        if (catalog?.Products == null)
            return results;

        // First pass: run individual validations
        foreach (var product in catalog.Products)
        {
            var validationResult = ValidateProduct(product);
            results[product] = validationResult;
        }

        // Second pass: check for duplicate platform IDs within each provider
        CheckDuplicatePlatformIds(catalog, results);

        return results;
    }

    /// <summary>
    /// Check for duplicate internal IDs - returns list of duplicate IDs if any exist
    /// </summary>
    public static List<string> GetDuplicateInternalIds(ProductCatalog catalog)
    {
        var duplicates = new List<string>();
        var seenIds = new HashSet<string>();

        if (catalog?.Products == null)
            return duplicates;

        foreach (var product in catalog.Products)
        {
            if (!string.IsNullOrWhiteSpace(product.Id))
            {
                if (!seenIds.Add(product.Id))
                {
                    if (!duplicates.Contains(product.Id))
                        duplicates.Add(product.Id);
                }
            }
        }

        return duplicates;
    }

    /// <summary>
    /// Check for duplicate platform IDs within each provider and add warnings
    /// </summary>
    private static void CheckDuplicatePlatformIds(ProductCatalog catalog, Dictionary<InAppProduct, ProductValidationResult> results)
    {
        var appleIds = new Dictionary<string, List<InAppProduct>>();
        var googlePlayIds = new Dictionary<string, List<InAppProduct>>();

        bool appleEnabled = GetPlatformEnabled(IAPSettings.AppleEnabled);
        bool googlePlayEnabled = GetPlatformEnabled(IAPSettings.GooglePlayEnabled);

        // Collect all platform IDs
        foreach (var product in catalog.Products)
        {
            if (appleEnabled && !string.IsNullOrWhiteSpace(product.AppleProductId))
            {
                if (!appleIds.ContainsKey(product.AppleProductId))
                    appleIds[product.AppleProductId] = new List<InAppProduct>();
                appleIds[product.AppleProductId].Add(product);
            }

            if (googlePlayEnabled && !string.IsNullOrWhiteSpace(product.GooglePlayProductId))
            {
                if (!googlePlayIds.ContainsKey(product.GooglePlayProductId))
                    googlePlayIds[product.GooglePlayProductId] = new List<InAppProduct>();
                googlePlayIds[product.GooglePlayProductId].Add(product);
            }
        }

        // Add warnings for duplicates
        foreach (var kvp in appleIds)
        {
            if (kvp.Value.Count > 1)
            {
                foreach (var product in kvp.Value)
                {
                    results[product].AddFieldWarning(ValidationFields.AppleProductId, ValidationWarningType.Duplicate,
                        $"Duplicate Apple Product ID '{kvp.Key}' (shared with {kvp.Value.Count - 1} other product(s))");
                }
            }
        }

        foreach (var kvp in googlePlayIds)
        {
            if (kvp.Value.Count > 1)
            {
                foreach (var product in kvp.Value)
                {
                    results[product].AddFieldWarning(ValidationFields.GooglePlayProductId, ValidationWarningType.Duplicate,
                        $"Duplicate Google Play Product ID '{kvp.Key}' (shared with {kvp.Value.Count - 1} other product(s))");
                }
            }
        }
    }

    private static bool GetPlatformEnabled(string settingKey)
    {
        if (ProjectSettings.HasSetting(settingKey))
        {
            return ProjectSettings.GetSetting(settingKey).AsBool();
        }
        return false;
    }
}
#endif
