# In-App Purchases (IAP) Implementation Plan

## Overview

Implement an IAP system following the same architectural patterns as `Godot.Achievements.Net`, with support for Google Play and Apple App Store (extensible for future providers).

## Key Design Decisions

### Price Handling Strategy

**Prices are NOT stored in the editor/database.** This is critical:

- Prices are defined in App Store Connect / Google Play Console
- Regional pricing is handled by the stores automatically
- At runtime, query the store API to get localized price strings
- The editor only stores **Product IDs** for each platform

```
Editor stores:          Runtime displays:
─────────────           ─────────────────
InternalId: "gems_100"  Price: "$0.99" (US)
AppleProductId: "..."   Price: "€0.99" (EU)
GoogleProductId: "..."  Price: "¥120" (JP)
```

### Product Types (Initial Scope)

| Type | Description | Example |
|------|-------------|---------|
| `NonConsumable` | Purchase once, persists forever | Remove Ads, Level Pack |
| `Subscription` | Recurring payment, has expiration | Premium Membership |

> **Note:** Consumables (gems, coins, etc.) are deferred to a future update. They require additional complexity around balance tracking that is game-specific.

### Providers (Initial)

- **Apple App Store** - iOS/macOS via StoreKit
- **Google Play Billing** - Android via Play Billing Library
- **Local** - Development/testing (simulates purchases)

---

## Component Mapping: Achievements → IAP

| Achievements | IAP Equivalent | Notes |
|--------------|----------------|-------|
| `Achievement.cs` | `InAppProduct.cs` | Core model |
| `AchievementDatabase.cs` | `ProductCatalog.cs` | Resource containing all products |
| `IAchievementProvider.cs` | `IIAPProvider.cs` | Provider interface |
| `AchievementProviderBase.cs` | `IAPProviderBase.cs` | Base with signals |
| `LocalAchievementProvider.cs` | `LocalIAPProvider.cs` | Dev/testing |
| `SteamAchievementProvider.cs` | *(skip for now)* | — |
| `GameCenterAchievementProvider.cs` | `AppleIAPProvider.cs` | App Store |
| `GooglePlayAchievementProvider.cs` | `GooglePlayIAPProvider.cs` | Play Store |
| `AchievementManager.cs` | `IAPManager.cs` | Singleton autoload |
| `AchievementSettings.cs` | `IAPSettings.cs` | Project settings paths |
| `AchievementPlugin.cs` | `IAPPlugin.cs` | Editor plugin |
| `AchievementEditorDock.cs` | `IAPEditorDock.cs` | Editor UI |
| `AchievementEditorDetailsPanel.cs` | `IAPEditorDetailsPanel.cs` | Product editing |
| `AchievementValidation.cs` | `IAPValidation.cs` | Editor validation |
| *(none)* | `PurchaseResult.cs` | Purchase operation result |
| *(none)* | `ProductInfo.cs` | Runtime price/availability info |

---

## File Structure

```
Demo/addons/Godot.IAP.Net/
├── plugin.cfg
├── IAPPlugin.cs
├── README.md
├── Core/
│   ├── InAppProduct.cs          # Product definition resource
│   ├── ProductType.cs           # Enum: NonConsumable, Subscription
│   ├── ProductCatalog.cs        # Database of products
│   ├── IAPManager.cs            # Main singleton API
│   ├── IAPSettings.cs           # Project settings constants
│   ├── IAPLogger.cs             # Logging utility
│   ├── LogLevel.cs              # (reuse or copy from achievements)
│   ├── ProductInfo.cs           # Runtime info (price, availability)
│   └── PendingPurchase.cs       # For interrupted purchases
├── Providers/
│   ├── IIAPProvider.cs          # Provider interface
│   ├── IAPProviderBase.cs       # Abstract base with signals
│   ├── ProviderNames.cs         # Constants for provider names
│   ├── PurchaseResult.cs        # Result struct
│   ├── Local/
│   │   └── LocalIAPProvider.cs  # Development testing
│   ├── Apple/
│   │   ├── AppleIAPProvider.cs
│   │   └── AppleIAPProvider.Stubs.cs
│   └── GooglePlay/
│       ├── GooglePlayIAPProvider.cs
│       └── GooglePlayIAPProvider.Stubs.cs
├── Editor/
│   ├── IAPEditorDock.cs
│   ├── IAPEditorDock.tscn
│   ├── IAPEditorDetailsPanel.cs
│   ├── IAPEditorDetailsPanel.tscn
│   ├── IAPCrudOperations.cs
│   ├── IAPValidation.cs
│   ├── IAPImportExportHandler.cs
│   └── assets/
│       └── NoImage.png
└── _products/
    └── _products.tres            # Default catalog location
```

