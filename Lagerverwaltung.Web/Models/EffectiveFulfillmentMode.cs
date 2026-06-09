namespace Lagerverwaltung.Web.Models;

/// <summary>
/// The actual fulfillment behavior selected for an order line.
/// This can differ from the catalog default because the app instance can disable storage usage.
/// </summary>
public enum EffectiveFulfillmentMode
{
    UseStorage = 0,
    ManualNoStorage = 1,
    ExternalOrder = 2,
    ServiceChange = 3,
    ReturnAction = 4
}
