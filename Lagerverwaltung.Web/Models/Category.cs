using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace Lagerverwaltung.Web.Models
{
    [Table("Categories")]
    public class Category
    {
        public int ID { get; set; }
        public string Name { get; set; } = " ";
        public string Comment { get; set; } = " ";

        // Shared database multi-site support. Existing storage data is migrated to Solothurn (Id = 1).
        public int SiteId { get; set; } = 1;
        public virtual Site? Site { get; set; }

        public int MinimumAmount { get; set; } = 0;


        [Column("NeedsNotification")]
        public int NeedsNotificationValue { get; set; } = 0;

        [NotMapped]
        public bool NeedsNotification
        {
            get => NeedsNotificationValue == 1;
            set => NeedsNotificationValue = value ? 1 : 0;
        }

        [Column("RESTOCK_EMAIL")]
        public string RestockEmail { get; set; } = " ";

        public virtual ICollection<Position> Positions { get; set; } = new List<Position>();

        public override string ToString() => Name;

        [NotMapped]
        public bool CriticalAmount => MinimumAmount >= AvaliableAmount;

        [NotMapped]
        public int CurrentAmount => Positions.Count;

        [NotMapped]
        public int AvaliableAmount => Positions.Count(x => x.Avaliable == "yes");
    }
}
