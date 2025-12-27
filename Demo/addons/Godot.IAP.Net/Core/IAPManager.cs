using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot.IAP.Providers;
using Godot.IAP.Providers.Local;
using Godot.IAP.Providers.Apple;
using Godot.IAP.Providers.GooglePlay;

namespace Godot.IAP.Core;

/// <summary>
/// Main IAP manager singleton - the primary API for in-app purchase operations.
/// Automatically registered as an autoload in project settings.
/// </summary>
public partial class IAPManager : Node
{
    public static IAPManager? Instance { get; private set; }

    [Export] public ProductCatalog? Catalog { get; private set; }

    private LocalIAPProvider? _localProvider;
    private IIAPProvider? _platformProvider;

    /// <summary>
    /// Optional server-side receipt validation. If set, purchases wait for validation.
    /// Parameters: (productId, receipt) → returns true if valid
    /// </summary>
    public Func<string, string, Task<bool>>? ReceiptValidator { get; set; }

    #region Signals

    [Signal] public delegate void ProductPurchasedEventHandler(string productId, InAppProduct product);
    [Signal] public delegate void PurchaseFailedEventHandler(string productId, string error);
    [Signal] public delegate void PurchaseCancelledEventHandler(string productId);
    [Signal] public delegate void PurchaseDeferredEventHandler(string productId);
    [Signal] public delegate void PurchasesRestoredEventHandler(string[] restoredProductIds);
    [Signal] public delegate void ProductInfoReceivedEventHandler(string productId, string localizedPrice);
    [Signal] public delegate void SubscriptionStatusCheckedEventHandler();
    [Signal] public delegate void ProviderChangedEventHandler(string providerName);
    [Signal] public delegate void CatalogChangedEventHandler(ProductCatalog catalog);

    #endregion

    public override void _EnterTree()
    {
        if (Instance != null)
        {
            IAPLogger.Warning(IAPLogger.Areas.Core, "Multiple IAPManager instances detected. Using first instance.");
            QueueFree();
            return;
        }

        Instance = this;
    }

    public override void _Ready()
    {
        if (Catalog == null)
        {
            Catalog = LoadCatalogFromSettings();
        }

        if (Catalog == null)
        {
            IAPLogger.Error(IAPLogger.Areas.Catalog, "No ProductCatalog found!");
            return;
        }

        InitializeWithCatalog();
        InitializePlatformProvider();

        // Auto-check subscription status on startup
        CallDeferred(nameof(CheckSubscriptionStatusDeferred));
    }

    private ProductCatalog? LoadCatalogFromSettings()
    {
        var path = IAPSettings.DefaultCatalogPath;

        if (ProjectSettings.HasSetting(IAPSettings.CatalogPath))
        {
            var settingPath = ProjectSettings.GetSetting(IAPSettings.CatalogPath).AsString();
            if (!string.IsNullOrEmpty(settingPath))
            {
                path = settingPath;
            }
        }

        if (!ResourceLoader.Exists(path))
        {
            IAPLogger.Warning(IAPLogger.Areas.Catalog, $"Catalog not found at: {path}");
            return null;
        }

        var catalog = GD.Load<ProductCatalog>(path);
        if (catalog != null)
        {
            IAPLogger.Log(IAPLogger.Areas.Catalog, $"Loaded catalog from: {path}");
        }

        return catalog;
    }

    private bool InitializeWithCatalog()
    {
        if (Catalog == null) return false;

        Catalog = (ProductCatalog)Catalog.Duplicate(true);

        var errors = Catalog.Validate();
        if (errors.Length > 0)
        {
            IAPLogger.Error(IAPLogger.Areas.Catalog, "Catalog validation failed:");
            foreach (var error in errors)
            {
                GD.PushError($"  - {error}");
            }
            return false;
        }

        // Initialize local provider for testing
        _localProvider = new LocalIAPProvider(Catalog);
        IAPLogger.Log(IAPLogger.Areas.Core, "Initialized LocalIAPProvider");

        return true;
    }

    private void InitializePlatformProvider()
    {
        if (Catalog == null) return;

        // Only one platform provider is active at a time (based on current platform)
        if (AppleIAPProvider.IsPlatformSupported && GetPlatformSetting(IAPSettings.AppleEnabled))
        {
            _platformProvider = new AppleIAPProvider(Catalog);
            _platformProvider.Initialize();
            IAPLogger.Log(IAPLogger.Areas.Core, "Apple IAP provider initialized");
        }
        else if (GooglePlayIAPProvider.IsPlatformSupported && GetPlatformSetting(IAPSettings.GooglePlayEnabled))
        {
            _platformProvider = new GooglePlayIAPProvider(Catalog);
            _platformProvider.Initialize();
            IAPLogger.Log(IAPLogger.Areas.Core, "Google Play IAP provider initialized");
        }
        else
        {
            // Use local provider for development/testing
            _platformProvider = _localProvider;
            IAPLogger.Log(IAPLogger.Areas.Core, "Using LocalIAPProvider (no platform provider available)");
        }

        if (_platformProvider != null)
        {
            EmitSignal(SignalName.ProviderChanged, _platformProvider.ProviderName);
        }
    }

