using System.Diagnostics;
using Lagerverwaltung.Web.Data;
using Lagerverwaltung.Web.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Lagerverwaltung.Web.Services;

public record StorageDashboardSummary(
    int TotalItems,
    int AvailableItems,
    int IssuedItems,
    int CategoryCount,
    int LowStockCategoryCount,
    int NewAvailableItems,
    int UsedAvailableItems,
    int PendingReturnCount);

public record StorageCategorySummary(
    int CategoryId,
    string CategoryName,
    int TotalCount,
    int AvailableCount,
    int NewAvailableCount,
    int UsedAvailableCount,
    int MinimumAmount,
    bool NeedsNotification,
    string RestockEmail,
    bool IsCritical);

public record StorageItemRow(
    int PositionId,
    int CategoryId,
    string CategoryName,
    string Description,
    string OrderNumber,
    string Supplier,
    double Price,
    DateTime PurchaseDate,
    StockCondition StockCondition,
    string StockConditionText,
    bool IsAvailable,
    bool IsIssued,
    bool IsReserved,
    bool HasAnyHistory,
    string StatusText,
    bool CanEdit,
    bool CanRemove);

public record StorageItemPage(
    List<StorageItemRow> Items,
    int TotalCount,
    int Page,
    int PageSize);

public record AddStorageItemRequest(
    int CategoryId,
    string Description,
    string OrderNumber,
    string Supplier,
    double Price,
    DateTime PurchaseDate,
    int Quantity,
    StockCondition StockCondition);

public record UpdateStorageItemRequest(
    int PositionId,
    int CategoryId,
    string Description,
    string OrderNumber,
    string Supplier,
    double Price,
    DateTime PurchaseDate,
    StockCondition StockCondition);

public record SaveCategoryRequest(
    int CategoryId,
    string Name,
    string Comment,
    int MinimumAmount,
    bool NeedsNotification,
    string RestockEmail);

public interface IStorageService
{
    Task<List<Category>> GetCategoriesAsync();
    Task<StorageDashboardSummary> GetDashboardSummaryAsync();
    Task<List<StorageCategorySummary>> GetCategorySummariesAsync();
    Task<List<StorageItemRow>> GetItemsAsync(int? categoryId, string? search, bool onlyAvailable);
    Task<StorageItemPage> GetItemsPageAsync(int? categoryId, string? search, bool onlyAvailable, int page, int pageSize);

    Task<(bool ok, string error)> AddItemsAsync(AddStorageItemRequest request);
    Task<(bool ok, string error)> UpdateItemAsync(UpdateStorageItemRequest request);
    Task<(bool ok, string error)> DeleteItemAsync(int positionId, string comment);
    Task<(bool ok, string error)> MarkDestroyedAsync(int positionId, string comment);
    Task<(bool ok, string error)> ForceDeleteItemAsync(int positionId);
    Task<(bool ok, string error)> ForceDeleteItemAsync(int positionId, int confirmationPositionId, string comment);

    Task<(bool ok, string error)> CreateCategoryAsync(SaveCategoryRequest request);
    Task<(bool ok, string error)> UpdateCategoryAsync(SaveCategoryRequest request);
    Task<(bool ok, string error)> DeleteCategoryAsync(int categoryId);
}

public class StorageService : IStorageService
{
    private const int MaxPageSize = 200;

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

    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<StorageService> _log;
    private readonly ICurrentSiteService _currentSiteService;

    public StorageService(
    IDbContextFactory<AppDbContext> dbFactory,
    IServiceScopeFactory scopeFactory,
    ILogger<StorageService> log,
    ICurrentSiteService currentSiteService)
    {
        _dbFactory = dbFactory;
        _scopeFactory = scopeFactory;
        _log = log;
        _currentSiteService = currentSiteService;
    }

    public async Task<List<Category>> GetCategoriesAsync()
    {
        

        var siteId = await GetCurrentSiteIdAsync();

        if (siteId is null)
            return [];

        await using var db = await _dbFactory.CreateDbContextAsync();

        return await db.Categories
            .AsNoTracking()
            .Where(c =>
                c.SiteId == siteId &&
                c.Name != null &&
                c.Name.Trim() != "")
            .OrderBy(c => c.Name)
            .ToListAsync();
    }

