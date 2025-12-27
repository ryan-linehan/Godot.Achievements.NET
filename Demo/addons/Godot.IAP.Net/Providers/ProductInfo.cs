namespace Godot.IAP.Providers;

/// <summary>
/// Runtime product information retrieved from the store (includes localized pricing)
/// </summary>
public class ProductInfo
{
    /// <summary>
    /// Product ID (platform-specific)
    /// </summary>
    public string ProductId { get; init; } = string.Empty;

    /// <summary>
    /// Localized price string (e.g., "$0.99", "€0.99", "¥120")
    /// </summary>
    public string LocalizedPrice { get; init; } = string.Empty;

    /// <summary>
    /// Price amount as a decimal value
    /// </summary>
    public decimal PriceAmount { get; init; }

    /// <summary>
    /// Currency code (e.g., "USD", "EUR", "JPY")
    /// </summary>
    public string CurrencyCode { get; init; } = string.Empty;

    /// <summary>
    /// Localized product title from the store
    /// </summary>
    public string LocalizedTitle { get; init; } = string.Empty;

    /// <summary>
    /// Localized product description from the store
    /// </summary>
    public string LocalizedDescription { get; init; } = string.Empty;

    /// <summary>
    /// Whether the product is available for purchase
    /// </summary>
    public bool IsAvailable { get; init; }
}

/// <summary>
/// Subscription status information
/// </summary>
public class SubscriptionStatus
{
    /// <summary>
    /// Product ID of the subscription
    /// </summary>
    public string ProductId { get; init; } = string.Empty;

    /// <summary>
    /// Whether the subscription is currently active
    /// </summary>
    public bool IsActive { get; init; }

    /// <summary>
    /// Expiration date of the subscription (UTC)
    /// </summary>
    public System.DateTime? ExpirationDate { get; init; }

    /// <summary>
    /// Whether the subscription will auto-renew
    /// </summary>
    public bool WillAutoRenew { get; init; }

    /// <summary>
    /// Whether the user is in a grace period (payment failed but still active)
    /// </summary>
    public bool IsInGracePeriod { get; init; }

    /// <summary>
    /// Whether this is a free trial
    /// </summary>
    public bool IsFreeTrial { get; init; }

    /// <summary>
    /// Whether this is an introductory price period
    /// </summary>
    public bool IsIntroductoryPricePeriod { get; init; }
}
