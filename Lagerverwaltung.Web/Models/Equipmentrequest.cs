using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Lagerverwaltung.Web.Models
{
    public class EquipmentRequest
    {
        public int Id { get; set; }

        public int PositionId { get; set; }
        public virtual Position? Position { get; set; }

        public string RequesterName { get; set; } = "";
        public string RequesterEmail { get; set; } = "";

        public string Department { get; set; } = " ";

        [Required]
        [Column("SUPERVISOR_NAME")]
        public string SupervisorName { get; set; } = " ";

        [Required]
        [Column("SUPERVISOR_EMAIL")]
        public string SupervisorEmail { get; set; } = " ";

        public string Reason { get; set; } = "";

        [Column("USED_ITEM_OK")]
        public int UsedItemOkValue { get; set; } = 0;

        [NotMapped]
        public bool UsedItemOk
        {
            get => UsedItemOkValue == 1;
            set => UsedItemOkValue = value ? 1 : 0;
        }

        [NotMapped]
        public string UsedItemPreferenceLabel => UsedItemOk
            ? "Gebrauchtgerät OK"
            : "Nur Neuware";

        [Required]
        [MaxLength(50)]
        [Column("TICKET_NUMBER")]
        public string TicketNumber { get; set; } = "";

        public RequestStatus Status { get; set; } = RequestStatus.Pending;
        public DateTime RequestDate { get; set; } = DateTime.Now;
        public DateTime? DecisionDate { get; set; }

        [Column("FULFILLMENT_TYPE")]
        public RequestFulfillmentType? FulfillmentType { get; set; }

        [Column("FULFILLMENT_DATE")]
        public DateTime? FulfillmentDate { get; set; }

        [Required]
        public string BossComment { get; set; } = " ";

        [Column("APPROVE_TOKEN")]
        public string ApproveToken { get; set; } = Guid.NewGuid().ToString("N");

        [Column("DENY_TOKEN")]
        public string DenyToken { get; set; } = Guid.NewGuid().ToString("N");

        [NotMapped]
        public string DisplayTicketNumber => string.IsNullOrWhiteSpace(TicketNumber)
            ? BuildTicketNumber(Id)
            : TicketNumber.Trim();

        public static string BuildTicketNumber(int requestId) => $"WEB-{requestId}";
    }

    public enum RequestStatus
    {
        Pending = 0,
        Approved = 1,
        Denied = 2,
        Preparing = 3,
        Collected = 4
    }

    public enum RequestFulfillmentType
    {
        NewFromStock = 0,
        UsedReturnedItem = 1
    }
}
