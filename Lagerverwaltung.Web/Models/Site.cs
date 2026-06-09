using System.ComponentModel.DataAnnotations.Schema;

namespace Lagerverwaltung.Web.Models;

[Table("SITES")]
public class Site
{
    public int Id { get; set; }

    public string Code { get; set; } = "";
    public string Name { get; set; } = "";

    [Column("IS_ACTIVE")]
    public int IsActiveValue { get; set; } = 1;

    [NotMapped]
    public bool IsActive
    {
        get => IsActiveValue == 1;
        set => IsActiveValue = value ? 1 : 0;
    }

    public AppStockPolicy StockPolicy { get; set; } = AppStockPolicy.UseCatalogDefault;

    public string ItEmail { get; set; } = " ";
    public string LowStockEmail { get; set; } = " ";

    public string DefaultCulture { get; set; } = "de-CH";
    public string AdminCulture { get; set; } = "de-CH";

    // Optional: Microsoft Entra group object id used only as a suggestion for preselecting a site.
    public string EntraGroupObjectId { get; set; } = " ";

    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<Category> Categories { get; set; } = new List<Category>();
    public virtual ICollection<Position> Positions { get; set; } = new List<Position>();
}
