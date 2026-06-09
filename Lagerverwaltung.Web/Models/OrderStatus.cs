namespace Lagerverwaltung.Web.Models;

/// <summary>
/// Status of the whole order/ticket.
/// Supervisor approval happens at this level.
/// </summary>
public enum OrderStatus
{
    PendingApproval = 0,
    Approved = 1,
    Denied = 2,
    PartiallyFulfilled = 3,
    Completed = 4,
    Cancelled = 5
}
