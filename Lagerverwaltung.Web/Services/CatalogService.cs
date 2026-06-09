using Lagerverwaltung.Web.Data;
using Lagerverwaltung.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace Lagerverwaltung.Web.Services;

public interface ICatalogService
{
    /// <summary>Returns active SiteCatalogItems for the currently selected site, ordered by SortOrder.</summary>
    Task<List<SiteCatalogItem>> GetActiveItemsAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns distinct active category names visible at the currently selected site.</summary>
    Task<List<string>> GetActiveCategoryNamesAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns a single SiteCatalogItem by its ID, only if it belongs to the current site.</summary>
    Task<SiteCatalogItem?> GetItemByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Returns the SiteCatalogItem for the given global catalog item at the current site.</summary>
    Task<SiteCatalogItem?> GetItemByCatalogItemIdAsync(int catalogItemId, CancellationToken cancellationToken = default);

    // ── Admin methods (not site-scoped) ──────────────────────────────────

    /// <summary>Returns all global RequestCatalogItems, ordered by ActionCode then ItemName.</summary>
    Task<List<RequestCatalogItem>> GetAllGlobalItemsAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns all SiteCatalogItems (active and inactive) for the given site, for admin use.</summary>
    Task<List<SiteCatalogItem>> GetAllItemsForSiteAsync(int siteId, CancellationToken cancellationToken = default);

    /// <summary>Creates or updates a SiteCatalogItem. If Id == 0 a new record is inserted.</summary>
    Task SaveSiteItemAsync(SiteCatalogItem item, CancellationToken cancellationToken = default);

    /// <summary>Permanently deletes a SiteCatalogItem by ID.</summary>
    Task DeleteSiteItemAsync(int id, CancellationToken cancellationToken = default);

    // ── Global catalog item management ───────────────────────────────────

    /// <summary>Returns a single global RequestCatalogItem by its ID (admin use).</summary>
    Task<RequestCatalogItem?> GetGlobalItemByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Creates or updates a global RequestCatalogItem. If Id == 0 a new record is inserted.</summary>
    Task SaveGlobalItemAsync(RequestCatalogItem item, CancellationToken cancellationToken = default);

    /// <summary>Returns all active sites for admin use.</summary>
    Task<List<Site>> GetAllSitesAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns categories for the given site (for admin override dropdowns).</summary>
    Task<List<Category>> GetCategoriesForSiteAsync(int siteId, CancellationToken cancellationToken = default);
}