    public async Task<StorageDashboardSummary> GetDashboardSummaryAsync()
    {

        var sw = Stopwatch.StartNew();
        var siteId = await GetCurrentSiteIdAsync();

        if (siteId is null)
        {
            return new StorageDashboardSummary(
                0, 0, 0, 0, 0, 0, 0, 0);
        }

        await using var db = await _dbFactory.CreateDbContextAsync();

        var totalItems = await db.Positions
            .AsNoTracking()
            .CountAsync(p => p.SiteId == siteId);

        var availableQuery = db.Positions
            .AsNoTracking()
            .Where(p =>
            p.SiteId == siteId &&
            !db.Issues.Any(i => i.PositionId == p.ID && i.TakeBackDate == null) &&
            !db.EquipmentRequests.Any(r => r.PositionId == p.ID && ReservedRequestStatuses.Contains(r.Status)) &&
            !db.EquipmentOrderLines.Any(l => l.PositionId == p.ID && ReservedOrderLineStatuses.Contains(l.Status)));

        var availableItems = await availableQuery.CountAsync();
        var newAvailableItems = await availableQuery.CountAsync(p => p.StockCondition == StockCondition.New);
        var usedAvailableItems = await availableQuery.CountAsync(p => p.StockCondition == StockCondition.Used);

        var categoryCount = await db.Categories
            .AsNoTracking()
            .CountAsync(c =>
                c.SiteId == siteId &&
                c.Name != null &&
                c.Name.Trim() != "");

        var pendingReturnCount = await db.ReturnRequests
            .AsNoTracking()
            .CountAsync(r => r.Status == ReturnRequestStatus.Pending);

        var lowStockRows = await db.Categories
            .AsNoTracking()
            .Where(c =>
                c.SiteId == siteId &&
                c.Name != null && c.Name.Trim() != "" &&
                c.MinimumAmount > 0)
            .Select(c => new
            {
                c.MinimumAmount,
                AvailableCount = db.Positions.Count(p =>
                    p.CategoryId == c.ID &&
                    !db.Issues.Any(i => i.PositionId == p.ID && i.TakeBackDate == null) &&
                    !db.EquipmentRequests.Any(r => r.PositionId == p.ID && ReservedRequestStatuses.Contains(r.Status)) &&
                    !db.EquipmentOrderLines.Any(l => l.PositionId == p.ID && ReservedOrderLineStatuses.Contains(l.Status)))
            })
            .ToListAsync();

        var result = new StorageDashboardSummary(
            TotalItems: totalItems,
            AvailableItems: availableItems,
            IssuedItems: Math.Max(0, totalItems - availableItems),
            CategoryCount: categoryCount,
            LowStockCategoryCount: lowStockRows.Count(x => x.AvailableCount <= x.MinimumAmount),
            NewAvailableItems: newAvailableItems,
            UsedAvailableItems: usedAvailableItems,
            PendingReturnCount: pendingReturnCount);

        sw.Stop();
        _log.LogDebug("Storage dashboard loaded in {ElapsedMilliseconds} ms", sw.ElapsedMilliseconds);

        return result;
    }

