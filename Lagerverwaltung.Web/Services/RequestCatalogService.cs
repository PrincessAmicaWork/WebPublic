using Lagerverwaltung.Web.Data;
using Lagerverwaltung.Web.Models;
using Lagerverwaltung.Web.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Lagerverwaltung.Web.Services;

public sealed record CatalogItemAvailability(
    int CatalogItemId,
    bool UsesStorage,
    bool HasStorageCategory,
    int AvailableTotal,
    int AvailableNew,
    int AvailableUsed)
{
    public bool IsAvailable => !UsesStorage || (HasStorageCategory && AvailableTotal > 0);

    public static CatalogItemAvailability NoStorage(int catalogItemId) =>
        new(catalogItemId, UsesStorage: false, HasStorageCategory: false, AvailableTotal: 0, AvailableNew: 0, AvailableUsed: 0);

    public static CatalogItemAvailability MissingStorageCategory(int catalogItemId) =>
        new(catalogItemId, UsesStorage: true, HasStorageCategory: false, AvailableTotal: 0, AvailableNew: 0, AvailableUsed: 0);
}


public interface IRequestCatalogService
{
    Task<List<RequestCatalogItem>> GetActiveItemsAsync(CancellationToken cancellationToken = default);
    Task<List<string>> GetActiveCategoryNamesAsync(CancellationToken cancellationToken = default);
    Task<RequestCatalogItem?> GetActiveItemByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<List<RequestCatalogItem>> GetActiveItemsByIdsAsync(IEnumerable<int> ids, CancellationToken cancellationToken = default);
    Task<Dictionary<int, CatalogItemAvailability>> GetAvailabilityByItemIdAsync(IEnumerable<int> ids, CancellationToken cancellationToken = default);
}

public class RequestCatalogService : IRequestCatalogService
{
    private static readonly RequestStatus[] ReservedRequestStatuses =
    {
        RequestStatus.Pending,
        RequestStatus.Approved,
        RequestStatus.Preparing
    };

    private static readonly OrderLineStatus[] ReservedOrderLineStatuses =
    {
        OrderLineStatus.Preparing,
        OrderLineStatus.ReadyForPickup
    };

    private readonly ICurrentSiteService _currentSiteService;
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly OrderingOptions _orderingOptions;

    public RequestCatalogService(
    IDbContextFactory<AppDbContext> dbFactory,
    IOptions<OrderingOptions> orderingOptions,
    ICurrentSiteService currentSiteService)
    {
        _dbFactory = dbFactory;
        _orderingOptions = orderingOptions.Value;
        _currentSiteService = currentSiteService;
    }

