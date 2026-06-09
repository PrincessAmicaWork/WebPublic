using System.ComponentModel.DataAnnotations.Schema;

namespace Lagerverwaltung.Web.Models;

/// <summary>
/// One submitted equipment order/ticket.
/// One order can contain multiple order lines.
/// </summary>
[Table("EQUIPMENT_ORDERS")]
public class EquipmentOrder
{
    [Column("ID")]
    public int Id { get; set; }

    [Column("TICKET_NUMBER")]
    public string TicketNumber { get; set; } = "";

    [Column("ORDERED_BY_NAME")]
    public string OrderedByName { get; set; } = "";

    [Column("ORDERED_BY_EMAIL")]
    public string OrderedByEmail { get; set; } = "";

    [Column("REQUESTED_FOR_NAME")]
    public string RequestedForName { get; set; } = "";

    [Column("REQUESTED_FOR_EMAIL")]
    public string RequestedForEmail { get; set; } = "";

    [Column("PICKUP_CONTACT_NAME")]
    public string PickupContactName { get; set; } = "";

    [Column("PICKUP_CONTACT_EMAIL")]
    public string PickupContactEmail { get; set; } = "";

    [Column("SUPERVISOR_NAME")]
    public string SupervisorName { get; set; } = " ";

    [Column("SUPERVISOR_EMAIL")]
    public string SupervisorEmail { get; set; } = " ";

    [Column("REASON")]
    public string Reason { get; set; } = "";

    [Column("BOSS_COMMENT")]
    public string BossComment { get; set; } = " ";

    [Column("STATUS")]
    public OrderStatus Status { get; set; } = OrderStatus.PendingApproval;

    [Column("CREATED_AT")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [Column("DECISION_DATE")]
    public DateTime? DecisionDate { get; set; }

    [Column("COMPLETED_AT")]
    public DateTime? CompletedAt { get; set; }

    [Column("APPROVE_TOKEN")]
    public string ApproveToken { get; set; } = Guid.NewGuid().ToString("N");

    [Column("DENY_TOKEN")]
    public string DenyToken { get; set; } = Guid.NewGuid().ToString("N");

    [Column("SITE_ID")]
    public int SiteId { get; set; } = 1;

    public Site? Site { get; set; }

    [NotMapped]
    public string DisplaySiteName => Site?.Name?.Trim() ?? "";

    public virtual ICollection<EquipmentOrderLine> Lines { get; set; } = new List<EquipmentOrderLine>();

    [NotMapped]
    public string DisplayTicketNumber => string.IsNullOrWhiteSpace(TicketNumber)
        ? BuildTicketNumber(Id)
        : TicketNumber.Trim();

    // Uses WEB-O to avoid collisions with legacy EquipmentRequest ticket numbers like WEB-123.
    public static string BuildTicketNumber(int orderId) => $"WEB-O-{orderId}";
}
