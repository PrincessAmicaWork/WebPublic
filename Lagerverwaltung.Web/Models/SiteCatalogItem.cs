using System.ComponentModel.DataAnnotations.Schema;

namespace Lagerverwaltung.Web.Models;

/// <summary>
/// Connects a global RequestCatalogItem to one Site.
/// This controls whether an item is visible at a site and how it behaves there.
/// </summary>
[Table("SITE_CATALOG_ITEMS")]
public class SiteCatalogItem
{
    [Column("ID")]
    public int Id { get; set; }

    [Column("SITE_ID")]
    public int SiteId { get; set; }

    public Site? Site { get; set; }

    [Column("CATALOG_ITEM_ID")]
    public int CatalogItemId { get; set; }

    public RequestCatalogItem? CatalogItem { get; set; }

    [Column("IS_ACTIVE")]
    public int IsActiveValue { get; set; } = 1;

    [NotMapped]
    public bool IsActive
    {
        get => IsActiveValue == 1;
        set => IsActiveValue = value ? 1 : 0;
    }

    [Column("FULFILLMENT_MODE_OVERRIDE")]
    public CatalogFulfillmentMode? FulfillmentModeOverride { get; set; }

    [Column("STORAGE_CATEGORY_ID")]
    public int? StorageCategoryId { get; set; }

    public Category? StorageCategory { get; set; }

    [Column("PRICE_OVERRIDE")]
    public decimal? PriceOverride { get; set; }

    [Column("BILLING_TYPE_OVERRIDE")]
    public string BillingTypeOverride { get; set; } = " ";

    [Column("SORT_ORDER")]
    public int SortOrder { get; set; }

    // ── Future-compatibility placeholders ────────────────────────────────
    // These properties are intentionally not yet mapped to the database.
    // They are reserved for future site-specific extensions without
    // requiring structural refactoring of the ordering pipeline.

    /// <summary>
    /// Reserved for future site-specific item name override (e.g. local brand name).
    /// Not yet persisted — add a migration with column NAME_OVERRIDE when needed.
    /// </summary>
    [NotMapped]
    public string? NameOverride { get; set; }

    /// <summary>
    /// Reserved for future site-specific currency.
    /// Effective currency falls back to CatalogItem.Currency when null.
    /// Not yet persisted — add a migration with column CURRENCY_OVERRIDE when needed.
    /// </summary>
    [NotMapped]
    public string? CurrencyOverride { get; set; }

    // ── Computed effective values ─────────────────────────────────────────

    [NotMapped]
    public CatalogFulfillmentMode EffectiveCatalogFulfillmentMode =>
        FulfillmentModeOverride
        ?? CatalogItem?.FulfillmentMode
        ?? CatalogFulfillmentMode.ConsumableNoStock;

    [NotMapped]
    public decimal EffectivePrice =>
        PriceOverride ?? CatalogItem?.Price ?? 0m;

    [NotMapped]
    public string EffectiveBillingType =>
        string.IsNullOrWhiteSpace(BillingTypeOverride)
            ? CatalogItem?.BillingType ?? "ohne"
            : BillingTypeOverride.Trim();

    /// <summary>Effective display name, respecting future NameOverride.</summary>
    [NotMapped]
    public string EffectiveDisplayName =>
        !string.IsNullOrWhiteSpace(NameOverride)
            ? NameOverride!
            : CatalogItem?.DisplayName ?? "";

    /// <summary>Effective currency, respecting future CurrencyOverride.</summary>
    [NotMapped]
    public string EffectiveCurrency =>
        !string.IsNullOrWhiteSpace(CurrencyOverride)
            ? CurrencyOverride!
            : CatalogItem?.Currency ?? "CHF";
}