using System.Net;
using Lagerverwaltung.Web.Data;
using Lagerverwaltung.Web.Models;
using Lagerverwaltung.Web.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Lagerverwaltung.Web.Services;

public record SubmitEquipmentOrderLineRequest(
    int CatalogItemId,
    int Quantity,
    bool UsedItemOk,
    string UserComment);

public record SubmitEquipmentOrderRequest(
    string RequestedForName,
    string RequestedForEmail,
    string PickupContactName,
    string PickupContactEmail,
    string SupervisorName,
    string SupervisorEmail,
    string Reason,
    IReadOnlyList<SubmitEquipmentOrderLineRequest> Lines);

public record SubmitEquipmentOrderResult(
    bool Ok,
    string Error,
    int? OrderId = null,
    string TicketNumber = "",
    string Warning = "");

public record OrderLinePositionOption(
    int PositionId,
    string CategoryName,
    string Description,
    string OrderNumber,
    string Supplier,
    StockCondition StockCondition)
{
    public string StockConditionLabel => StockCondition == StockCondition.Used
        ? "Gebraucht"
        : "Neu";

    public string DisplayText
    {
        get
        {
            var description = string.IsNullOrWhiteSpace(Description) ? "-" : Description.Trim();
            var orderNumber = string.IsNullOrWhiteSpace(OrderNumber) || OrderNumber == " " ? "ohne Bestellnummer" : OrderNumber.Trim();
            var supplier = string.IsNullOrWhiteSpace(Supplier) || Supplier == " " ? "ohne Lieferant" : Supplier.Trim();

            return $"#{PositionId} | {CategoryName} | {description} | {orderNumber} | {supplier} | {StockConditionLabel}";
        }
    }
}

public interface IEquipmentOrderService
{
    Task<SubmitEquipmentOrderResult> SubmitOrderAsync(
        SubmitEquipmentOrderRequest request,
        CancellationToken cancellationToken = default);

    Task<(bool ok, string error)> ApproveByTokenAsync(
        int orderId,
        string token,
        CancellationToken cancellationToken = default);

    Task<(bool ok, string error)> DenyByTokenAsync(
        int orderId,
        string token,
        string bossComment,
        CancellationToken cancellationToken = default);

    Task<List<EquipmentOrder>> GetOrdersAsync(
        OrderStatus? status = null,
        CancellationToken cancellationToken = default);

    Task<List<EquipmentOrder>> GetOrdersForSupervisorAsync(
        string supervisorEmail,
        CancellationToken cancellationToken = default);

    Task<List<EquipmentOrder>> GetOrdersByEmailAsync(
        string email,
        CancellationToken cancellationToken = default);

    Task<EquipmentOrder?> GetOrderByIdAsync(
        int orderId,
        CancellationToken cancellationToken = default);

    Task<(bool ok, string error)> ApproveAsync(
        int orderId,
        string actorEmail,
        bool actorIsAdmin,
        string bossComment = "",
        CancellationToken cancellationToken = default);

    Task<(bool ok, string error)> DenyAsync(
        int orderId,
        string actorEmail,
        bool actorIsAdmin,
        string bossComment,
        CancellationToken cancellationToken = default);

    Task<(bool ok, string error, List<OrderLinePositionOption> positions)> GetAvailablePositionsForLineAsync(
        int orderId,
        int lineId,
        string actorEmail,
        bool actorIsAdmin,
        CancellationToken cancellationToken = default);

    Task<(bool ok, string error)> AssignPositionToLineAsync(
        int orderId,
        int lineId,
        int positionId,
        string actorEmail,
        bool actorIsAdmin,
        CancellationToken cancellationToken = default);

    Task<(bool ok, string error)> FulfillLineWithoutStorageAsync(
        int orderId,
        int lineId,
        string actorEmail,
        bool actorIsAdmin,
        CancellationToken cancellationToken = default);

    Task<(bool ok, string error)> CompleteLineHandoverAsync(
        int orderId,
        int lineId,
        string actorEmail,
        bool actorIsAdmin,
        CancellationToken cancellationToken = default);

    Task<(bool ok, string error)> MarkLinePreparingAsync(
        int orderId,
        int lineId,
        string actorEmail,
        bool actorIsAdmin,
        CancellationToken cancellationToken = default);
}

