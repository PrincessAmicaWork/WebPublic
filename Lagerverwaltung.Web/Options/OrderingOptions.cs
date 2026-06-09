using Lagerverwaltung.Web.Models;

namespace Lagerverwaltung.Web.Options;

public sealed class OrderingOptions
{
    public const string SectionName = "Ordering";

    public AppStockPolicy StockPolicy { get; set; } = AppStockPolicy.UseCatalogDefault;
}
