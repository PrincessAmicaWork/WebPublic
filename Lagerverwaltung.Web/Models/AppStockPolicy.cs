namespace Lagerverwaltung.Web.Models;

public enum AppStockPolicy
{
    UseCatalogDefault = 0,
    NeverUseStorage = 1,
    PreferStorageButAllowExternal = 2,
    RequireStorageForStockItems = 3
}
