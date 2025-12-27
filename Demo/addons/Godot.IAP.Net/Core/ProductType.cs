namespace Godot.IAP.Core;

/// <summary>
/// Type of in-app product
/// </summary>
public enum ProductType
{
    /// <summary>
    /// One-time purchase that persists forever (e.g., Remove Ads, Level Pack)
    /// </summary>
    NonConsumable,

    /// <summary>
    /// Recurring payment with expiration (e.g., Premium Membership)
    /// </summary>
    Subscription
}