---

## Implementation Order

### Phase 1: Core Foundation

1. **`ProductType.cs`** - Simple enum
2. **`InAppProduct.cs`** - Core model resource
3. **`ProductCatalog.cs`** - Collection resource
4. **`IAPSettings.cs`** - Project settings paths
5. **`IAPLogger.cs`** - Logging (can largely copy from achievements)

### Phase 2: Provider Interface

6. **`PurchaseResult.cs`** - Result structs for operations
7. **`ProductInfo.cs`** - Runtime product info (localized price, availability)
8. **`IIAPProvider.cs`** - Provider interface
9. **`IAPProviderBase.cs`** - Abstract base with Godot signals
10. **`ProviderNames.cs`** - Provider name constants

### Phase 3: Providers

11. **`LocalIAPProvider.cs`** - Development/testing provider
12. **`AppleIAPProvider.Stubs.cs`** - Non-iOS stub
13. **`AppleIAPProvider.cs`** - Real iOS implementation
14. **`GooglePlayIAPProvider.Stubs.cs`** - Non-Android stub
15. **`GooglePlayIAPProvider.cs`** - Real Android implementation

### Phase 4: Manager

16. **`IAPManager.cs`** - Main singleton API

### Phase 5: Editor

17. **`IAPValidation.cs`** - Validation logic
18. **`IAPEditorDetailsPanel.cs/.tscn`** - Details panel
19. **`IAPEditorDock.cs/.tscn`** - Main dock
20. **`IAPCrudOperations.cs`** - CRUD helpers
21. **`IAPImportExportHandler.cs`** - Import/export

### Phase 6: Plugin Registration

22. **`IAPPlugin.cs`** - Editor plugin
23. **`plugin.cfg`** - Plugin configuration

---

## Detailed Component Specifications

### 1. InAppProduct.cs

```csharp
[Tool]
[GlobalClass]
public partial class InAppProduct : Resource
{
    // === Identity ===
    [Export] public string Id { get; set; } = "";              // Internal ID
    [Export] public string DisplayName { get; set; } = "";
    [Export(PropertyHint.MultilineText)]
    public string Description { get; set; } = "";
    [Export] public Texture2D? Icon { get; set; }

    // === Type ===
    [Export] public ProductType Type { get; set; } = ProductType.NonConsumable;

    // === Platform IDs (editor-configured) ===
    [Export] public string AppleProductId { get; set; } = "";
    [Export] public string GooglePlayProductId { get; set; } = "";

    // === Subscription-specific ===
    [Export] public string SubscriptionGroupId { get; set; } = ""; // For upgrade/downgrade

    // === Extra ===
    [Export] public Dictionary<string, Variant> ExtraProperties { get; set; } = new();

    // === Runtime (not exported) ===
    public bool IsOwned { get; set; }           // For non-consumables
    public DateTime? ExpirationDate { get; set; } // For subscriptions
}
```

### 2. IIAPProvider.cs

```csharp
public interface IIAPProvider
{
    static virtual bool IsPlatformSupported => false;
    string ProviderName { get; }
    bool IsAvailable { get; }
    bool IsInitialized { get; }

    // === Initialization ===
    void Initialize();
    Task<bool> InitializeAsync();

    // === Product Info (prices from store) ===
    Task<ProductInfo?> GetProductInfoAsync(string productId);
    Task<ProductInfo[]> GetProductInfoAsync(string[] productIds);

    // === Purchasing ===
    void Purchase(string productId);
    Task<PurchaseResult> PurchaseAsync(string productId);

    // === Restore (non-consumables & subscriptions) ===
    void RestorePurchases();
    Task<RestoreResult> RestorePurchasesAsync();

    // === Ownership Checks ===
    Task<bool> IsOwnedAsync(string productId);
    Task<SubscriptionStatus?> GetSubscriptionStatusAsync(string productId);
}
```