    public async Task<List<StorageCategorySummary>> GetCategorySummariesAsync()
    {
        var sw = Stopwatch.StartNew();
        var siteId = await GetCurrentSiteIdAsync();

        if (siteId is null)
            return [];

        await using var db = await _dbFactory.CreateDbContextAsync();

        var rows = await db.Categories
            .AsNoTracking()
            .Where(c => c.SiteId == siteId && c.Name != null && c.Name.Trim() != "")
            .OrderBy(c => c.Name)
            .Select(c => new
            {
                CategoryId = c.ID,
                CategoryName = c.Name,
                c.MinimumAmount,
                NeedsNotification = c.NeedsNotificationValue == 1,
                c.RestockEmail,
                TotalCount = db.Positions.Count(p => p.SiteId == siteId && p.CategoryId == c.ID),
                AvailableCount = db.Positions.Count(p => 
                    p.SiteId == siteId &&
                    p.CategoryId == c.ID &&
                    !db.Issues.Any(i => i.PositionId == p.ID && i.TakeBackDate == null) &&
                    !db.EquipmentRequests.Any(r => r.PositionId == p.ID && ReservedRequestStatuses.Contains(r.Status)) &&
                    !db.EquipmentOrderLines.Any(l => l.PositionId == p.ID && ReservedOrderLineStatuses.Contains(l.Status))),
                NewAvailableCount = db.Positions.Count(p =>
                    p.SiteId == siteId &&
                    p.CategoryId == c.ID &&
                    p.StockCondition == StockCondition.New &&
                    !db.Issues.Any(i => i.PositionId == p.ID && i.TakeBackDate == null) &&
                    !db.EquipmentRequests.Any(r => r.PositionId == p.ID && ReservedRequestStatuses.Contains(r.Status)) &&
                    !db.EquipmentOrderLines.Any(l => l.PositionId == p.ID && ReservedOrderLineStatuses.Contains(l.Status))),
                UsedAvailableCount = db.Positions.Count(p =>
                    p.SiteId == siteId &&
                    p.CategoryId == c.ID &&
                    p.StockCondition == StockCondition.Used &&
                    !db.Issues.Any(i => i.PositionId == p.ID && i.TakeBackDate == null) &&
                    !db.EquipmentRequests.Any(r => r.PositionId == p.ID && ReservedRequestStatuses.Contains(r.Status)) &&
                    !db.EquipmentOrderLines.Any(l => l.PositionId == p.ID && ReservedOrderLineStatuses.Contains(l.Status)))
            })
            .ToListAsync();

        var result = rows
            .Select(c => new StorageCategorySummary(
                CategoryId: c.CategoryId,
                CategoryName: NormalizeDisplay(c.CategoryName),
                TotalCount: c.TotalCount,
                AvailableCount: c.AvailableCount,
                NewAvailableCount: c.NewAvailableCount,
                UsedAvailableCount: c.UsedAvailableCount,
                MinimumAmount: c.MinimumAmount,
                NeedsNotification: c.NeedsNotification,
                RestockEmail: NormalizeDisplay(c.RestockEmail),
                IsCritical: c.MinimumAmount > 0 && c.AvailableCount <= c.MinimumAmount))
            .ToList();

        sw.Stop();
        _log.LogDebug("Storage category summaries loaded in {ElapsedMilliseconds} ms", sw.ElapsedMilliseconds);

        return result;
    }

    public async Task<List<StorageItemRow>> GetItemsAsync(int? categoryId, string? search, bool onlyAvailable)
    {
        var page = await GetItemsPageInternalAsync(
            categoryId,
            search,
            onlyAvailable,
            page: 1,
            pageSize: int.MaxValue,
            capPageSize: false);

        return page.Items;
    }

    public Task<StorageItemPage> GetItemsPageAsync(
        int? categoryId,
        string? search,
        bool onlyAvailable,
        int page,
        int pageSize)
    {
        return GetItemsPageInternalAsync(
            categoryId,
            search,
            onlyAvailable,
            page,
            pageSize,
            capPageSize: true);
    }

    public async Task<(bool ok, string error)> AddItemsAsync(AddStorageItemRequest request)
    {
        if (request.CategoryId <= 0)
            return (false, "Bitte eine Kategorie auswählen.");

        if (request.Quantity <= 0)
            return (false, "Die Menge muss mindestens 1 sein.");

        if (request.Quantity > 500)
            return (false, "Bitte nicht mehr als 500 Artikel auf einmal anlegen.");

        if (!Enum.IsDefined(typeof(StockCondition), request.StockCondition))
            return (false, "Ungültiger Zustand.");

        var description = NormalizeRequired(request.Description);
        var orderNumber = NormalizeOptional(request.OrderNumber);
        var supplier = NormalizeOptional(request.Supplier);

        if (description == " ")
            return (false, "Bitte eine Beschreibung eingeben.");

        await using var db = await _dbFactory.CreateDbContextAsync();

        var categoryExists = await db.Categories.AnyAsync(c => c.ID == request.CategoryId);
        if (!categoryExists)
            return (false, "Die ausgewählte Kategorie existiert nicht mehr.");

        for (var i = 0; i < request.Quantity; i++)
        {
            db.Positions.Add(new Position
            {
                CategoryId = request.CategoryId,
                Category = null!,
                Description = description,
                OrderNumber = orderNumber,
                Supplier = supplier,
                Price = Math.Max(0, request.Price),
                PurchaseDate = request.PurchaseDate,
                StockCondition = request.StockCondition
            });
        }

        await db.SaveChangesAsync();

        return (true, "");
    }

