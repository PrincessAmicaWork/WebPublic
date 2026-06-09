using System.ComponentModel.DataAnnotations.Schema;

namespace Lagerverwaltung.Web.Models;

public enum ReturnRequestStatus
{
    Pending = 0,
    ConfirmedReturned = 1,
    Cancelled = 2
}

[Table("RETURN_REQUESTS")]
public class ReturnRequest
{
    [Column("ID")]
    public int Id { get; set; }

    // Legacy single-item request. Nullable so new multi-line orders can use the same return table.
    [Column("EQUIPMENT_REQUEST_ID")]
    public int? EquipmentRequestId { get; set; }

    public EquipmentRequest? EquipmentRequest { get; set; }

    // New flow: one return request belongs to exactly one order line.
    [Column("EQUIPMENT_ORDER_LINE_ID")]
    public int? EquipmentOrderLineId { get; set; }

    public EquipmentOrderLine? EquipmentOrderLine { get; set; }

    [Column("POSITION_ID")]
    public int PositionId { get; set; }

    public Position? Position { get; set; }

    [Column("REQUESTER_NAME")]
    public string RequesterName { get; set; } = " ";

    [Column("REQUESTER_EMAIL")]
    public string RequesterEmail { get; set; } = " ";

    [Column("STATUS")]
    public ReturnRequestStatus Status { get; set; } = ReturnRequestStatus.Pending;

    [Column("REQUESTED_AT")]
    public DateTime RequestedAt { get; set; } = DateTime.Now;

    [Column("CONFIRMED_AT")]
    public DateTime? ConfirmedAt { get; set; }

    [Column("USER_COMMENT")]
    public string UserComment { get; set; } = " ";

    [Column("ADMIN_COMMENT")]
    public string AdminComment { get; set; } = " ";

    [NotMapped]
    public bool IsLegacyRequestReturn => EquipmentRequestId.HasValue;

    [NotMapped]
    public bool IsOrderLineReturn => EquipmentOrderLineId.HasValue;
}