### 3. ProductInfo.cs (Runtime Price Data)

```csharp
public class ProductInfo
{
    public string ProductId { get; init; }
    public string LocalizedPrice { get; init; }      // "$0.99", "€0.99", etc.
    public decimal PriceAmount { get; init; }        // 0.99
    public string CurrencyCode { get; init; }        // "USD", "EUR"
    public string LocalizedTitle { get; init; }      // From store
    public string LocalizedDescription { get; init; }
    public bool IsAvailable { get; init; }           // Can be purchased
}
```

### 4. PurchaseResult.cs

```csharp
public readonly struct PurchaseResult
{
    public bool Success { get; init; }
    public string? Error { get; init; }
    public string? TransactionId { get; init; }
    public string? Receipt { get; init; }            // For server validation
    public PurchaseState State { get; init; }

    public static PurchaseResult Succeeded(string transactionId, string? receipt = null) => ...
    public static PurchaseResult Failed(string error) => ...
    public static PurchaseResult Cancelled() => ...
    public static PurchaseResult Pending() => ...    // Deferred/pending parent approval
}

public enum PurchaseState
{
    Purchased,
    Cancelled,
    Failed,
    Pending,      // Awaiting payment/approval
    Deferred      // Ask-to-buy (kids)
}
```

### 5. IAPManager.cs - Key Signals

```csharp
[Signal] public delegate void ProductPurchasedEventHandler(string productId, InAppProduct product);
[Signal] public delegate void PurchaseFailedEventHandler(string productId, string error);
[Signal] public delegate void PurchaseCancelledEventHandler(string productId);
[Signal] public delegate void PurchaseDeferredEventHandler(string productId); // Ask-to-buy
[Signal] public delegate void PurchasesRestoredEventHandler(string[] restoredProductIds);
[Signal] public delegate void ProductInfoReceivedEventHandler(string productId, ProductInfo info);
[Signal] public delegate void SubscriptionStatusCheckedEventHandler(); // Emitted on startup after checking subscriptions
```

### 6. Editor Details Panel - Fields

The details panel should show:

```
┌─────────────────────────────────────────┐
│ Internal ID: [_______________] ⚠        │
│ Display Name: [_______________]         │
│ Description: [                 ]        │
│              [_________________]        │
│ Icon: [Select...] [Preview]             │
│                                         │
│ ─── Product Type ───────────────────    │
│ Type: [NonConsumable ▼]                 │
│                                         │
│ ─── Subscription Options ───────────    │  ← Show/hide when Type=Subscription
│ Subscription Group: [___________]       │
│                                         │
│ ─── Apple App Store ────────────────    │  ← Show/hide based on settings
│ Product ID: [com.game.removeads_]       │
│                                         │
│ ─── Google Play ────────────────────    │  ← Show/hide based on settings
│ Product ID: [remove_ads_________]       │
│                                         │
│ ─── Custom Properties ──────────────    │
│ [+ Add Property]                        │
└─────────────────────────────────────────┘
```

### 7. Validation Rules

**Errors (block save):**
- Missing Internal ID
- Duplicate Internal ID
- Invalid Product Type value

**Warnings:**
- Apple enabled but no Apple Product ID
- Google Play enabled but no Google Play Product ID
- Subscription without Subscription Group ID

---

## IAP-Specific Considerations

### Receipt Validation (Optional Hook)

Receipt validation is **optional but recommended** for production. The manager provides a hook that developers can use if they have a validation server.

**Behavior:**
- If `ReceiptValidator` is null → purchase is granted immediately (client-only)
- If `ReceiptValidator` is set → purchase waits for validation before granting