    private static bool GetPlatformSetting(string settingKey)
    {
        if (ProjectSettings.HasSetting(settingKey))
        {
            return ProjectSettings.GetSetting(settingKey).AsBool();
        }
        return false;
    }

    public override void _ExitTree()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    #region Purchasing

    /// <summary>
    /// Initiate a purchase (async).
    /// </summary>
    public async Task<PurchaseResult> PurchaseAsync(string productId)
    {
        if (_platformProvider == null)
        {
            var error = "No IAP provider available";
            EmitSignal(SignalName.PurchaseFailed, productId, error);
            return PurchaseResult.Failed(error);
        }

        var product = Catalog?.GetById(productId);
        if (product == null)
        {
            var error = $"Product '{productId}' not found in catalog";
            EmitSignal(SignalName.PurchaseFailed, productId, error);
            return PurchaseResult.Failed(error);
        }

        IAPLogger.Log(IAPLogger.Areas.Purchase, $"Initiating purchase for '{productId}'...");

        var result = await _platformProvider.PurchaseAsync(productId);

        if (result.State == PurchaseState.Cancelled)
        {
            IAPLogger.Log(IAPLogger.Areas.Purchase, $"Purchase cancelled for '{productId}'");
            EmitSignal(SignalName.PurchaseCancelled, productId);
            return result;
        }

        if (result.State == PurchaseState.Deferred)
        {
            IAPLogger.Log(IAPLogger.Areas.Purchase, $"Purchase deferred for '{productId}'");
            EmitSignal(SignalName.PurchaseDeferred, productId);
            return result;
        }

        if (!result.Success)
        {
            IAPLogger.Error(IAPLogger.Areas.Purchase, $"Purchase failed for '{productId}': {result.Error}");
            EmitSignal(SignalName.PurchaseFailed, productId, result.Error ?? "Unknown error");
            return result;
        }

        // Validate receipt if validator is set
        if (ReceiptValidator != null && result.Receipt != null)
        {
            IAPLogger.Log(IAPLogger.Areas.Purchase, $"Validating receipt for '{productId}'...");
            var isValid = await ReceiptValidator(productId, result.Receipt);
            if (!isValid)
            {
                var error = "Receipt validation failed";
                IAPLogger.Error(IAPLogger.Areas.Purchase, error);
                EmitSignal(SignalName.PurchaseFailed, productId, error);
                return PurchaseResult.Failed(error);
            }
        }

        // Mark as owned
        product.IsOwned = true;
        if (product.Type == ProductType.Subscription)
        {
            // For subscriptions, we should get the expiration from the provider
            // This is a placeholder - actual expiration comes from the store
            product.ExpirationDate = DateTime.UtcNow.AddDays(30);
        }

        IAPLogger.Log(IAPLogger.Areas.Purchase, $"Purchase completed for '{productId}'");
        EmitSignal(SignalName.ProductPurchased, productId, product);

        return result;
    }

    /// <summary>
    /// Initiate a purchase (sync, fire-and-forget).
    /// </summary>
    public void Purchase(string productId)
    {
        _ = PurchaseAsync(productId);
    }

    #endregion

    #region Restore

    /// <summary>
    /// Restore previous purchases (async). Required by Apple for non-consumables.
    /// </summary>
    public async Task<RestoreResult> RestorePurchasesAsync()
    {
        if (_platformProvider == null)
        {
            var error = "No IAP provider available";
            EmitSignal(SignalName.PurchasesRestored, Array.Empty<string>());
            return RestoreResult.Failed(error);
        }

        IAPLogger.Log(IAPLogger.Areas.Restore, "Restoring purchases...");

        var result = await _platformProvider.RestorePurchasesAsync();

        if (!result.Success)
        {
            IAPLogger.Error(IAPLogger.Areas.Restore, $"Restore failed: {result.Error}");
            EmitSignal(SignalName.PurchasesRestored, Array.Empty<string>());
            return result;
        }

        // Mark restored products as owned
        foreach (var productId in result.RestoredProductIds)
        {
            var product = Catalog?.GetById(productId);
            if (product != null)
            {
                product.IsOwned = true;
            }
        }

        IAPLogger.Log(IAPLogger.Areas.Restore, $"Restored {result.RestoredProductIds.Length} purchases");
        EmitSignal(SignalName.PurchasesRestored, result.RestoredProductIds);

        return result;
    }