public class EquipmentOrderService : IEquipmentOrderService
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

    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly IEmailService _email;
    private readonly IConfiguration _configuration;
    private readonly ILogger<EquipmentOrderService> _log;
    private readonly IApproverService _approvers;
    private readonly ICurrentUserService _currentUser;
    private readonly OrderingOptions _orderingOptions;
    private readonly ICurrentSiteService _currentSiteService;

    public EquipmentOrderService(
        IDbContextFactory<AppDbContext> dbFactory,
        IEmailService email,
        IConfiguration configuration,
        ILogger<EquipmentOrderService> log,
        IApproverService approvers,
        ICurrentUserService currentUser,
        IOptions<OrderingOptions> orderingOptions,
        ICurrentSiteService currentSiteService)
    {
        _dbFactory = dbFactory;
        _email = email;
        _configuration = configuration;
        _log = log;
        _approvers = approvers;
        _currentUser = currentUser;
        _orderingOptions = orderingOptions.Value;
        _currentSiteService = currentSiteService; 
    }

    public async Task<SubmitEquipmentOrderResult> SubmitOrderAsync(
        SubmitEquipmentOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        static string Normalize(string? value) => value?.Trim() ?? "";

        var currentUser = await _currentUser.GetCurrentUserAsync(cancellationToken);

        if (!currentUser.IsAuthenticated)
            return new SubmitEquipmentOrderResult(false, "Bitte melde dich zuerst an.");

        var orderedByName = Normalize(currentUser.Name);
        var orderedByEmail = Normalize(currentUser.Email);

        var currentSite = await _currentSiteService.GetCurrentSiteAsync(cancellationToken);

        if (currentSite is null)
            return new SubmitEquipmentOrderResult(false, "Bitte zuerst einen Standort auswählen.");

        if (string.IsNullOrWhiteSpace(orderedByEmail))
            return new SubmitEquipmentOrderResult(false, "Deine E-Mail-Adresse konnte nicht aus dem Login gelesen werden.");

        if (string.IsNullOrWhiteSpace(orderedByName))
            orderedByName = orderedByEmail;

        if (request.Lines is null || request.Lines.Count == 0)
            return new SubmitEquipmentOrderResult(false, "Bitte mindestens einen Artikel in den Warenkorb legen.");

        var reason = Normalize(request.Reason);
        if (string.IsNullOrWhiteSpace(reason))
            return new SubmitEquipmentOrderResult(false, "Bitte eine Begründung angeben.");

        var approver = await _approvers.FindActiveByEmailAsync(request.SupervisorEmail);
        if (approver is null)
            return new SubmitEquipmentOrderResult(false, "Bitte einen gültigen Vorgesetzten aus der Liste auswählen.");

        var requestedForName = Normalize(request.RequestedForName);
        var requestedForEmail = Normalize(request.RequestedForEmail);
        var pickupContactName = Normalize(request.PickupContactName);
        var pickupContactEmail = Normalize(request.PickupContactEmail);

        if (string.IsNullOrWhiteSpace(requestedForName))
            requestedForName = orderedByName;

        if (string.IsNullOrWhiteSpace(requestedForEmail))
            requestedForEmail = orderedByEmail;

        if (string.IsNullOrWhiteSpace(pickupContactName))
            pickupContactName = orderedByName;

        if (string.IsNullOrWhiteSpace(pickupContactEmail))
            pickupContactEmail = orderedByEmail;

        var requestedLineItems = request.Lines
            .Where(x => x.CatalogItemId > 0)
            .Select(x => new
            {
                x.CatalogItemId,
                Quantity = Math.Clamp(x.Quantity, 1, 99),
                x.UsedItemOk,
                UserComment = Normalize(x.UserComment)
            })
            .ToList();

        if (requestedLineItems.Count == 0)
            return new SubmitEquipmentOrderResult(false, "Bitte mindestens einen gültigen Artikel auswählen.");

        var requestedCatalogIds = requestedLineItems
            .Select(x => x.CatalogItemId)
            .Distinct()
            .ToList();

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var catalogItems = await db.RequestCatalogItems
            .AsNoTracking()
            .Where(x => requestedCatalogIds.Contains(x.Id) && x.IsActiveValue == 1)
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        if (catalogItems.Count != requestedCatalogIds.Count)
            return new SubmitEquipmentOrderResult(false, "Mindestens ein ausgewählter Katalogartikel existiert nicht mehr oder ist inaktiv.");

        foreach (var lineRequest in requestedLineItems)
        {
            var item = catalogItems[lineRequest.CatalogItemId];

            if (item.RequiresComment && string.IsNullOrWhiteSpace(lineRequest.UserComment))
                return new SubmitEquipmentOrderResult(false, $"Bitte eine Notiz für '{item.DisplayName}' erfassen.");
        }

        var stockRequirements = new Dictionary<int, StockRequirement>();

        foreach (var lineRequest in requestedLineItems)
        {
            var item = catalogItems[lineRequest.CatalogItemId];
            var effectiveMode = DetermineEffectiveFulfillmentMode(item, currentSite.StockPolicy);

            if (effectiveMode != EffectiveFulfillmentMode.UseStorage)
                continue;

            if (item.StorageCategoryId is null or <= 0)
            {
                return new SubmitEquipmentOrderResult(
                    false,
                    $"'{item.DisplayName}' ist ein Lagerartikel, aber noch keiner Lagerkategorie zugeordnet.");
            }

            var categoryId = item.StorageCategoryId.Value;
            if (!stockRequirements.TryGetValue(categoryId, out var requirement))
            {
                requirement = new StockRequirement(categoryId, item.DisplayName);
                stockRequirements[categoryId] = requirement;
            }

            requirement.TotalQuantity += lineRequest.Quantity;
            if (!lineRequest.UsedItemOk)
                requirement.NewOnlyQuantity += lineRequest.Quantity;
        }

        foreach (var requirement in stockRequirements.Values)
        {
            var available = await GetAvailableStockCountsForCategoryAsync(db, requirement.CategoryId, currentSite.Id, cancellationToken);

            if (available.Total < requirement.TotalQuantity)
            {
                return new SubmitEquipmentOrderResult(
                    false,
                    $"'{requirement.ExampleItemName}' ist aktuell nicht verfügbar. Freier Lagerbestand: {available.Total}, benötigt: {requirement.TotalQuantity}.");
            }

            if (available.New < requirement.NewOnlyQuantity)
            {
                return new SubmitEquipmentOrderResult(
                    false,
                    $"Für '{requirement.ExampleItemName}' ist nicht genug Neuware verfügbar. Neu verfügbar: {available.New}, benötigt: {requirement.NewOnlyQuantity}. Bitte 'Gebrauchtgerät ist okay' wählen oder später erneut versuchen.");
            }
        }

        var order = new EquipmentOrder
        {
            SiteId = currentSite.Id,
            TicketNumber = $"PENDING-{Guid.NewGuid():N}",
            OrderedByName = orderedByName,
            OrderedByEmail = orderedByEmail,
            RequestedForName = requestedForName,
            RequestedForEmail = requestedForEmail,
            PickupContactName = pickupContactName,
            PickupContactEmail = pickupContactEmail,
            SupervisorName = approver.Name,
            SupervisorEmail = approver.Email,
            Reason = reason,
            BossComment = " ",
            Status = OrderStatus.PendingApproval,
            CreatedAt = DateTime.Now,
            DecisionDate = null,
            CompletedAt = null,
            ApproveToken = Guid.NewGuid().ToString("N"),
            DenyToken = Guid.NewGuid().ToString("N")
        };

        foreach (var lineRequest in requestedLineItems)
        {
            var item = catalogItems[lineRequest.CatalogItemId];
            var effectiveMode = DetermineEffectiveFulfillmentMode(item, currentSite.StockPolicy);
            var mustBeIndividualLines = ShouldUseIndividualLines(item, effectiveMode);

            if (mustBeIndividualLines)
            {
                for (var i = 0; i < lineRequest.Quantity; i++)
                {
                    order.Lines.Add(CreateOrderLine(
                        item,
                        effectiveMode,
                        quantity: 1,
                        lineRequest.UsedItemOk,
                        lineRequest.UserComment));
                }
            }
            else
            {
                order.Lines.Add(CreateOrderLine(
                    item,
                    effectiveMode,
                    lineRequest.Quantity,
                    lineRequest.UsedItemOk,
                    lineRequest.UserComment));
            }
        }

        db.EquipmentOrders.Add(order);
        await db.SaveChangesAsync(cancellationToken);

        order.TicketNumber = EquipmentOrder.BuildTicketNumber(order.Id);
        await db.SaveChangesAsync(cancellationToken);

        var warning = "";

        try
        {
            await SendSupervisorApprovalEmailAsync(order.Id, cancellationToken);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to send supervisor approval email for order {OrderId}", order.Id);
            warning = "Bestellung wurde gespeichert, aber die Freigabe-E-Mail konnte nicht gesendet werden.";
        }

        return new SubmitEquipmentOrderResult(
            Ok: true,
            Error: "",
            OrderId: order.Id,
            TicketNumber: order.TicketNumber,
            Warning: warning);
    }

    public async Task<(bool ok, string error)> ApproveByTokenAsync(
        int orderId,
        string token,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
            return (false, "Invalid link");

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var order = await db.EquipmentOrders
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == orderId && x.ApproveToken == token, cancellationToken);

        if (order is null)
            return (false, "Invalid link");

        if (order.Status != OrderStatus.PendingApproval)
            return (false, GetAlreadyHandledMessage(order.Status, approveAttempt: true));

        order.Status = OrderStatus.Approved;
        order.DecisionDate = DateTime.Now;
        order.BossComment = " ";

        foreach (var line in order.Lines.Where(x => x.Status == OrderLineStatus.WaitingForApproval))
        {
            line.Status = OrderLineStatus.Open;
        }

        await db.SaveChangesAsync(cancellationToken);

        try
        {
            await SendItNotificationAsync(order.Id, cancellationToken);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to send IT notification for order {OrderId}", order.Id);
            return (true, "Approved, but IT email failed");
        }

        return (true, "");
    }

    public async Task<(bool ok, string error)> DenyByTokenAsync(
        int orderId,
        string token,
        string bossComment,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
            return (false, "Invalid link");

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var order = await db.EquipmentOrders
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == orderId && x.DenyToken == token, cancellationToken);

        if (order is null)
            return (false, "Invalid link");

        if (order.Status != OrderStatus.PendingApproval)
            return (false, GetAlreadyHandledMessage(order.Status, approveAttempt: false));

        order.Status = OrderStatus.Denied;
        order.DecisionDate = DateTime.Now;
        order.BossComment = string.IsNullOrWhiteSpace(bossComment) ? " " : bossComment.Trim();

        foreach (var line in order.Lines.Where(x => x.Status == OrderLineStatus.WaitingForApproval))
        {
            line.Status = OrderLineStatus.Cancelled;
        }

        await db.SaveChangesAsync(cancellationToken);

        try
        {
            await SendDenialEmailAsync(order.Id, cancellationToken);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to send denial email for order {OrderId}", order.Id);
            return (true, "Denied, but user email failed");
        }

        return (true, "");
    }

    public async Task<List<EquipmentOrder>> GetOrdersAsync(
        OrderStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var query = db.EquipmentOrders
            .AsNoTracking()
            .Include(x => x.Lines)
            .AsQueryable();

        if (status.HasValue)
            query = query.Where(x => x.Status == status.Value);

        return await query
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<EquipmentOrder>> GetOrdersForSupervisorAsync(
        string supervisorEmail,
        CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeEmail(supervisorEmail);

        if (string.IsNullOrWhiteSpace(normalized))
            return new List<EquipmentOrder>();

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        return await db.EquipmentOrders
            .AsNoTracking()
            .Include(x => x.Lines)
            .Where(x => x.SupervisorEmail.ToLower() == normalized)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<EquipmentOrder>> GetOrdersByEmailAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeEmail(email);

        if (string.IsNullOrWhiteSpace(normalized))
            return new List<EquipmentOrder>();

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        return await db.EquipmentOrders
            .AsNoTracking()
            .Include(x => x.Lines)
            .Where(x =>
                x.OrderedByEmail.ToLower() == normalized ||
                x.RequestedForEmail.ToLower() == normalized ||
                x.PickupContactEmail.ToLower() == normalized)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<EquipmentOrder?> GetOrderByIdAsync(
        int orderId,
        CancellationToken cancellationToken = default)
    {
        if (orderId <= 0)
            return null;

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        return await db.EquipmentOrders
            .AsNoTracking()
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == orderId, cancellationToken);
    }

    public async Task<(bool ok, string error)> ApproveAsync(
        int orderId,
        string actorEmail,
        bool actorIsAdmin,
        string bossComment = "",
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var order = await db.EquipmentOrders
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == orderId, cancellationToken);

        if (order is null)
            return (false, "Bestellung wurde nicht gefunden.");

        if (order.Status != OrderStatus.PendingApproval)
            return (false, "Diese Bestellung wurde bereits bearbeitet.");

        if (!CanActOnOrder(order, actorEmail, actorIsAdmin))
            return (false, "Du darfst diese Bestellung nicht bearbeiten.");

        order.Status = OrderStatus.Approved;
        order.DecisionDate = DateTime.Now;
        order.BossComment = string.IsNullOrWhiteSpace(bossComment) ? " " : bossComment.Trim();

        foreach (var line in order.Lines.Where(x => x.Status == OrderLineStatus.WaitingForApproval))
        {
            line.Status = OrderLineStatus.Open;
        }

        await db.SaveChangesAsync(cancellationToken);

        try
        {
            await SendItNotificationAsync(order.Id, cancellationToken);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to send IT notification for order {OrderId}", order.Id);
            return (true, "Bestellung wurde genehmigt, aber die IT-E-Mail konnte nicht gesendet werden.");
        }

        return (true, "");
    }

    public async Task<(bool ok, string error)> DenyAsync(
        int orderId,
        string actorEmail,
        bool actorIsAdmin,
        string bossComment,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var order = await db.EquipmentOrders
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == orderId, cancellationToken);

        if (order is null)
            return (false, "Bestellung wurde nicht gefunden.");

        if (order.Status != OrderStatus.PendingApproval)
            return (false, "Diese Bestellung wurde bereits bearbeitet.");

        if (!CanActOnOrder(order, actorEmail, actorIsAdmin))
            return (false, "Du darfst diese Bestellung nicht bearbeiten.");

        order.Status = OrderStatus.Denied;
        order.DecisionDate = DateTime.Now;
        order.BossComment = string.IsNullOrWhiteSpace(bossComment) ? " " : bossComment.Trim();

        foreach (var line in order.Lines.Where(x => x.Status == OrderLineStatus.WaitingForApproval))
        {
            line.Status = OrderLineStatus.Cancelled;
        }

        await db.SaveChangesAsync(cancellationToken);

        try
        {
            await SendDenialEmailAsync(order.Id, cancellationToken);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to send denial email for order {OrderId}", order.Id);
            return (true, "Bestellung wurde abgelehnt, aber die Benutzer-E-Mail konnte nicht gesendet werden.");
        }

        return (true, "");
    }

    public async Task<(bool ok, string error)> MarkLinePreparingAsync(
        int orderId,
        int lineId,
        string actorEmail,
        bool actorIsAdmin,
        CancellationToken cancellationToken = default)
    {
        if (!actorIsAdmin)
            return (false, "Nur IT/Admins dürfen Bestellpositionen vorbereiten.");

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var order = await db.EquipmentOrders
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == orderId, cancellationToken);

        if (order is null)
            return (false, "Bestellung wurde nicht gefunden.");

        if (order.Status is not (OrderStatus.Approved or OrderStatus.PartiallyFulfilled))
            return (false, "Nur genehmigte Bestellungen können vorbereitet werden.");

        var line = order.Lines.FirstOrDefault(x => x.Id == lineId);
        if (line is null)
            return (false, "Bestellposition wurde nicht gefunden.");

        if (line.Status != OrderLineStatus.Open)
            return (false, "Nur offene Bestellpositionen können in Vorbereitung gesetzt werden.");

        line.Status = OrderLineStatus.Preparing;
        RecalculateOrderStatus(order);

        await db.SaveChangesAsync(cancellationToken);
        return (true, "");
    }

    public async Task<(bool ok, string error, List<OrderLinePositionOption> positions)> GetAvailablePositionsForLineAsync(
        int orderId,
        int lineId,
        string actorEmail,
        bool actorIsAdmin,
        CancellationToken cancellationToken = default)
    {
        if (!actorIsAdmin)
            return (false, "Nur IT/Admins dürfen Lagerpositionen zuweisen.", new List<OrderLinePositionOption>());

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var order = await db.EquipmentOrders
            .AsNoTracking()
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == orderId, cancellationToken);

        if (order is null)
            return (false, "Bestellung wurde nicht gefunden.", new List<OrderLinePositionOption>());

        if (order.Status is not (OrderStatus.Approved or OrderStatus.PartiallyFulfilled))
            return (false, "Nur genehmigte Bestellungen können vorbereitet werden.", new List<OrderLinePositionOption>());

        var line = order.Lines.FirstOrDefault(x => x.Id == lineId);
        if (line is null)
            return (false, "Bestellposition wurde nicht gefunden.", new List<OrderLinePositionOption>());

        if (line.Status != OrderLineStatus.Preparing)
            return (false, "Positionen können nur für Bestellpositionen in Vorbereitung zugewiesen werden.", new List<OrderLinePositionOption>());

        if (line.EffectiveFulfillmentMode != EffectiveFulfillmentMode.UseStorage)
            return (false, "Diese Bestellposition wird nicht über das Lager erfüllt.", new List<OrderLinePositionOption>());

        var catalogItem = await db.RequestCatalogItems
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == line.CatalogItemId, cancellationToken);

        if (catalogItem?.StorageCategoryId is null or <= 0)
            return (false, "Dieser Katalogartikel ist noch keiner Lagerkategorie zugeordnet.", new List<OrderLinePositionOption>());

        var storageCategoryId = catalogItem.StorageCategoryId.Value;
        var allowUsed = line.UsedItemOk;

        var query = db.Positions
            .AsNoTracking()
            .Include(x => x.Category)
            .Where(p =>
                p.CategoryId == storageCategoryId &&
                !db.Issues.Any(i => i.PositionId == p.ID && i.TakeBackDate == null) &&
                !db.EquipmentRequests.Any(r => r.PositionId == p.ID && ReservedRequestStatuses.Contains(r.Status)) &&
                !db.EquipmentOrderLines.Any(l =>
                    l.PositionId == p.ID &&
                    l.Id != lineId &&
                    ReservedOrderLineStatuses.Contains(l.Status)));

        if (!allowUsed)
            query = query.Where(p => p.StockCondition == StockCondition.New);

        var positions = await query
            .OrderBy(p => allowUsed && p.StockCondition == StockCondition.Used ? 0 : 1)
            .ThenBy(p => p.ID)
            .Select(p => new OrderLinePositionOption(
                p.ID,
                p.Category.Name,
                p.Description,
                p.OrderNumber,
                p.Supplier,
                p.StockCondition))
            .ToListAsync(cancellationToken);

        return (true, "", positions);
    }

    public async Task<(bool ok, string error)> AssignPositionToLineAsync(
        int orderId,
        int lineId,
        int positionId,
        string actorEmail,
        bool actorIsAdmin,
        CancellationToken cancellationToken = default)
    {
        if (!actorIsAdmin)
            return (false, "Nur IT/Admins dürfen Lagerpositionen zuweisen.");

        if (positionId <= 0)
            return (false, "Bitte eine Lagerposition auswählen.");

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var order = await db.EquipmentOrders
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == orderId, cancellationToken);

        if (order is null)
            return (false, "Bestellung wurde nicht gefunden.");

        if (order.Status is not (OrderStatus.Approved or OrderStatus.PartiallyFulfilled))
            return (false, "Nur genehmigte Bestellungen können vorbereitet werden.");

        var line = order.Lines.FirstOrDefault(x => x.Id == lineId);
        if (line is null)
            return (false, "Bestellposition wurde nicht gefunden.");

        if (line.Status != OrderLineStatus.Preparing)
            return (false, "Eine Lagerposition kann nur für Positionen in Vorbereitung zugewiesen werden.");

        if (line.EffectiveFulfillmentMode != EffectiveFulfillmentMode.UseStorage)
            return (false, "Diese Bestellposition wird nicht über das Lager erfüllt.");

        if (line.PositionId.HasValue)
            return (false, "Diese Bestellposition hat bereits eine Lagerposition.");

        var catalogItem = await db.RequestCatalogItems
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == line.CatalogItemId, cancellationToken);

        if (catalogItem?.StorageCategoryId is null or <= 0)
            return (false, "Dieser Katalogartikel ist noch keiner Lagerkategorie zugeordnet.");

        var position = await db.Positions
            .Include(x => x.Category)
            .FirstOrDefaultAsync(x => x.ID == positionId, cancellationToken);

        if (position is null)
            return (false, "Die ausgewählte Lagerposition wurde nicht gefunden.");

        if (position.CategoryId != catalogItem.StorageCategoryId.Value)
            return (false, "Die ausgewählte Lagerposition gehört nicht zur Lagerkategorie dieses Katalogartikels.");

        if (!line.UsedItemOk && position.StockCondition == StockCondition.Used)
            return (false, "Der Benutzer hat kein Gebrauchtgerät akzeptiert. Bitte Neuware auswählen.");

        var hasOpenIssue = await db.Issues.AnyAsync(
            i => i.PositionId == positionId && i.TakeBackDate == null,
            cancellationToken);

        if (hasOpenIssue)
            return (false, "Diese Lagerposition ist aktuell ausgegeben.");

        var hasReservedLegacyRequest = await db.EquipmentRequests.AnyAsync(
            r => r.PositionId == positionId && ReservedRequestStatuses.Contains(r.Status),
            cancellationToken);

        if (hasReservedLegacyRequest)
            return (false, "Diese Lagerposition ist bereits für eine alte Einzelanfrage reserviert.");

        var hasReservedOrderLine = await db.EquipmentOrderLines.AnyAsync(
            l => l.PositionId == positionId &&
                 l.Id != lineId &&
                 ReservedOrderLineStatuses.Contains(l.Status),
            cancellationToken);

        if (hasReservedOrderLine)
            return (false, "Diese Lagerposition ist bereits für eine andere Bestellung reserviert.");

        line.PositionId = position.ID;
        line.Status = OrderLineStatus.ReadyForPickup;
        line.AdminComment = $"Lagerposition #{position.ID} zugewiesen.";
        RecalculateOrderStatus(order);

        await db.SaveChangesAsync(cancellationToken);
        return (true, "");
    }

    public async Task<(bool ok, string error)> FulfillLineWithoutStorageAsync(
        int orderId,
        int lineId,
        string actorEmail,
        bool actorIsAdmin,
        CancellationToken cancellationToken = default)
    {
        if (!actorIsAdmin)
            return (false, "Nur IT/Admins dürfen Bestellpositionen erfüllen.");

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var order = await db.EquipmentOrders
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == orderId, cancellationToken);

        if (order is null)
            return (false, "Bestellung wurde nicht gefunden.");

        if (order.Status is not (OrderStatus.Approved or OrderStatus.PartiallyFulfilled))
            return (false, "Nur genehmigte Bestellungen können erfüllt werden.");

        var line = order.Lines.FirstOrDefault(x => x.Id == lineId);
        if (line is null)
            return (false, "Bestellposition wurde nicht gefunden.");

        if (line.Status != OrderLineStatus.Preparing)
            return (false, "Nur Positionen in Vorbereitung können manuell erfüllt werden.");

        if (!IsManualCompletable(line))
            return (false, "Diese Position benötigt eine Lagerposition oder einen eigenen Rückgabeprozess.");

        line.PositionId = null;
        line.Status = OrderLineStatus.Fulfilled;
        line.FulfilledAt = DateTime.Now;
        line.AdminComment = "Manuell ohne Lager erfüllt.";
        RecalculateOrderStatus(order);

        await db.SaveChangesAsync(cancellationToken);
        return (true, "");
    }

    public async Task<(bool ok, string error)> CompleteLineHandoverAsync(
        int orderId,
        int lineId,
        string actorEmail,
        bool actorIsAdmin,
        CancellationToken cancellationToken = default)
    {
        if (!actorIsAdmin)
            return (false, "Nur IT/Admins dürfen Lagerpositionen ausgeben.");

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var order = await db.EquipmentOrders
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == orderId, cancellationToken);

        if (order is null)
            return (false, "Bestellung wurde nicht gefunden.");

        if (order.Status is not (OrderStatus.Approved or OrderStatus.PartiallyFulfilled))
            return (false, "Nur genehmigte Bestellungen können ausgegeben werden.");

        var line = order.Lines.FirstOrDefault(x => x.Id == lineId);
        if (line is null)
            return (false, "Bestellposition wurde nicht gefunden.");

        if (line.Status != OrderLineStatus.ReadyForPickup)
            return (false, "Nur abholbereite Lagerpositionen können ausgegeben werden.");

        if (line.EffectiveFulfillmentMode != EffectiveFulfillmentMode.UseStorage)
            return (false, "Diese Bestellposition wird nicht über das Lager ausgegeben.");

        if (!line.PositionId.HasValue)
            return (false, "Diese Bestellposition hat noch keine Lagerposition.");

        var positionId = line.PositionId.Value;

        var positionExists = await db.Positions
            .AsNoTracking()
            .AnyAsync(p => p.ID == positionId, cancellationToken);

        if (!positionExists)
            return (false, "Die zugewiesene Lagerposition wurde nicht gefunden.");

        var ticketNumber = order.DisplayTicketNumber;

        var alreadyIssuedForThisOrder = await db.Issues.AnyAsync(
            i => i.PositionId == positionId &&
                 i.TicketNumber == ticketNumber &&
                 i.TakeBackDate == null,
            cancellationToken);

        if (!alreadyIssuedForThisOrder)
        {
            var hasOpenIssue = await db.Issues.AnyAsync(
                i => i.PositionId == positionId && i.TakeBackDate == null,
                cancellationToken);

            if (hasOpenIssue)
                return (false, "Diese Lagerposition ist bereits ausgegeben.");

            db.Issues.Add(new Issue
            {
                PositionId = positionId,
                TicketNumber = ticketNumber,
                Username = string.IsNullOrWhiteSpace(order.RequestedForName)
                    ? order.RequestedForEmail
                    : order.RequestedForName,
                CostCentre = string.IsNullOrWhiteSpace(order.SupervisorName)
                    ? " "
                    : order.SupervisorName,
                IssueDate = DateTime.Now,
                TakeBackDate = null
            });
        }

        line.Status = OrderLineStatus.Fulfilled;
        line.FulfilledAt = DateTime.Now;
        line.AdminComment = $"Lagerposition #{positionId} ausgegeben.";
        RecalculateOrderStatus(order);

        await db.SaveChangesAsync(cancellationToken);
        return (true, "");
    }

    private static bool IsManualCompletable(EquipmentOrderLine line)
    {
        return line.EffectiveFulfillmentMode is EffectiveFulfillmentMode.ManualNoStorage
            or EffectiveFulfillmentMode.ExternalOrder
            or EffectiveFulfillmentMode.ServiceChange
            or EffectiveFulfillmentMode.ReturnAction;
    }

    private static bool IsFinalLineStatus(OrderLineStatus status)
    {
        return status is OrderLineStatus.Fulfilled
            or OrderLineStatus.Cancelled
            or OrderLineStatus.Returned;
    }

    private static void RecalculateOrderStatus(EquipmentOrder order)
    {
        if (order.Lines.Count == 0)
            return;

        if (order.Lines.All(x => x.Status == OrderLineStatus.Cancelled))
        {
            order.Status = OrderStatus.Cancelled;
            return;
        }

        if (order.Lines.All(x => IsFinalLineStatus(x.Status)))
        {
            order.Status = OrderStatus.Completed;
            order.CompletedAt ??= DateTime.Now;
            return;
        }

        if (order.Lines.All(x => x.Status == OrderLineStatus.Open))
        {
            order.Status = OrderStatus.Approved;
            return;
        }

        if (order.Lines.Any(x => x.Status is OrderLineStatus.Preparing
                or OrderLineStatus.ReadyForPickup
                or OrderLineStatus.WaitingForExternalOrder
                or OrderLineStatus.Fulfilled))
        {
            order.Status = OrderStatus.PartiallyFulfilled;
        }
    }

    private static bool CanActOnOrder(EquipmentOrder order, string actorEmail, bool actorIsAdmin)
    {
        if (actorIsAdmin)
            return true;

        return string.Equals(
            order.SupervisorEmail?.Trim(),
            actorEmail?.Trim(),
            StringComparison.OrdinalIgnoreCase);
    }

    private EquipmentOrderLine CreateOrderLine(
        RequestCatalogItem item,
        EffectiveFulfillmentMode effectiveMode,
        int quantity,
        bool usedItemOk,
        string userComment)
    {
        return new EquipmentOrderLine
        {
            CatalogItemId = item.Id,
            PositionId = null,
            Quantity = Math.Max(1, quantity),
            Status = OrderLineStatus.WaitingForApproval,
            CatalogFulfillmentMode = item.FulfillmentMode,
            EffectiveFulfillmentMode = effectiveMode,
            Returnable = item.Returnable,
            UsedItemOk = usedItemOk,
            UserComment = string.IsNullOrWhiteSpace(userComment) ? " " : userComment.Trim(),
            AdminComment = " ",
            FulfilledAt = null,
            ActionCode = item.ActionCode,
            CategoryName = item.CategoryName,
            Manufacturer = item.Manufacturer,
            ItemName = item.ItemName,
            Currency = item.Currency,
            UnitPrice = item.Price,
            BillingType = item.BillingType
        };
    }


    private async Task<(int Total, int New, int Used)> GetAvailableStockCountsForCategoryAsync(
        AppDbContext db,
        int categoryId,
        int siteId,
        CancellationToken cancellationToken)
    {
        var availablePositions = await db.Positions
            .AsNoTracking()
            .Where(p =>
                p.SiteId == siteId &&
                p.CategoryId == categoryId &&
                !db.Issues.Any(i => i.PositionId == p.ID && i.TakeBackDate == null) &&
                !db.EquipmentRequests.Any(r => r.PositionId == p.ID && ReservedRequestStatuses.Contains(r.Status)) &&
                !db.EquipmentOrderLines.Any(l =>
                    l.PositionId == p.ID &&
                    ReservedOrderLineStatuses.Contains(l.Status)))
            .Select(p => p.StockCondition)
            .ToListAsync(cancellationToken);

        return (
            Total: availablePositions.Count,
            New: availablePositions.Count(x => x == StockCondition.New),
            Used: availablePositions.Count(x => x == StockCondition.Used));
    }

    private sealed class StockRequirement
    {
        public StockRequirement(int categoryId, string exampleItemName)
        {
            CategoryId = categoryId;
            ExampleItemName = exampleItemName;
        }

        public int CategoryId { get; }
        public string ExampleItemName { get; }
        public int TotalQuantity { get; set; }
        public int NewOnlyQuantity { get; set; }
    }

    private static EffectiveFulfillmentMode DetermineEffectiveFulfillmentMode(
        RequestCatalogItem item,
        AppStockPolicy siteStockPolicy)
    {
        return item.FulfillmentMode switch
        {
            CatalogFulfillmentMode.ServiceChange => EffectiveFulfillmentMode.ServiceChange,
            CatalogFulfillmentMode.ReturnAction => EffectiveFulfillmentMode.ReturnAction,
            CatalogFulfillmentMode.ExternalOrderOnly => EffectiveFulfillmentMode.ExternalOrder,
            CatalogFulfillmentMode.ConsumableNoStock => EffectiveFulfillmentMode.ManualNoStorage,

            CatalogFulfillmentMode.StockManaged or CatalogFulfillmentMode.StockOrExternalOrder =>
                siteStockPolicy == AppStockPolicy.NeverUseStorage
                    ? EffectiveFulfillmentMode.ManualNoStorage
                    : EffectiveFulfillmentMode.UseStorage,

            _ => EffectiveFulfillmentMode.ManualNoStorage
        };
    }

    private static bool ShouldUseIndividualLines(
        RequestCatalogItem item,
        EffectiveFulfillmentMode effectiveMode)
    {
        return item.Returnable || effectiveMode == EffectiveFulfillmentMode.UseStorage;
    }

    private async Task SendSupervisorApprovalEmailAsync(int orderId, CancellationToken cancellationToken)
    {
        var order = await LoadOrderForEmailAsync(orderId, cancellationToken);
        if (order is null)
            return;

        var baseUrl = GetBaseUrl();
        var approveUrl = $"{baseUrl}/api/orders/{order.Id}/approve/{order.ApproveToken}";
        var denyUrl = $"{baseUrl}/api/orders/{order.Id}/deny/{order.DenyToken}";
        var subject = $"[{order.DisplayTicketNumber}] Freigabe benötigt – Equipment-Bestellung";

        var content = $@"
<p style='margin:0 0 16px;color:#27313d;font-size:15px;line-height:1.55;'>
  <strong>{H(order.OrderedByName)}</strong> hat eine Equipment-Bestellung zur Freigabe eingereicht.
</p>
{BuildOrderHeaderTable(order)}
{BuildLinesTable(order)}
{BuildCostSummary(order)}
<div style='margin-top:24px;padding-top:18px;border-top:1px solid #e5e7eb;'>
  <p style='margin:0 0 14px;color:#27313d;font-weight:bold;'>Bitte wähle eine Option:</p>
  {BuildActionButton(approveUrl, "Genehmigen", "#2cb34a")}
  {BuildActionButton(denyUrl, "Ablehnen", "#EA0016")}
  <p style='margin:16px 0 0;color:#8a8f98;font-size:12px;'>Die Links sind einmalig verwendbar.</p>
</div>";

        var body = BuildEmailShell(
            title: "Equipment-Bestellung freigeben",
            subtitle: order.DisplayTicketNumber,
            accentColor: "#005691",
            content: content);

        await _email.SendAsync(order.SupervisorEmail, subject, body);
    }

    private async Task SendItNotificationAsync(int orderId, CancellationToken cancellationToken)
    {
        var order = await LoadOrderForEmailAsync(orderId, cancellationToken);
        if (order is null)
            return;

        var itEmail = order.Site?.ItEmail;

        if (string.IsNullOrWhiteSpace(itEmail))
            itEmail = _configuration["Email:ItEmail"] ?? "";
        if (string.IsNullOrWhiteSpace(itEmail))
        {
            _log.LogWarning("Email:ItEmail is missing. Cannot send IT notification for order {OrderId}", orderId);
            return;
        }

        var baseUrl = GetBaseUrl();
        var subject = $"[GENEHMIGT] [{order.DisplayTicketNumber}] Bestellung vorbereiten";

        var content = $@"
<p style='margin:0 0 16px;color:#27313d;font-size:15px;line-height:1.55;'>
  Die Bestellung wurde genehmigt und kann nun durch IT bearbeitet werden.
</p>
{BuildOrderHeaderTable(order)}
{BuildLinesTable(order, includeInternalColumns: true)}
{BuildInfoNote("Lagerpositionen werden nicht automatisch reserviert. Bitte Lagerpositionen pro Bestellposition im Admin-Panel zuweisen oder lagerlose Positionen manuell abschließen.")}
<p style='margin:18px 0 0;'>
  {BuildActionButton($"{baseUrl}/admin", "Im Admin-Panel öffnen", "#005691")}
</p>";

        var body = BuildEmailShell(
            title: "Bestellung vorbereiten",
            subtitle: order.DisplayTicketNumber,
            accentColor: "#005691",
            content: content);

        await _email.SendAsync(itEmail, subject, body);
    }

    private async Task SendDenialEmailAsync(int orderId, CancellationToken cancellationToken)
    {
        var order = await LoadOrderForEmailAsync(orderId, cancellationToken);
        if (order is null)
            return;

        var recipient = string.IsNullOrWhiteSpace(order.PickupContactEmail)
            ? order.OrderedByEmail
            : order.PickupContactEmail;

        var siteName = order.Site?.Name?.Trim() ?? "Unbekannter Standort";
        var subject = $"[GENEHMIGT] [{siteName}] [{order.DisplayTicketNumber}] Bestellung vorbereiten";
        var comment = string.IsNullOrWhiteSpace(order.BossComment) || order.BossComment == " "
            ? ""
            : BuildInfoNote($"Kommentar: {H(order.BossComment)}", "#fff7ed", "#fed7aa", "#9a3412");

        var content = $@"
<p style='margin:0 0 16px;color:#27313d;font-size:15px;line-height:1.55;'>
  Hallo {H(order.PickupContactName)}, die Bestellung <strong>{H(order.DisplayTicketNumber)}</strong> wurde abgelehnt.
</p>
{BuildOrderHeaderTable(order)}
{BuildLinesTable(order)}
{comment}";

        var body = BuildEmailShell(
            title: "Bestellung abgelehnt",
            subtitle: order.DisplayTicketNumber,
            accentColor: "#EA0016",
            content: content);

        await _email.SendAsync(recipient, subject, body);
    }

    private async Task<EquipmentOrder?> LoadOrderForEmailAsync(int orderId, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        return await db.EquipmentOrders
            .AsNoTracking()
            .Include(x => x.Site)
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == orderId, cancellationToken);
    }

    private static string BuildEmailShell(string title, string subtitle, string accentColor, string content)
    {
        return $@"
<div style='font-family:Arial,Helvetica,sans-serif;max-width:780px;margin:0 auto;color:#1f2937;background:#ffffff;'>
  <div style='border-radius:18px;overflow:hidden;border:1px solid #d9e2ec;box-shadow:0 14px 34px rgba(0,41,77,0.10);'>
    <div style='background:linear-gradient(135deg,{accentColor},#008ecf);color:white;padding:22px 26px;'>
      <div style='font-size:12px;font-weight:bold;letter-spacing:.08em;text-transform:uppercase;opacity:.86;'>Lagerverwaltung</div>
      <h2 style='margin:6px 0 0;font-size:24px;line-height:1.22;'>{H(title)}</h2>
      <div style='margin-top:8px;font-size:14px;opacity:.92;'>{H(subtitle)}</div>
    </div>
    <div style='padding:24px 26px;background:#ffffff;'>
      {content}
    </div>
    <div style='padding:14px 26px;background:#f7f9fb;border-top:1px solid #e5e7eb;color:#8a8f98;font-size:12px;'>
      Diese Nachricht wurde automatisch durch die Lagerverwaltung erstellt.
    </div>
  </div>
</div>";
    }

    private static string BuildActionButton(string url, string label, string backgroundColor)
    {
        return $"<a href='{H(url)}' style='display:inline-block;background:{backgroundColor};color:white;padding:12px 22px;border-radius:999px;text-decoration:none;font-weight:bold;margin:0 8px 8px 0;box-shadow:0 10px 18px rgba(0,0,0,0.12);'>{H(label)}</a>";
    }

    private static string BuildInfoNote(string text, string background = "#f0f7ff", string border = "#c7ddf2", string color = "#005691")
    {
        return $"<div style='margin:16px 0;padding:12px 14px;border-radius:12px;border:1px solid {border};background:{background};color:{color};font-size:13px;line-height:1.45;'>{text}</div>";
    }

    private static string BuildOrderHeaderTable(EquipmentOrder order)
    {
        return $@"
<table role='presentation' style='width:100%;border-collapse:separate;border-spacing:0;margin:18px 0;border:1px solid #e5e7eb;border-radius:12px;overflow:hidden;'>
  {BuildHeaderRow("Ticket", H(order.DisplayTicketNumber))}
  {BuildHeaderRow("Bestellt von", $"{H(order.OrderedByName)} ({H(order.OrderedByEmail)})")}
  {BuildHeaderRow("Für", $"{H(order.RequestedForName)} ({H(order.RequestedForEmail)})")}
  {BuildHeaderRow("Kontakt", $"{H(order.PickupContactName)} ({H(order.PickupContactEmail)})")}
  {BuildHeaderRow("Vorgesetzte/r", $"{H(order.SupervisorName)} ({H(order.SupervisorEmail)})")}
  {BuildHeaderRow("Begründung", H(order.Reason))}
  {BuildHeaderRow("Datum", order.CreatedAt.ToString("dd.MM.yyyy HH:mm"))}
</table>";
    }

    private static string BuildHeaderRow(string label, string value)
    {
        return $"<tr><td style='width:165px;padding:10px 12px;background:#f5f8fb;border-bottom:1px solid #e5e7eb;font-weight:bold;color:#344054;'>{H(label)}</td><td style='padding:10px 12px;border-bottom:1px solid #e5e7eb;color:#27313d;'>{value}</td></tr>";
    }

    private static string BuildLinesTable(EquipmentOrder order, bool includeInternalColumns = false)
    {
        var internalHeader = includeInternalColumns
            ? "<th style='padding:10px 12px;background:#f5f8fb;text-align:left;color:#344054;'>Bearbeitung</th>"
            : "";

        var rows = string.Join(Environment.NewLine, order.Lines
            .OrderBy(x => x.Id)
            .Select(line =>
            {
                var internalCell = includeInternalColumns
                    ? $"<td style='padding:10px 12px;border-bottom:1px solid #eef2f6;color:#27313d;'>{H(GetEffectiveFulfillmentLabel(line.EffectiveFulfillmentMode))}</td>"
                    : "";

                var note = string.IsNullOrWhiteSpace(line.UserComment) || line.UserComment == " "
                    ? "–"
                    : H(line.UserComment);

                return $@"
<tr>
  <td style='padding:10px 12px;border-bottom:1px solid #eef2f6;color:#005691;font-weight:bold;'>{H(line.CategoryName)}</td>
  <td style='padding:10px 12px;border-bottom:1px solid #eef2f6;color:#111827;font-weight:bold;'>{H(line.ItemName)}</td>
  <td style='padding:10px 12px;border-bottom:1px solid #eef2f6;text-align:center;color:#27313d;'>{line.Quantity}</td>
  <td style='padding:10px 12px;border-bottom:1px solid #eef2f6;text-align:right;color:#27313d;white-space:nowrap;'>{H(line.Currency)} {line.LinePrice:0.00}</td>
  <td style='padding:10px 12px;border-bottom:1px solid #eef2f6;color:#27313d;'>{H(line.BillingType)}</td>
  {internalCell}
  <td style='padding:10px 12px;border-bottom:1px solid #eef2f6;color:#667085;'>{note}</td>
</tr>";
            }));

        return $@"
<table role='presentation' style='width:100%;border-collapse:separate;border-spacing:0;margin:18px 0;border:1px solid #e5e7eb;border-radius:12px;overflow:hidden;'>
  <thead>
    <tr>
      <th style='padding:10px 12px;background:#f5f8fb;text-align:left;color:#344054;'>Kategorie</th>
      <th style='padding:10px 12px;background:#f5f8fb;text-align:left;color:#344054;'>Artikel</th>
      <th style='padding:10px 12px;background:#f5f8fb;text-align:center;color:#344054;'>Menge</th>
      <th style='padding:10px 12px;background:#f5f8fb;text-align:right;color:#344054;'>Preis</th>
      <th style='padding:10px 12px;background:#f5f8fb;text-align:left;color:#344054;'>Abrechnung</th>
      {internalHeader}
      <th style='padding:10px 12px;background:#f5f8fb;text-align:left;color:#344054;'>Notiz</th>
    </tr>
  </thead>
  <tbody>{rows}</tbody>
</table>";
    }

    private static string BuildCostSummary(EquipmentOrder order)
    {
        var oneTime = order.Lines
            .Where(x => IsOneTime(x.BillingType))
            .Sum(x => x.LinePrice);

        var monthly = order.Lines
            .Where(x => IsMonthly(x.BillingType))
            .Sum(x => x.LinePrice);

        var noCostLines = order.Lines.Count(x => IsNoCost(x.BillingType));
        var currency = order.Lines.FirstOrDefault()?.Currency ?? "CHF";

        return $@"
<table role='presentation' style='width:100%;border-collapse:separate;border-spacing:10px;margin:14px 0 2px;background:#f7f9fb;border:1px solid #e5e7eb;border-radius:14px;'>
  <tr>
    <td style='padding:10px 12px;background:white;border-radius:12px;'><div style='color:#667085;font-size:12px;font-weight:bold;text-transform:uppercase;'>Einmalig</div><div style='font-size:18px;font-weight:bold;color:#111827;'>{H(currency)} {oneTime:0.00}</div></td>
    <td style='padding:10px 12px;background:white;border-radius:12px;'><div style='color:#667085;font-size:12px;font-weight:bold;text-transform:uppercase;'>Monatlich</div><div style='font-size:18px;font-weight:bold;color:#111827;'>{H(currency)} {monthly:0.00}</div></td>
    <td style='padding:10px 12px;background:white;border-radius:12px;'><div style='color:#667085;font-size:12px;font-weight:bold;text-transform:uppercase;'>Ohne Kosten</div><div style='font-size:18px;font-weight:bold;color:#111827;'>{noCostLines} Position(en)</div></td>
  </tr>
</table>";
    }


    private static bool IsMonthly(string? billingType)
        => (billingType ?? "").Contains("monat", StringComparison.OrdinalIgnoreCase);

    private static bool IsOneTime(string? billingType)
        => (billingType ?? "").Contains("einmal", StringComparison.OrdinalIgnoreCase);

    private static bool IsNoCost(string? billingType)
    {
        var normalized = (billingType ?? "").Trim();
        return string.IsNullOrWhiteSpace(normalized) ||
               normalized.Equals("ohne", StringComparison.OrdinalIgnoreCase) ||
               (!IsMonthly(normalized) && !IsOneTime(normalized));
    }

    private static string GetEffectiveFulfillmentLabel(EffectiveFulfillmentMode mode)
    {
        return mode switch
        {
            EffectiveFulfillmentMode.UseStorage => "Lagerposition zuweisen",
            EffectiveFulfillmentMode.ManualNoStorage => "Manuell abschließen",
            EffectiveFulfillmentMode.ExternalOrder => "Externe Bestellung",
            EffectiveFulfillmentMode.ServiceChange => "Service / Mutation",
            EffectiveFulfillmentMode.ReturnAction => "Rückgabe / Kündigung",
            _ => mode.ToString()
        };
    }

    private string GetBaseUrl()
    {
        var configured = (_configuration["App:BaseUrl"] ?? "").Trim();
        var environmentName =
            _configuration["ASPNETCORE_ENVIRONMENT"]
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? "Production";

        var isDevelopment = string.Equals(
            environmentName,
            "Development",
            StringComparison.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(configured))
        {
            configured = configured.TrimEnd('/');

            // Localhost links in production emails are never useful.
            // Keep localhost only for local Development.
            if (isDevelopment || !IsLocalhostBaseUrl(configured))
                return configured;
        }

        return isDevelopment
            ? "https://localhost:5001"
            : "https://rb-sn-lagerverwaltung.intranet.bosch.com";
    }

    private static bool IsLocalhostBaseUrl(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
            return false;

        return string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase)
            || string.Equals(uri.Host, "127.0.0.1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(uri.Host, "::1", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeEmail(string? email)
    {
        return email?.Trim().ToLowerInvariant() ?? "";
    }

    private static string H(string? value)
    {
        return WebUtility.HtmlEncode(value?.Trim() ?? "");
    }

    private static string GetAlreadyHandledMessage(OrderStatus status, bool approveAttempt)
    {
        return status switch
        {
            OrderStatus.Approved =>
                "Diese Bestellung wurde bereits genehmigt. Es ist keine weitere Aktion notwendig.",

            OrderStatus.Denied when approveAttempt =>
                "Diese Bestellung wurde bereits abgelehnt und kann über diesen Link nicht mehr genehmigt werden.",

            OrderStatus.Denied =>
                "Diese Bestellung wurde bereits abgelehnt. Es ist keine weitere Aktion notwendig.",

            OrderStatus.PartiallyFulfilled =>
                "Diese Bestellung wurde bereits genehmigt und wird bereits durch IT bearbeitet.",

            OrderStatus.Completed =>
                "Diese Bestellung wurde bereits genehmigt und abgeschlossen.",

            OrderStatus.Cancelled =>
                "Diese Bestellung wurde bereits storniert.",

            _ =>
                "Diese Bestellung wurde bereits bearbeitet. Es ist keine weitere Aktion notwendig."
        };
    }
}
