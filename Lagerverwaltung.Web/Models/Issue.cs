using System.ComponentModel.DataAnnotations.Schema;

namespace Lagerverwaltung.Web.Models
{
    // =================================================================
    //Explicitly tell EF Core the table name is "Issue" (singular).
    [Table("Issue")]
    // =================================================================
    public class Issue
    {
        public int ID { get; set; }
        public int PositionId { get; set; }
        public string TicketNumber { get; set; } = "";
        public string Username { get; set; } = "";
        public string CostCentre { get; set; } = "";
        public DateTime IssueDate { get; set; }
        public DateTime? TakeBackDate { get; set; }

        // Navigation property to Position is optional but good practice
        // public virtual Position Position { get; set; }
    }
}
