using System.ComponentModel.DataAnnotations.Schema;

namespace Lagerverwaltung.Web.Models;

/// <summary>
/// A user-requestable catalog item.
/// This is deliberately separate from Position: not every requestable item exists in physical storage.
/// </summary>
[Table("REQUEST_CATALOG_ITEMS")]
public class RequestCatalogItem
{
    public int Id { get; set; }

    public string ActionCode { get; set; } = "";
    public string CategoryName { get; set; } = " ";
    public string Manufacturer { get; set; } = " ";
    public string ItemName { get; set; } = " ";

    public string Currency { get; set; } = "CHF";
    public decimal Price { get; set; }
    public string BillingType { get; set; } = "ohne";

    public CatalogFulfillmentMode FulfillmentMode { get; set; } = CatalogFulfillmentMode.ConsumableNoStock;

    [Column("RETURNABLE")]
    public int ReturnableValue { get; set; }

    [NotMapped]
    public bool Returnable
    {
        get => ReturnableValue == 1;
        set => ReturnableValue = value ? 1 : 0;
    }

    [Column("REQUIRES_COMMENT")]
    public int RequiresCommentValue { get; set; }

    [NotMapped]
    public bool RequiresComment
    {
        get => RequiresCommentValue == 1;
        set => RequiresCommentValue = value ? 1 : 0;
    }

    [Column("IS_ACTIVE")]
    public int IsActiveValue { get; set; } = 1;

    [NotMapped]
    public bool IsActive
    {
        get => IsActiveValue == 1;
        set => IsActiveValue = value ? 1 : 0;
    }

    public int? StorageCategoryId { get; set; }
    public Category? StorageCategory { get; set; }

    [NotMapped]
    public bool CanUseStorageByCatalogDefault =>
        FulfillmentMode is CatalogFulfillmentMode.StockManaged
            or CatalogFulfillmentMode.StockOrExternalOrder;

    [NotMapped]
    public string DisplayName => string.IsNullOrWhiteSpace(ActionCode)
        ? ItemName.Trim()
        : $"{ActionCode.Trim()} - {ItemName.Trim()}";
}
