using System.Threading.Tasks;
using Godot.IAP.Core;

namespace Godot.IAP.Providers;

/// <summary>
/// Extension methods for provider logging with consistent [IAP] [ProviderName] format
/// </summary>
public static class ProviderLogExtensions
{
    public static void Log(this IIAPProvider provider, string message)
    {
        IAPLogger.Log(provider.ProviderName, message);
    }

    public static void LogWarning(this IIAPProvider provider, string message)
    {
        IAPLogger.Warning(provider.ProviderName, message);
    }

    public static void LogError(this IIAPProvider provider, string message)
    {
        IAPLogger.Error(provider.ProviderName, message);
    }
}

/// <summary>
/// Interface for platform-specific IAP providers (Apple, Google Play, etc.)
/// Providers implement both sync and async versions of each operation.
/// </summary>
public interface IIAPProvider
{
    /// <summary>
    /// Whether this provider is supported on the current platform (compile-time check)
    /// Use preprocessor directives inside the implementation to enforce this.
    /// </summary>
    static virtual bool IsPlatformSupported => false;

    /// <summary>
    /// Name of the provider. Use values from <see cref="ProviderNames"/> for built-in providers.
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Whether the provider is currently available (SDK initialized, user logged in, etc.)
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Whether the provider has been initialized
    /// </summary>
    bool IsInitialized { get; }

    #region Initialization

    /// <summary>
    /// Initialize the provider (sync, fire-and-forget)
    /// </summary>
    void Initialize();

    /// <summary>
    /// Initialize the provider (async)
    /// </summary>
    Task<bool> InitializeAsync();

    #endregion

    #region Product Info

    /// <summary>
    /// Get product information including localized price (async)
    /// </summary>
    Task<ProductInfo?> GetProductInfoAsync(string productId);

    /// <summary>
    /// Get product information for multiple products (async)
    /// </summary>
    Task<ProductInfo[]> GetProductInfoAsync(string[] productIds);

    #endregion

    #region Purchasing

    /// <summary>
    /// Initiate a purchase (sync, fire-and-forget - results come via signals)
    /// </summary>
    void Purchase(string productId);

    /// <summary>
    /// Initiate a purchase (async)
    /// </summary>
    Task<PurchaseResult> PurchaseAsync(string productId);

    #endregion

    #region Restore

    /// <summary>
    /// Restore previous purchases (sync, fire-and-forget - results come via signals)
    /// </summary>
    void RestorePurchases();

    /// <summary>
    /// Restore previous purchases (async)
    /// </summary>
    Task<RestoreResult> RestorePurchasesAsync();

    #endregion

    #region Ownership

    /// <summary>
    /// Check if a product is owned (async)
    /// </summary>
    Task<bool> IsOwnedAsync(string productId);

    /// <summary>
    /// Get subscription status (async)
    /// </summary>
    Task<SubscriptionStatus?> GetSubscriptionStatusAsync(string productId);

    #endregion
}