    public async Task<List<RequestCatalogItem>> GetActiveItemsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        return await db.RequestCatalogItems
            .AsNoTracking()
            .Include(x => x.StorageCategory)
            .Where(x => x.IsActiveValue == 1)
            .OrderBy(x => x.ActionCode)
            .ThenBy(x => x.ItemName)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<string>> GetActiveCategoryNamesAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        return await db.RequestCatalogItems
            .AsNoTracking()
            .Where(x => x.IsActiveValue == 1)
            .Select(x => x.CategoryName)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);
    }

    public async Task<RequestCatalogItem?> GetActiveItemByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        if (id <= 0)
            return null;

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        return await db.RequestCatalogItems
            .AsNoTracking()
            .Include(x => x.StorageCategory)
            .FirstOrDefaultAsync(x => x.Id == id && x.IsActiveValue == 1, cancellationToken);
    }

    public async Task<List<RequestCatalogItem>> GetActiveItemsByIdsAsync(
        IEnumerable<int> ids,
        CancellationToken cancellationToken = default)
    {
        var idList = ids
            .Where(id => id > 0)
            .Distinct()
            .ToList();

        if (idList.Count == 0)
            return new List<RequestCatalogItem>();

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        return await db.RequestCatalogItems
            .AsNoTracking()
            .Include(x => x.StorageCategory)
            .Where(x => x.IsActiveValue == 1 && idList.Contains(x.Id))
            .OrderBy(x => x.ActionCode)
            .ThenBy(x => x.ItemName)
            .ToListAsync(cancellationToken);
    }


    public async Task<Dictionary<int, CatalogItemAvailability>> GetAvailabilityByItemIdAsync(
        IEnumerable<int> ids,
        CancellationToken cancellationToken = default)
    {
        var idList = ids
            .Where(id => id > 0)
            .Distinct()
            .ToList();

        if (idList.Count == 0)
            return new Dictionary<int, CatalogItemAvailability>();

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var items = await db.RequestCatalogItems
            .AsNoTracking()
            .Where(x => x.IsActiveValue == 1 && idList.Contains(x.Id))
            .Select(x => new
            {
                x.Id,
                x.FulfillmentMode,
                x.StorageCategoryId
            })
            .ToListAsync(cancellationToken);

        var result = new Dictionary<int, CatalogItemAvailability>();

        var currentSite = await _currentSiteService.GetCurrentSiteAsync(cancellationToken);
        var stockPolicy = currentSite?.StockPolicy ?? _orderingOptions.StockPolicy;
        var siteId = currentSite?.Id;

        var storageItems = items
            .Where(x => UsesStorage(x.FulfillmentMode, stockPolicy))
            .ToList();

        foreach (var item in items.Where(x => !UsesStorage(x.FulfillmentMode, stockPolicy)))
            result[item.Id] = CatalogItemAvailability.NoStorage(item.Id);

        foreach (var item in storageItems.Where(x => x.StorageCategoryId is null or <= 0))
            result[item.Id] = CatalogItemAvailability.MissingStorageCategory(item.Id);

        var storageCategoryIds = storageItems
            .Where(x => x.StorageCategoryId.HasValue && x.StorageCategoryId.Value > 0)
            .Select(x => x.StorageCategoryId!.Value)
            .Distinct()
            .ToList();

        if (storageCategoryIds.Count == 0)
            return result;

        var availablePositions = await db.Positions
            .AsNoTracking()
            .Where(p => 
                siteId.HasValue &&
                p.SiteId == siteId.Value &&
                storageCategoryIds.Contains(p.CategoryId) &&
                !db.Issues.Any(i => i.PositionId == p.ID && i.TakeBackDate == null) &&
                !db.EquipmentRequests.Any(r => r.PositionId == p.ID && ReservedRequestStatuses.Contains(r.Status)) &&
                !db.EquipmentOrderLines.Any(l =>
                    l.PositionId == p.ID &&
                    ReservedOrderLineStatuses.Contains(l.Status)))
            .Select(p => new { p.CategoryId, p.StockCondition })
            .ToListAsync(cancellationToken);

        var countsByCategory = availablePositions
            .GroupBy(x => x.CategoryId)
            .ToDictionary(
                g => g.Key,
                g => new
                {
                    Total = g.Count(),
                    New = g.Count(x => x.StockCondition == StockCondition.New),
                    Used = g.Count(x => x.StockCondition == StockCondition.Used)
                });

        foreach (var item in storageItems.Where(x => x.StorageCategoryId.HasValue && x.StorageCategoryId.Value > 0))
        {
            var categoryId = item.StorageCategoryId!.Value;
            countsByCategory.TryGetValue(categoryId, out var counts);

            result[item.Id] = new CatalogItemAvailability(
                item.Id,
                UsesStorage: true,
                HasStorageCategory: true,
                AvailableTotal: counts?.Total ?? 0,
                AvailableNew: counts?.New ?? 0,
                AvailableUsed: counts?.Used ?? 0);
        }

        return result;
    }

    private bool UsesStorage(CatalogFulfillmentMode mode, AppStockPolicy stockPolicy)
    {
        if (stockPolicy == AppStockPolicy.NeverUseStorage)
            return false;

        return mode is CatalogFulfillmentMode.StockManaged or CatalogFulfillmentMode.StockOrExternalOrder;
    }

}