    /// <summary>
    /// Restore previous purchases (sync, fire-and-forget).
    /// </summary>
    public void RestorePurchases()
    {
        _ = RestorePurchasesAsync();
    }

    #endregion

    #region Product Info

    /// <summary>
    /// Get product info including localized price from the store.
    /// </summary>
    public async Task<ProductInfo?> GetProductInfoAsync(string productId)
    {
        if (_platformProvider == null)
            return null;

        var info = await _platformProvider.GetProductInfoAsync(productId);

        if (info != null)
        {
            EmitSignal(SignalName.ProductInfoReceived, productId, info.LocalizedPrice);
        }

        return info;
    }

    /// <summary>
    /// Get product info for multiple products.
    /// </summary>
    public async Task<ProductInfo[]> GetProductInfoAsync(string[] productIds)
    {
        if (_platformProvider == null)
            return Array.Empty<ProductInfo>();

        return await _platformProvider.GetProductInfoAsync(productIds);
    }

    #endregion

    #region Subscription Status

    private async void CheckSubscriptionStatusDeferred()
    {
        await CheckSubscriptionStatusAsync();
    }

    /// <summary>
    /// Check subscription status for all subscription products.
    /// Called automatically on startup.
    /// </summary>
    public async Task CheckSubscriptionStatusAsync()
    {
        if (Catalog == null || _platformProvider == null)
        {
            EmitSignal(SignalName.SubscriptionStatusChecked);
            return;
        }

        var subscriptions = Catalog.GetSubscriptions();
        if (subscriptions.Length == 0)
        {
            EmitSignal(SignalName.SubscriptionStatusChecked);
            return;
        }

        IAPLogger.Log(IAPLogger.Areas.Subscription, $"Checking status for {subscriptions.Length} subscription(s)...");

        foreach (var product in subscriptions)
        {
            try
            {
                var status = await _platformProvider.GetSubscriptionStatusAsync(product.Id);
                if (status != null)
                {
                    product.IsOwned = status.IsActive;
                    product.ExpirationDate = status.ExpirationDate;

                    IAPLogger.Log(IAPLogger.Areas.Subscription,
                        $"Subscription '{product.Id}': Active={status.IsActive}, Expires={status.ExpirationDate}");
                }
            }
            catch (Exception ex)
            {
                IAPLogger.Error(IAPLogger.Areas.Subscription,
                    $"Failed to check subscription status for '{product.Id}': {ex.Message}");
            }
        }

        EmitSignal(SignalName.SubscriptionStatusChecked);
    }

    /// <summary>
    /// Get subscription status for a specific product.
    /// </summary>
    public async Task<SubscriptionStatus?> GetSubscriptionStatusAsync(string productId)
    {
        if (_platformProvider == null)
            return null;

        return await _platformProvider.GetSubscriptionStatusAsync(productId);
    }

    #endregion

    #region Query Methods

    /// <summary>
    /// Get a product by its internal ID.
    /// </summary>
    public InAppProduct? GetProduct(string productId)
    {
        return Catalog?.GetById(productId);
    }

    /// <summary>
    /// Get all products in the catalog.
    /// </summary>
    public InAppProduct[] GetAllProducts()
    {
        if (Catalog == null)
            return Array.Empty<InAppProduct>();

        return Catalog.Products.ToArray();
    }

    /// <summary>
    /// Check if a product is owned.
    /// </summary>
    public bool IsOwned(string productId)
    {
        var product = Catalog?.GetById(productId);
        if (product == null)
            return false;

        if (product.Type == ProductType.Subscription)
        {
            return product.IsSubscriptionActive;
        }

        return product.IsOwned;
    }

    /// <summary>
    /// Get the current platform provider.
    /// </summary>
    public IIAPProvider? GetCurrentProvider()
    {
        return _platformProvider;
    }

    /// <summary>
    /// Get the local provider (for testing).
    /// </summary>
    public LocalIAPProvider? GetLocalProvider()
    {
        return _localProvider;
    }

    #endregion

    #region Catalog Management

    /// <summary>
    /// Set a new product catalog at runtime.
    /// </summary>
    public bool SetCatalog(ProductCatalog catalog)
    {
        if (catalog == null)
        {
            IAPLogger.Error(IAPLogger.Areas.Catalog, "Cannot set null catalog");
            return false;
        }

        Catalog = catalog;

        if (!InitializeWithCatalog())
        {
            return false;
        }

        EmitSignal(SignalName.CatalogChanged, catalog);
        IAPLogger.Log(IAPLogger.Areas.Catalog, "Catalog changed at runtime");

        return true;
    }

    #endregion
}
