namespace Lagerverwaltung.Web.Models;

/// <summary>
/// Status of one requested line inside an order.
/// IT fulfillment happens at this level.
/// </summary>
public enum OrderLineStatus
{
    WaitingForApproval = 0,
    Open = 1,
    Preparing = 2,
    WaitingForExternalOrder = 3,
    ReadyForPickup = 4,
    Fulfilled = 5,
    Cancelled = 6,
    Returned = 7
}