    public async Task<(bool ok, string error)> UpdateItemAsync(UpdateStorageItemRequest request)
    {
        if (request.PositionId <= 0)
            return (false, "Ungültige Positions-ID.");

        if (request.CategoryId <= 0)
            return (false, "Bitte eine Kategorie auswählen.");

        if (!Enum.IsDefined(typeof(StockCondition), request.StockCondition))
            return (false, "Ungültiger Zustand.");

        var description = NormalizeRequired(request.Description);
        if (description == " ")
            return (false, "Bitte eine Beschreibung eingeben.");

        await using var db = await _dbFactory.CreateDbContextAsync();

        var position = await db.Positions.FirstOrDefaultAsync(p => p.ID == request.PositionId);
        if (position is null)
            return (false, "Artikel wurde nicht gefunden.");

        var categoryExists = await db.Categories.AnyAsync(c => c.ID == request.CategoryId);
        if (!categoryExists)
            return (false, "Die ausgewählte Kategorie existiert nicht mehr.");

        position.CategoryId = request.CategoryId;
        position.Description = description;
        position.OrderNumber = NormalizeOptional(request.OrderNumber);
        position.Supplier = NormalizeOptional(request.Supplier);
        position.Price = Math.Max(0, request.Price);
        position.PurchaseDate = request.PurchaseDate;
        position.StockCondition = request.StockCondition;

        await db.SaveChangesAsync();

        QueueLowStockCheck($"Artikel bearbeitet: Position {position.ID}");

        return (true, "");
    }

    public Task<(bool ok, string error)> DeleteItemAsync(int positionId, string comment)
    {
        return RemovePositionAsync(positionId, comment, StorageRemoveMode.Deleted);
    }

    public Task<(bool ok, string error)> MarkDestroyedAsync(int positionId, string comment)
    {
        return RemovePositionAsync(positionId, comment, StorageRemoveMode.Destroyed);
    }

    public Task<(bool ok, string error)> ForceDeleteItemAsync(int positionId)
    {
        return ForceDeleteItemAsync(positionId, positionId, "Force Delete ohne Kommentar");
    }

    public async Task<(bool ok, string error)> ForceDeleteItemAsync(
        int positionId,
        int confirmationPositionId,
        string comment)
    {
        if (positionId <= 0)
            return (false, "Ungültige Positions-ID.");

        if (confirmationPositionId != positionId)
            return (false, "Die bestätigte Positions-ID stimmt nicht mit dem Artikel überein.");

        var normalizedComment = NormalizeRequired(comment);
        if (normalizedComment == " ")
            return (false, "Bitte zuerst einen Grund für Force Delete eingeben.");

        await using var db = await _dbFactory.CreateDbContextAsync();
        await using var tx = await db.Database.BeginTransactionAsync();

        var position = await db.Positions
            .FirstOrDefaultAsync(p => p.ID == positionId);

        if (position is null)
            return (false, "Artikel wurde nicht gefunden.");

        var categoryId = position.CategoryId;
        var orderNumber = NormalizeOptional(position.OrderNumber);

        var requests = await db.EquipmentRequests
            .Where(r => r.PositionId == positionId)
            .ToListAsync();

        var requestIds = requests
            .Select(r => r.Id)
            .ToList();

        var returnRequests = await db.ReturnRequests
            .Where(r =>
                r.PositionId == positionId ||
                (r.EquipmentRequestId.HasValue && requestIds.Contains(r.EquipmentRequestId.Value)))
            .ToListAsync();

        var issues = await db.Issues
            .Where(i => i.PositionId == positionId)
            .ToListAsync();

        var destroyedLogs = await db.Destroyeds
            .Where(d => d.PositionID == positionId)
            .ToListAsync();

        db.Deleted.Add(new Deleted
        {
            DeletedDate = DateTime.Now,
            Comment = $"FORCE DELETE: {normalizedComment}",
            OrderNumber = orderNumber
        });

        db.ReturnRequests.RemoveRange(returnRequests);
        db.Destroyeds.RemoveRange(destroyedLogs);
        db.Issues.RemoveRange(issues);
        db.EquipmentRequests.RemoveRange(requests);
        db.Positions.Remove(position);

        await db.SaveChangesAsync();
        await tx.CommitAsync();

        _log.LogWarning(
            "Force deleted position {PositionId}. Category {CategoryId}. Reason: {Reason}",
            positionId,
            categoryId,
            normalizedComment);

        QueueLowStockCheck($"Artikel endgültig gelöscht: Position {positionId}, Kategorie {categoryId}. Grund: {normalizedComment}");

        return (true, "");
    }

