using System.ComponentModel.DataAnnotations.Schema;

namespace Lagerverwaltung.Web.Models
{
    public enum StockCondition
    {
        New = 0,
        Used = 1
    }

    /// <summary>
    /// Mirror of the WPF Position model. Same Oracle table, same columns plus STOCK_CONDITION.
    /// </summary>
    [Table("Positions")]
    public class Position
    {
        public int ID { get; set; }
        public DateTime PurchaseDate { get; set; }
        public string Supplier { get; set; } = " ";
        public double Price { get; set; } = 0;
        public string Description { get; set; } = " ";
        public string OrderNumber { get; set; } = " ";

        // Shared database multi-site support. Existing storage data is migrated to Solothurn (Id = 1).
        public int SiteId { get; set; } = 1;

        public int CategoryId { get; set; }

        [Column("STOCK_CONDITION")]
        public StockCondition StockCondition { get; set; } = StockCondition.New;

        public virtual Site? Site { get; set; }
        public virtual Category Category { get; set; } = new();
        public virtual ICollection<Issue> Issued { get; set; } = new List<Issue>();

        [NotMapped]
        public string StockConditionLabel => StockCondition == StockCondition.Used
            ? "Gebraucht"
            : "Neu";

        [NotMapped]
        public string Avaliable => !Issued.Any(i => i.TakeBackDate == null) ? "yes" : "no";
    }
}
