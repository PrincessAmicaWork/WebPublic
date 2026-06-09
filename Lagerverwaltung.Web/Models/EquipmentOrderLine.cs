using System.ComponentModel.DataAnnotations.Schema;

namespace Lagerverwaltung.Web.Models;

/// <summary>
/// One requested item inside an EquipmentOrder.
/// A line can be connected to a physical Position, but PositionId is deliberately nullable.
/// </summary>
[Table("EQUIPMENT_ORDER_LINES")]
public class EquipmentOrderLine
{
    [Column("ID")]
    public int Id { get; set; }

    [Column("EQUIPMENT_ORDER_ID")]
    public int EquipmentOrderId { get; set; }

    public EquipmentOrder? EquipmentOrder { get; set; }

    [Column("CATALOG_ITEM_ID")]
    public int CatalogItemId { get; set; }

    public RequestCatalogItem? CatalogItem { get; set; }

    [Column("POSITION_ID")]
    public int? PositionId { get; set; }

    public Position? Position { get; set; }

    [Column("QUANTITY")]
    public int Quantity { get; set; } = 1;

    [Column("STATUS")]
    public OrderLineStatus Status { get; set; } = OrderLineStatus.WaitingForApproval;

    [Column("CATALOG_FULFILLMENT_MODE")]
    public CatalogFulfillmentMode CatalogFulfillmentMode { get; set; }

    [Column("EFFECTIVE_FULFILLMENT_MODE")]
    public EffectiveFulfillmentMode EffectiveFulfillmentMode { get; set; }

    [Column("RETURNABLE")]
    public int ReturnableValue { get; set; }

    [NotMapped]
    public bool Returnable
    {
        get => ReturnableValue == 1;
        set => ReturnableValue = value ? 1 : 0;
    }

    [Column("USED_ITEM_OK")]
    public int UsedItemOkValue { get; set; }

    [NotMapped]
    public bool UsedItemOk
    {
        get => UsedItemOkValue == 1;
        set => UsedItemOkValue = value ? 1 : 0;
    }

    [Column("USER_COMMENT")]
    public string UserComment { get; set; } = " ";

    [Column("ADMIN_COMMENT")]
    public string AdminComment { get; set; } = " ";

    [Column("FULFILLED_AT")]
    public DateTime? FulfilledAt { get; set; }

    // Snapshot fields from RequestCatalogItem.
    // These preserve old order history even if the catalog changes later.
    [Column("ACTION_CODE")]
    public string ActionCode { get; set; } = "";

    [Column("CATEGORY_NAME")]
    public string CategoryName { get; set; } = "";

    [Column("MANUFACTURER")]
    public string Manufacturer { get; set; } = "";

    [Column("ITEM_NAME")]
    public string ItemName { get; set; } = "";

    [Column("CURRENCY")]
    public string Currency { get; set; } = "CHF";

    [Column("UNIT_PRICE")]
    public decimal UnitPrice { get; set; }

    [Column("BILLING_TYPE")]
    public string BillingType { get; set; } = "ohne";

    [NotMapped]
    public bool HasPhysicalPosition => PositionId.HasValue;

    [NotMapped]
    public decimal LinePrice => UnitPrice * Math.Max(1, Quantity);

    [NotMapped]
    public string DisplayName => string.IsNullOrWhiteSpace(ActionCode)
        ? ItemName.Trim()
        : $"{ActionCode.Trim()} - {ItemName.Trim()}";
}
