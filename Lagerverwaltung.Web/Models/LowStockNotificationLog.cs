using System.ComponentModel.DataAnnotations.Schema;

namespace Lagerverwaltung.Web.Models;

[Table("LOW_STOCK_NOTIFICATION_LOGS")]
public class LowStockNotificationLog
{
    [Column("ID")]
    public int Id { get; set; }

    [Column("CATEGORY_ID")]
    public int CategoryId { get; set; }

    public Category? Category { get; set; }

    [Column("SENT_AT")]
    public DateTime SentAt { get; set; } = DateTime.Now;

    [Column("AVAILABLE_AMOUNT")]
    public int AvailableAmount { get; set; }

    [Column("MINIMUM_AMOUNT")]
    public int MinimumAmount { get; set; }

    [Column("RECIPIENT_EMAIL")]
    public string RecipientEmail { get; set; } = " ";

    [Column("TRIGGER_REASON")]
    public string TriggerReason { get; set; } = " ";
}
