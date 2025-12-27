using System;
using Godot.Collections;

namespace Godot.IAP.Core;

/// <summary>
/// In-app product definition resource that can be edited in the Godot editor
/// </summary>
[Tool]
[GlobalClass]
public partial class InAppProduct : Resource
{
    /// <summary>
    /// Unique internal identifier for the product
    /// </summary>
    [Export]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Display name of the product
    /// </summary>
    [Export]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Description of the product
    /// </summary>
    [Export(PropertyHint.MultilineText)]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Icon representing the product
    /// </summary>
    [Export]
    public Texture2D? Icon { get; set; }

    /// <summary>
    /// Type of product (NonConsumable or Subscription)
    /// </summary>
    [Export]
    public ProductType Type { get; set; } = ProductType.NonConsumable;

    /// <summary>
    /// Apple App Store product ID
    /// </summary>
    [Export]
    public string AppleProductId { get; set; } = string.Empty;

    /// <summary>
    /// Google Play product ID
    /// </summary>
    [Export]
    public string GooglePlayProductId { get; set; } = string.Empty;

    /// <summary>
    /// Subscription group ID for upgrade/downgrade (subscriptions only)
    /// </summary>
    [Export]
    public string SubscriptionGroupId { get; set; } = string.Empty;

    /// <summary>
    /// Extra properties for extensibility
    /// </summary>
    [Export]
    public Dictionary<string, Variant> ExtraProperties { get; set; } = new();

    /// <summary>
    /// Whether the product is owned (runtime state, not exported)
    /// </summary>
    public bool IsOwned { get; set; }

    /// <summary>
    /// Subscription expiration date (runtime state, not exported)
    /// </summary>
    public DateTime? ExpirationDate { get; set; }

    /// <summary>
    /// Whether the subscription is currently active
    /// </summary>
    public bool IsSubscriptionActive => Type == ProductType.Subscription
        && IsOwned
        && ExpirationDate.HasValue
        && ExpirationDate.Value > DateTime.UtcNow;
}