    public async Task<(bool ok, string error)> CreateCategoryAsync(SaveCategoryRequest request)
    {
        var name = NormalizeRequired(request.Name);
        if (name == " ")
            return (false, "Bitte einen Kategorienamen eingeben.");

        await using var db = await _dbFactory.CreateDbContextAsync();

        var normalizedName = name.ToLower();

        var exists = await db.Categories
            .AnyAsync(c => c.Name != null && c.Name.ToLower() == normalizedName);

        if (exists)
            return (false, "Diese Kategorie existiert bereits.");

        db.Categories.Add(new Category
        {
            Name = name,
            Comment = NormalizeOptional(request.Comment),
            MinimumAmount = Math.Max(0, request.MinimumAmount),
            NeedsNotification = request.NeedsNotification,
            RestockEmail = NormalizeOptional(request.RestockEmail)
        });

        await db.SaveChangesAsync();

        return (true, "");
    }

    public async Task<(bool ok, string error)> UpdateCategoryAsync(SaveCategoryRequest request)
    {
        if (request.CategoryId <= 0)
            return (false, "Ungültige Kategorie-ID.");

        var name = NormalizeRequired(request.Name);
        if (name == " ")
            return (false, "Bitte einen Kategorienamen eingeben.");

        await using var db = await _dbFactory.CreateDbContextAsync();

        var category = await db.Categories.FirstOrDefaultAsync(c => c.ID == request.CategoryId);
        if (category is null)
            return (false, "Kategorie wurde nicht gefunden.");

        var normalizedName = name.ToLower();

        var duplicateExists = await db.Categories
            .AnyAsync(c => c.ID != request.CategoryId && c.Name != null && c.Name.ToLower() == normalizedName);

        if (duplicateExists)
            return (false, "Eine andere Kategorie mit diesem Namen existiert bereits.");

        category.Name = name;
        category.Comment = NormalizeOptional(request.Comment);
        category.MinimumAmount = Math.Max(0, request.MinimumAmount);
        category.NeedsNotification = request.NeedsNotification;
        category.RestockEmail = NormalizeOptional(request.RestockEmail);

        await db.SaveChangesAsync();

        if (category.NeedsNotification)
            QueueLowStockCheck($"Kategorie bearbeitet: {category.Name.Trim()}");

        return (true, "");
    }

    public async Task<(bool ok, string error)> DeleteCategoryAsync(int categoryId)
    {
        if (categoryId <= 0)
            return (false, "Ungültige Kategorie-ID.");

        await using var db = await _dbFactory.CreateDbContextAsync();

        var category = await db.Categories.FirstOrDefaultAsync(c => c.ID == categoryId);
        if (category is null)
            return (false, "Kategorie wurde nicht gefunden.");

        var hasPositions = await db.Positions.AnyAsync(p => p.CategoryId == categoryId);
        if (hasPositions)
            return (false, "Diese Kategorie hat noch Artikel und kann deshalb nicht gelöscht werden.");

        db.Categories.Remove(category);
        await db.SaveChangesAsync();

        return (true, "");
    }

