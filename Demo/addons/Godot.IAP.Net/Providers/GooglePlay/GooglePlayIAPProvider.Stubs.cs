#if !GODOT_ANDROID
using System;
using System.Threading.Tasks;
using Godot.IAP.Core;

namespace Godot.IAP.Providers.GooglePlay;

/// <summary>
/// Stub implementation for non-Android platforms
/// </summary>
public class GooglePlayIAPProvider : IIAPProvider
{
    public static bool IsPlatformSupported => false;

    public string ProviderName => ProviderNames.GooglePlay;
    public bool IsAvailable => false;
    public bool IsInitialized => false;

    public GooglePlayIAPProvider(ProductCatalog catalog)
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
        => Task.FromResult(PurchaseResult.Failed("Google Play Billing is not supported on this platform"));

    public Task<RestoreResult> RestorePurchasesAsync()
        => Task.FromResult(RestoreResult.Failed("Google Play Billing is not supported on this platform"));

    public Task<bool> IsOwnedAsync(string productId)
        => Task.FromResult(false);

    public Task<SubscriptionStatus?> GetSubscriptionStatusAsync(string productId)
        => Task.FromResult<SubscriptionStatus?>(null);
}
#endif
