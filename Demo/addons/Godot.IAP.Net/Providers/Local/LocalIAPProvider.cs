using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot.IAP.Core;

namespace Godot.IAP.Providers.Local;

/// <summary>
/// Local IAP provider for development and testing.
/// Simulates purchases without real transactions.
/// Does NOT persist purchase state - the store is the source of truth.
/// </summary>
public partial class LocalIAPProvider : IAPProviderBase
{
    public static bool IsPlatformSupported => true;

    private readonly ProductCatalog _catalog;
    private readonly HashSet<string> _ownedProducts = new();
    private readonly Dictionary<string, DateTime> _subscriptionExpirations = new();
    private bool _isInitialized;

    /// <summary>
    /// Simulate purchase failures for testing
    /// </summary>
    public bool SimulateFailures { get; set; }

    /// <summary>
    /// Simulate purchase cancellations for testing
    /// </summary>
    public bool SimulateCancellations { get; set; }

    /// <summary>
    /// Simulate deferred purchases (Ask-to-Buy) for testing
    /// </summary>
    public bool SimulateDeferred { get; set; }

    /// <summary>
    /// Simulated delay for async operations (milliseconds)
    /// </summary>
    public int SimulatedDelayMs { get; set; } = 500;

    public override string ProviderName => ProviderNames.Local;
    public override bool IsAvailable => _isInitialized;
    public override bool IsInitialized => _isInitialized;

    public LocalIAPProvider(ProductCatalog catalog)
    {
        _catalog = catalog;
    }

    #region Initialization

    public override void Initialize()
    {
        _isInitialized = true;
        this.Log("Initialized (development/testing mode)");
    }

    public override async Task<bool> InitializeAsync()
    {
        await Task.Delay(SimulatedDelayMs);
        Initialize();
        return true;
    }

    #endregion

    #region Product Info

    public override async Task<ProductInfo?> GetProductInfoAsync(string productId)
    {
        await Task.Delay(SimulatedDelayMs);

        var product = _catalog.GetById(productId);
        if (product == null)
        {
            this.LogWarning($"Product '{productId}' not found in catalog");
            return null;
        }

        // Return simulated product info
        return new ProductInfo
        {
            ProductId = productId,
            LocalizedPrice = "$0.99",
            PriceAmount = 0.99m,
            CurrencyCode = "USD",
            LocalizedTitle = product.DisplayName,
            LocalizedDescription = product.Description,
            IsAvailable = true
        };
    }

    public override async Task<ProductInfo[]> GetProductInfoAsync(string[] productIds)
    {
        var results = new List<ProductInfo>();
        foreach (var productId in productIds)
        {
            var info = await GetProductInfoAsync(productId);
            if (info != null)
            {
                results.Add(info);
            }
        }
        return results.ToArray();
    }

    #endregion

    #region Purchasing

    public override void Purchase(string productId)
    {
        _ = PurchaseAsync(productId);
    }

    public override async Task<PurchaseResult> PurchaseAsync(string productId)
    {
        await Task.Delay(SimulatedDelayMs);

        var product = _catalog.GetById(productId);
        if (product == null)
        {
            var error = $"Product '{productId}' not found in catalog";
            this.LogWarning(error);
            EmitPurchaseCompleted(productId, false, error);
            return PurchaseResult.Failed(error);
        }

        if (SimulateFailures)
        {
            var error = "Simulated purchase failure";
            this.Log(error);
            EmitPurchaseCompleted(productId, false, error);
            return PurchaseResult.Failed(error);
        }

        if (SimulateCancellations)
        {
            this.Log($"Simulated purchase cancellation for '{productId}'");
            EmitPurchaseCancelled(productId);
            return PurchaseResult.Cancelled();
        }

        if (SimulateDeferred)
        {
            this.Log($"Simulated deferred purchase for '{productId}'");
            EmitPurchaseDeferred(productId);
            return PurchaseResult.Deferred();
        }

        // Simulate successful purchase
        var transactionId = $"local_{Guid.NewGuid():N}";
        _ownedProducts.Add(productId);
        product.IsOwned = true;

        if (product.Type == ProductType.Subscription)
        {
            // Simulate 1-month subscription
            var expiration = DateTime.UtcNow.AddDays(30);
            _subscriptionExpirations[productId] = expiration;
            product.ExpirationDate = expiration;
        }

        this.Log($"Purchased '{productId}' (transaction: {transactionId})");
        EmitPurchaseCompleted(productId, true);

        return PurchaseResult.Succeeded(transactionId, "local_receipt_simulated");
    }