    private async Task<StorageItemPage> GetItemsPageInternalAsync(
        int? categoryId,
        string? search,
        bool onlyAvailable,
        int page,
        int pageSize,
        bool capPageSize)
    {
        var sw = Stopwatch.StartNew();

        var normalizedPage = Math.Max(1, page);
        var normalizedPageSize = pageSize <= 0 ? 25 : pageSize;

        if (capPageSize)
            normalizedPageSize = Math.Clamp(normalizedPageSize, 10, MaxPageSize);

        await using var db = await _dbFactory.CreateDbContextAsync();

        var query = db.Positions
            .AsNoTracking()
            .AsQueryable();

        if (categoryId.HasValue && categoryId.Value > 0)
            query = query.Where(p => p.CategoryId == categoryId.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToLower();

            query = query.Where(p =>
                p.Description.ToLower().Contains(normalizedSearch) ||
                p.OrderNumber.ToLower().Contains(normalizedSearch) ||
                p.Supplier.ToLower().Contains(normalizedSearch) ||
                p.Category.Name.ToLower().Contains(normalizedSearch));
        }

        if (onlyAvailable)
        {
            query = query.Where(p =>
                !db.Issues.Any(i => i.PositionId == p.ID && i.TakeBackDate == null) &&
                !db.EquipmentRequests.Any(r => r.PositionId == p.ID && ReservedRequestStatuses.Contains(r.Status)) &&
                    !db.EquipmentOrderLines.Any(l => l.PositionId == p.ID && ReservedOrderLineStatuses.Contains(l.Status)));
        }

        var totalCount = await query.CountAsync();
        var skip = (normalizedPage - 1) * normalizedPageSize;

        var rows = await query
            .OrderBy(p => p.Category.Name)
            .ThenBy(p => p.Description)
            .ThenBy(p => p.OrderNumber)
            .ThenBy(p => p.ID)
            .Skip(skip)
            .Take(normalizedPageSize)
            .Select(p => new StorageItemProjection
            {
                PositionId = p.ID,
                CategoryId = p.CategoryId,
                CategoryName = p.Category.Name,
                Description = p.Description,
                OrderNumber = p.OrderNumber,
                Supplier = p.Supplier,
                Price = p.Price,
                PurchaseDate = p.PurchaseDate,
                StockCondition = p.StockCondition,
                IsIssued = db.Issues.Any(i => i.PositionId == p.ID && i.TakeBackDate == null),
                IsReserved = db.EquipmentRequests.Any(r => r.PositionId == p.ID && ReservedRequestStatuses.Contains(r.Status)) ||
                             db.EquipmentOrderLines.Any(l => l.PositionId == p.ID && ReservedOrderLineStatuses.Contains(l.Status)),
                HasAnyIssue = db.Issues.Any(i => i.PositionId == p.ID),
                HasAnyRequest = db.EquipmentRequests.Any(r => r.PositionId == p.ID) ||
                                db.EquipmentOrderLines.Any(l => l.PositionId == p.ID)
            })
            .ToListAsync();

        var items = rows.Select(ToItemRow).ToList();

        sw.Stop();
        _log.LogDebug(
            "Storage items loaded in {ElapsedMilliseconds} ms. Page {Page}, PageSize {PageSize}, Total {TotalCount}",
            sw.ElapsedMilliseconds,
            normalizedPage,
            normalizedPageSize,
            totalCount);

        return new StorageItemPage(items, totalCount, normalizedPage, normalizedPageSize);
    }

    private async Task<(bool ok, string error)> RemovePositionAsync(
        int positionId,
        string comment,
        StorageRemoveMode mode)
    {
        if (positionId <= 0)
            return (false, "Ungültige Positions-ID.");

        var normalizedComment = NormalizeRequired(comment);
        if (normalizedComment == " ")
            return (false, "Bitte zuerst einen Kommentar für diese Aktion eingeben.");

        await using var db = await _dbFactory.CreateDbContextAsync();

        var position = await db.Positions
            .FirstOrDefaultAsync(p => p.ID == positionId);

        if (position is null)
            return (false, "Artikel wurde nicht gefunden.");

        var hasOpenIssue = await db.Issues.AnyAsync(i => i.PositionId == positionId && i.TakeBackDate == null);
        if (hasOpenIssue)
            return (false, "Dieser Artikel ist aktuell ausgegeben und kann nicht entfernt werden.");

        var hasReservedRequest = await db.EquipmentRequests.AnyAsync(r =>
            r.PositionId == positionId && ReservedRequestStatuses.Contains(r.Status));

        var hasReservedOrderLine = await db.EquipmentOrderLines.AnyAsync(l =>
            l.PositionId == positionId && ReservedOrderLineStatuses.Contains(l.Status));

        if (hasReservedRequest || hasReservedOrderLine)
            return (false, "Dieser Artikel ist aktuell reserviert und kann nicht entfernt werden.");

        var hasAnyIssue = await db.Issues.AnyAsync(i => i.PositionId == positionId);
        var hasAnyRequest = await db.EquipmentRequests.AnyAsync(r => r.PositionId == positionId) ||
                            await db.EquipmentOrderLines.AnyAsync(l => l.PositionId == positionId);

        if (hasAnyIssue || hasAnyRequest)
        {
            return (false,
                "Dieser Artikel hat bereits Verlauf/Anfragen. Für solche Artikel bitte FORCE LÖSCHEN nur bei echten Testdaten verwenden.");
        }

        var categoryId = position.CategoryId;

        if (mode == StorageRemoveMode.Deleted)
        {
            db.Deleted.Add(new Deleted
            {
                DeletedDate = DateTime.Now,
                Comment = normalizedComment,
                OrderNumber = NormalizeOptional(position.OrderNumber)
            });
        }
        else
        {
            db.Destroyeds.Add(new Destroyed
            {
                PositionID = position.ID,
                PositionOrderNumber = NormalizeOptional(position.OrderNumber),
                DestroyDate = DateTime.Now,
                Comment = normalizedComment
            });
        }

        db.Positions.Remove(position);
        await db.SaveChangesAsync();

        QueueLowStockCheck($"Artikel entfernt: Position {positionId}, Kategorie {categoryId}");

        return (true, "");
    }