```csharp
/// <summary>
/// Optional server-side receipt validation. If set, purchases wait for validation.
/// Parameters: (productId, receipt) → returns true if valid
/// </summary>
public Func<string, string, Task<bool>>? ReceiptValidator { get; set; }

// === Internal flow ===
private async Task CompletePurchase(string productId, string receipt)
{
    // If validator is set, wait for server confirmation
    if (ReceiptValidator != null)
    {
        var isValid = await ReceiptValidator(productId, receipt);
        if (!isValid)
        {
            EmitSignal(SignalName.PurchaseFailed, productId, "Receipt validation failed");
            return;
        }
    }

    // Grant the purchase
    var product = Catalog.GetById(productId);
    product.IsOwned = true;
    EmitSignal(SignalName.ProductPurchased, productId, product);
}

// === Developer usage ===
IAPManager.Instance.ReceiptValidator = async (productId, receipt) => {
    return await MyServer.ValidateReceipt(receipt);
};
```

### Restore Purchases (User-Initiated)

**No automatic sync on startup.** The store is the source of truth, and we don't persist transaction history locally.

**Apple requirement:** Apps must provide a "Restore Purchases" button that users can tap to recover their purchases (e.g., after reinstalling the app or switching devices).

```csharp
// User taps "Restore Purchases" button in your UI
private async void OnRestoreButtonPressed()
{
    var result = await IAPManager.Instance.RestorePurchasesAsync();
    if (result.Success)
    {
        // result.RestoredProductIds contains what was restored
        ShowMessage($"Restored {result.RestoredProductIds.Length} purchases");
    }
}
```

**Flow:**
1. User installs app fresh → no purchases shown
2. User taps "Restore Purchases" → we query the store
3. Store returns owned non-consumables and active subscriptions
4. We update `IsOwned` on those products and emit signals

### Sandbox/Testing

- Local provider simulates all scenarios (success, fail, cancel, pending)
- Project setting to force sandbox mode
- Clear purchase history for testing

### Subscription Status Checking

Subscriptions are different from non-consumables - they can expire. **The manager auto-checks subscription status on startup** and emits a signal when complete. The developer decides how to react.

```csharp
// In IAPManager._Ready() - automatic
private async void CheckSubscriptionStatus()
{
    foreach (var product in Catalog.Products.Where(p => p.Type == ProductType.Subscription))
    {
        var status = await _provider.GetSubscriptionStatusAsync(product.Id);
        if (status != null)
        {
            product.IsOwned = status.IsActive;
            product.ExpirationDate = status.ExpirationDate;
        }
    }
    EmitSignal(SignalName.SubscriptionStatusChecked);
}

// Developer reacts to the signal
IAPManager.Instance.SubscriptionStatusChecked += () => {
    var premium = IAPManager.Instance.GetProduct("premium_monthly");
    if (premium?.IsOwned == true)
    {
        EnablePremiumFeatures();
    }
};
```

**Summary:**
- **Non-consumables:** User-initiated restore only
- **Subscriptions:** Auto-checked on startup, developer reacts via signal

**Additional considerations:**
- Grace periods (handled by store)
- Upgrade/downgrade within subscription groups
- Introductory offers / free trials (store-managed)

---

## Project Settings Structure

```
addons/iap/
├── database_path                    # Path to ProductCatalog.tres
├── platforms/
│   ├── apple_enabled               # bool
│   └── googleplay_enabled          # bool
├── validation/
│   └── require_server_validation   # bool (optional, for strict apps)
└── log_level                        # enum: Info, Warning, Error, None
```

---

## Resolved Decisions

| Question | Decision |
|----------|----------|
| **Receipt validation** | Optional hook (`ReceiptValidator` delegate). If not set, purchases grant immediately. |
| **Non-consumable persistence** | None. Store is source of truth. User triggers "Restore Purchases" when needed (Apple requirement). |
| **Subscription checking** | Auto-check on startup, emit `SubscriptionStatusChecked` signal. Developer reacts accordingly. |
| **Import/Export format** | Same JSON structure as achievements for consistency. |
| **Addon structure** | Standalone `Godot.IAP.Net` addon (separate from achievements). |

---

## Next Steps

1. ~~Review and approve this plan~~
2. ~~Resolve open questions above~~
3. Begin Phase 1 implementation

