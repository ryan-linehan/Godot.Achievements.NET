#if !GODOT_IOS
using System;
using System.Threading.Tasks;
using Godot.IAP.Core;

namespace Godot.IAP.Providers.Apple;

/// <summary>
/// Stub implementation for non-iOS platforms
/// </summary>
public class AppleIAPProvider : IIAPProvider
{
    public static bool IsPlatformSupported => false;

    public string ProviderName => ProviderNames.Apple;
    public bool IsAvailable => false;
    public bool IsInitialized => false;

    public AppleIAPProvider(ProductCatalog catalog)
    {
    }

    // Sync methods (no-op on unsupported platforms)
    public void Initialize() { }
    public void Purchase(string productId) { }
    public void RestorePurchases() { }

    // Async methods (return failure results)
    public Task<bool> InitializeAsync()
        => Task.FromResult(false);

    public Task<ProductInfo?> GetProductInfoAsync(string productId)
        => Task.FromResult<ProductInfo?>(null);

    public Task<ProductInfo[]> GetProductInfoAsync(string[] productIds)
        => Task.FromResult(Array.Empty<ProductInfo>());

    public Task<PurchaseResult> PurchaseAsync(string productId)
        => Task.FromResult(PurchaseResult.Failed("Apple IAP is not supported on this platform"));

    public Task<RestoreResult> RestorePurchasesAsync()
        => Task.FromResult(RestoreResult.Failed("Apple IAP is not supported on this platform"));

    public Task<bool> IsOwnedAsync(string productId)
        => Task.FromResult(false);

    public Task<SubscriptionStatus?> GetSubscriptionStatusAsync(string productId)
        => Task.FromResult<SubscriptionStatus?>(null);
}
#endif
