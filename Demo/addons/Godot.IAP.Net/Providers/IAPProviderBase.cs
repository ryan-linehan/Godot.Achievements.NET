using System.Threading.Tasks;
using Godot.IAP.Core;

namespace Godot.IAP.Providers;

/// <summary>
/// Abstract base class for IAP providers that provides Godot signal support.
/// Providers inherit from RefCounted to enable Godot signals for sync operation results.
/// </summary>
public abstract partial class IAPProviderBase : RefCounted, IIAPProvider
{
    #region Signals

    /// <summary>
    /// Emitted when a purchase operation completes.
    /// </summary>
    [Signal]
    public delegate void PurchaseCompletedEventHandler(string productId, bool success, string errorMessage);

    /// <summary>
    /// Emitted when a purchase is cancelled by the user.
    /// </summary>
    [Signal]
    public delegate void PurchaseCancelledEventHandler(string productId);

    /// <summary>
    /// Emitted when a purchase is deferred (Ask-to-Buy).
    /// </summary>
    [Signal]
    public delegate void PurchaseDeferredEventHandler(string productId);

    /// <summary>
    /// Emitted when purchases are restored.
    /// </summary>
    [Signal]
    public delegate void PurchasesRestoredEventHandler(string[] restoredProductIds, bool success, string errorMessage);

    /// <summary>
    /// Emitted when product info is received.
    /// </summary>
    [Signal]
    public delegate void ProductInfoReceivedEventHandler(string productId, string localizedPrice, bool isAvailable);

    #endregion

    #region Signal Helper Methods

    /// <summary>
    /// Emit the PurchaseCompleted signal.
    /// </summary>
    protected void EmitPurchaseCompleted(string productId, bool success, string? errorMessage = null)
        => EmitSignal(SignalName.PurchaseCompleted, productId, success, errorMessage ?? "");

    /// <summary>
    /// Emit the PurchaseCancelled signal.
    /// </summary>
    protected void EmitPurchaseCancelled(string productId)
        => EmitSignal(SignalName.PurchaseCancelled, productId);

    /// <summary>
    /// Emit the PurchaseDeferred signal.
    /// </summary>
    protected void EmitPurchaseDeferred(string productId)
        => EmitSignal(SignalName.PurchaseDeferred, productId);

    /// <summary>
    /// Emit the PurchasesRestored signal.
    /// </summary>
    protected void EmitPurchasesRestored(string[] restoredProductIds, bool success, string? errorMessage = null)
        => EmitSignal(SignalName.PurchasesRestored, restoredProductIds, success, errorMessage ?? "");

    /// <summary>
    /// Emit the ProductInfoReceived signal.
    /// </summary>
    protected void EmitProductInfoReceived(string productId, string localizedPrice, bool isAvailable)
        => EmitSignal(SignalName.ProductInfoReceived, productId, localizedPrice, isAvailable);

    #endregion

    #region IIAPProvider - Abstract Members

    /// <inheritdoc />
    public abstract string ProviderName { get; }

    /// <inheritdoc />
    public abstract bool IsAvailable { get; }

    /// <inheritdoc />
    public abstract bool IsInitialized { get; }

    /// <inheritdoc />
    public abstract void Initialize();

    /// <inheritdoc />
    public abstract Task<bool> InitializeAsync();

    /// <inheritdoc />
    public abstract Task<ProductInfo?> GetProductInfoAsync(string productId);

    /// <inheritdoc />
    public abstract Task<ProductInfo[]> GetProductInfoAsync(string[] productIds);

    /// <inheritdoc />
    public abstract void Purchase(string productId);

    /// <inheritdoc />
    public abstract Task<PurchaseResult> PurchaseAsync(string productId);

    /// <inheritdoc />
    public abstract void RestorePurchases();

    /// <inheritdoc />
    public abstract Task<RestoreResult> RestorePurchasesAsync();

    /// <inheritdoc />
    public abstract Task<bool> IsOwnedAsync(string productId);

    /// <inheritdoc />
    public abstract Task<SubscriptionStatus?> GetSubscriptionStatusAsync(string productId);

    #endregion
}
