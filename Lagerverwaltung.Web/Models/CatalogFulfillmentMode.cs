namespace Lagerverwaltung.Web.Models;

/// <summary>
/// Describes the catalog item's default fulfillment behavior.
/// App-wide ordering policy can still override storage usage later.
/// </summary>
public enum CatalogFulfillmentMode
{
    StockManaged = 0,
    StockOrExternalOrder = 1,
    ExternalOrderOnly = 2,
    ConsumableNoStock = 3,
    ServiceChange = 4,
    ReturnAction = 5
}
