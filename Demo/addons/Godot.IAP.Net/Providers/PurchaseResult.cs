namespace Godot.IAP.Providers;

/// <summary>
/// State of a purchase operation
/// </summary>
public enum PurchaseState
{
    /// <summary>
    /// Purchase completed successfully
    /// </summary>
    Purchased,

    /// <summary>
    /// User cancelled the purchase
    /// </summary>
    Cancelled,

    /// <summary>
    /// Purchase failed due to an error
    /// </summary>
    Failed,

    /// <summary>
    /// Purchase is pending (awaiting payment confirmation)
    /// </summary>
    Pending,

    /// <summary>
    /// Purchase is deferred (Ask-to-Buy for kids)
    /// </summary>
    Deferred
}

/// <summary>
/// Result of a purchase operation
/// </summary>
public readonly struct PurchaseResult
{
    public bool Success { get; init; }
    public string? Error { get; init; }
    public string? TransactionId { get; init; }
    public string? Receipt { get; init; }
    public PurchaseState State { get; init; }

    public static PurchaseResult Succeeded(string transactionId, string? receipt = null) =>
        new()
        {
            Success = true,
            TransactionId = transactionId,
            Receipt = receipt,
            State = PurchaseState.Purchased
        };

    public static PurchaseResult Failed(string error) =>
        new()
        {
            Success = false,
            Error = error,
            State = PurchaseState.Failed
        };

    public static PurchaseResult Cancelled() =>
        new()
        {
            Success = false,
            State = PurchaseState.Cancelled
        };

    public static PurchaseResult Pending(string? transactionId = null) =>
        new()
        {
            Success = false,
            TransactionId = transactionId,
            State = PurchaseState.Pending
        };

    public static PurchaseResult Deferred(string? transactionId = null) =>
        new()
        {
            Success = false,
            TransactionId = transactionId,
            State = PurchaseState.Deferred
        };

    /// <summary>
    /// Implicit conversion to bool for easy success checks
    /// </summary>
    public static implicit operator bool(PurchaseResult result) => result.Success;
}

/// <summary>
/// Result of a restore purchases operation
/// </summary>
public readonly struct RestoreResult
{
    public bool Success { get; init; }
    public string? Error { get; init; }
    public string[] RestoredProductIds { get; init; }

    public static RestoreResult Succeeded(string[] restoredProductIds) =>
        new()
        {
            Success = true,
            RestoredProductIds = restoredProductIds
        };

    public static RestoreResult Failed(string error) =>
        new()
        {
            Success = false,
            Error = error,
            RestoredProductIds = System.Array.Empty<string>()
        };

    /// <summary>
    /// Implicit conversion to bool for easy success checks
    /// </summary>
    public static implicit operator bool(RestoreResult result) => result.Success;
}