    private void QueueLowStockCheck(string triggerReason)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var lowStock = scope.ServiceProvider.GetRequiredService<ILowStockNotificationService>();
                await lowStock.CheckAndNotifyAsync(triggerReason);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Mindestbestand-Prüfung im Hintergrund fehlgeschlagen. Auslöser: {TriggerReason}", triggerReason);
            }
        });
    }

    private static StorageItemRow ToItemRow(StorageItemProjection row)
    {
        var isAvailable = !row.IsIssued && !row.IsReserved;
        var hasAnyHistory = row.HasAnyIssue || row.HasAnyRequest;

        var statusText = row.IsIssued
            ? "Ausgegeben"
            : row.IsReserved
                ? "Reserviert"
                : "Verfügbar";

        var stockConditionText = row.StockCondition == StockCondition.Used
            ? "Gebraucht"
            : "Neu";

        return new StorageItemRow(
            PositionId: row.PositionId,
            CategoryId: row.CategoryId,
            CategoryName: NormalizeDisplay(row.CategoryName),
            Description: NormalizeDisplay(row.Description),
            OrderNumber: NormalizeDisplay(row.OrderNumber),
            Supplier: NormalizeDisplay(row.Supplier),
            Price: row.Price,
            PurchaseDate: row.PurchaseDate,
            StockCondition: row.StockCondition,
            StockConditionText: stockConditionText,
            IsAvailable: isAvailable,
            IsIssued: row.IsIssued,
            IsReserved: row.IsReserved,
            HasAnyHistory: hasAnyHistory,
            StatusText: statusText,
            CanEdit: true,
            CanRemove: isAvailable && !hasAnyHistory);
    }

    private static string NormalizeDisplay(string? value)
    {
        return value?.Trim() ?? "";
    }

    private static string NormalizeRequired(string? value)
    {
        var normalized = value?.Trim() ?? "";
        return string.IsNullOrWhiteSpace(normalized) ? " " : normalized;
    }

    private static string NormalizeOptional(string? value)
    {
        var normalized = value?.Trim() ?? "";
        return string.IsNullOrWhiteSpace(normalized) ? " " : normalized;
    }

    private sealed class StorageItemProjection
    {
        public int PositionId { get; set; }
        public int CategoryId { get; set; }
        public string? CategoryName { get; set; }
        public string? Description { get; set; }
        public string? OrderNumber { get; set; }
        public string? Supplier { get; set; }
        public double Price { get; set; }
        public DateTime PurchaseDate { get; set; }
        public StockCondition StockCondition { get; set; }
        public bool IsIssued { get; set; }
        public bool IsReserved { get; set; }
        public bool HasAnyIssue { get; set; }
        public bool HasAnyRequest { get; set; }
    }

    private enum StorageRemoveMode
    {
        Deleted,
        Destroyed
    }
    private async Task<int?> GetCurrentSiteIdAsync()
    {
        return await _currentSiteService.GetCurrentSiteIdAsync();
    }
}