    #endregion

    #region Restore

    public override void RestorePurchases()
    {
        _ = RestorePurchasesAsync();
    }

    public override async Task<RestoreResult> RestorePurchasesAsync()
    {
        await Task.Delay(SimulatedDelayMs);

        // In local testing, just return currently "owned" products
        var restoredIds = _ownedProducts.ToArray();

        foreach (var productId in restoredIds)
        {
            var product = _catalog.GetById(productId);
            if (product != null)
            {
                product.IsOwned = true;
                if (product.Type == ProductType.Subscription &&
                    _subscriptionExpirations.TryGetValue(productId, out var expiration))
                {
                    product.ExpirationDate = expiration;
                }
            }
        }

        this.Log($"Restored {restoredIds.Length} purchases");
        EmitPurchasesRestored(restoredIds, true);

        return RestoreResult.Succeeded(restoredIds);
    }

    #endregion

    #region Ownership

    public override Task<bool> IsOwnedAsync(string productId)
    {
        var product = _catalog.GetById(productId);
        if (product == null)
        {
            return Task.FromResult(false);
        }

        if (product.Type == ProductType.Subscription)
        {
            // Check if subscription is still active
            if (_subscriptionExpirations.TryGetValue(productId, out var expiration))
            {
                return Task.FromResult(expiration > DateTime.UtcNow);
            }
            return Task.FromResult(false);
        }

        return Task.FromResult(_ownedProducts.Contains(productId));
    }

    public override Task<SubscriptionStatus?> GetSubscriptionStatusAsync(string productId)
    {
        var product = _catalog.GetById(productId);
        if (product == null || product.Type != ProductType.Subscription)
        {
            return Task.FromResult<SubscriptionStatus?>(null);
        }

        if (!_subscriptionExpirations.TryGetValue(productId, out var expiration))
        {
            return Task.FromResult<SubscriptionStatus?>(new SubscriptionStatus
            {
                ProductId = productId,
                IsActive = false
            });
        }

        return Task.FromResult<SubscriptionStatus?>(new SubscriptionStatus
        {
            ProductId = productId,
            IsActive = expiration > DateTime.UtcNow,
            ExpirationDate = expiration,
            WillAutoRenew = true,
            IsInGracePeriod = false,
            IsFreeTrial = false,
            IsIntroductoryPricePeriod = false
        });
    }

    #endregion

    #region Testing Helpers

    /// <summary>
    /// Clear all simulated purchases (for testing)
    /// </summary>
    public void ClearAllPurchases()
    {
        _ownedProducts.Clear();
        _subscriptionExpirations.Clear();

        foreach (var product in _catalog.Products)
        {
            product.IsOwned = false;
            product.ExpirationDate = null;
        }

        this.Log("Cleared all simulated purchases");
    }

    /// <summary>
    /// Simulate owning a product (for testing)
    /// </summary>
    public void SimulateOwnership(string productId, DateTime? subscriptionExpiration = null)
    {
        var product = _catalog.GetById(productId);
        if (product == null) return;

        _ownedProducts.Add(productId);
        product.IsOwned = true;

        if (product.Type == ProductType.Subscription && subscriptionExpiration.HasValue)
        {
            _subscriptionExpirations[productId] = subscriptionExpiration.Value;
            product.ExpirationDate = subscriptionExpiration.Value;
        }
    }

    #endregion
}