public class CatalogService : ICatalogService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly ICurrentSiteService _currentSiteService;

    public CatalogService(
        IDbContextFactory<AppDbContext> dbFactory,
        ICurrentSiteService currentSiteService)
    {
        _dbFactory = dbFactory;
        _currentSiteService = currentSiteService;
    }

    public async Task<List<SiteCatalogItem>> GetActiveItemsAsync(CancellationToken cancellationToken = default)
    {
        var siteId = await _currentSiteService.GetCurrentSiteIdAsync(cancellationToken);
        if (siteId is null)
            return new List<SiteCatalogItem>();

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        return await db.SiteCatalogItems
            .AsNoTracking()
            .Include(x => x.CatalogItem)
            .Include(x => x.StorageCategory)
            .Where(x => x.SiteId == siteId.Value && x.IsActiveValue == 1)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.CatalogItem!.ActionCode)
            .ThenBy(x => x.CatalogItem!.ItemName)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<string>> GetActiveCategoryNamesAsync(CancellationToken cancellationToken = default)
    {
        var siteId = await _currentSiteService.GetCurrentSiteIdAsync(cancellationToken);
        if (siteId is null)
            return new List<string>();

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        return await db.SiteCatalogItems
            .AsNoTracking()
            .Include(x => x.CatalogItem)
            .Where(x => x.SiteId == siteId.Value && x.IsActiveValue == 1)
            .Select(x => x.CatalogItem!.CategoryName)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);
    }

    public async Task<SiteCatalogItem?> GetItemByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        if (id <= 0)
            return null;

        var siteId = await _currentSiteService.GetCurrentSiteIdAsync(cancellationToken);
        if (siteId is null)
            return null;

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        return await db.SiteCatalogItems
            .AsNoTracking()
            .Include(x => x.CatalogItem)
            .Include(x => x.StorageCategory)
            .FirstOrDefaultAsync(x => x.Id == id && x.SiteId == siteId.Value, cancellationToken);
    }

    public async Task<SiteCatalogItem?> GetItemByCatalogItemIdAsync(
        int catalogItemId,
        CancellationToken cancellationToken = default)
    {
        if (catalogItemId <= 0)
            return null;

        var siteId = await _currentSiteService.GetCurrentSiteIdAsync(cancellationToken);
        if (siteId is null)
            return null;

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        return await db.SiteCatalogItems
            .AsNoTracking()
            .Include(x => x.CatalogItem)
            .Include(x => x.StorageCategory)
            .FirstOrDefaultAsync(
                x => x.SiteId == siteId.Value && x.CatalogItemId == catalogItemId,
                cancellationToken);
    }

    public async Task<List<RequestCatalogItem>> GetAllGlobalItemsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        return await db.RequestCatalogItems
            .AsNoTracking()
            .Include(x => x.StorageCategory)
            .OrderBy(x => x.ActionCode)
            .ThenBy(x => x.ItemName)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<SiteCatalogItem>> GetAllItemsForSiteAsync(
        int siteId,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        return await db.SiteCatalogItems
            .AsNoTracking()
            .Include(x => x.CatalogItem)
            .Include(x => x.StorageCategory)
            .Where(x => x.SiteId == siteId)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.CatalogItem!.ActionCode)
            .ThenBy(x => x.CatalogItem!.ItemName)
            .ToListAsync(cancellationToken);
    }

    public async Task SaveSiteItemAsync(SiteCatalogItem item, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        if (item.Id == 0)
        {
            db.SiteCatalogItems.Add(item);
        }
        else
        {
            var existing = await db.SiteCatalogItems
                .FirstOrDefaultAsync(x => x.Id == item.Id, cancellationToken)
                ?? throw new InvalidOperationException(
                    $"SiteCatalogItem with ID {item.Id} was not found. It may have been deleted by another user.");

            existing.SiteId = item.SiteId;
            existing.CatalogItemId = item.CatalogItemId;
            existing.IsActiveValue = item.IsActiveValue;
            existing.SortOrder = item.SortOrder;
            existing.FulfillmentModeOverride = item.FulfillmentModeOverride;
            existing.StorageCategoryId = item.StorageCategoryId;
            existing.PriceOverride = item.PriceOverride;
            existing.BillingTypeOverride = item.BillingTypeOverride;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteSiteItemAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var item = await db.SiteCatalogItems
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (item is not null)
        {
            db.SiteCatalogItems.Remove(item);
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<RequestCatalogItem?> GetGlobalItemByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
            return null;

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        return await db.RequestCatalogItems
            .AsNoTracking()
            .Include(x => x.StorageCategory)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task SaveGlobalItemAsync(RequestCatalogItem item, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        if (item.Id == 0)
        {
            db.RequestCatalogItems.Add(item);
        }
        else
        {
            var existing = await db.RequestCatalogItems
                .FirstOrDefaultAsync(x => x.Id == item.Id, cancellationToken)
                ?? throw new InvalidOperationException(
                    $"RequestCatalogItem with ID {item.Id} was not found.");

            existing.ActionCode = item.ActionCode;
            existing.CategoryName = item.CategoryName;
            existing.Manufacturer = item.Manufacturer;
            existing.ItemName = item.ItemName;
            existing.Currency = item.Currency;
            existing.Price = item.Price;
            existing.BillingType = item.BillingType;
            existing.FulfillmentMode = item.FulfillmentMode;
            existing.ReturnableValue = item.ReturnableValue;
            existing.RequiresCommentValue = item.RequiresCommentValue;
            existing.IsActiveValue = item.IsActiveValue;
            existing.StorageCategoryId = item.StorageCategoryId;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<Site>> GetAllSitesAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        return await db.Sites
            .AsNoTracking()
            .Where(s => s.IsActiveValue == 1)
            .OrderBy(s => s.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Category>> GetCategoriesForSiteAsync(
        int siteId,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        return await db.Categories
            .AsNoTracking()
            .Where(c => c.SiteId == siteId)
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);
    }
}