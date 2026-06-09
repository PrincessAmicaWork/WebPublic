using Lagerverwaltung.Web.Data;
using Lagerverwaltung.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace Lagerverwaltung.Web.Services;

public record ReturnRequestRow(
    int ReturnRequestId,
    int? EquipmentRequestId,
    int? EquipmentOrderLineId,
    int PositionId,
    string TicketNumber,
    string RequesterName,
    string RequesterEmail,
    string CategoryName,
    string Description,
    string OrderNumber,
    StockCondition StockCondition,
    ReturnRequestStatus Status,
    DateTime RequestedAt,
    DateTime? ConfirmedAt,
    string UserComment,
    string AdminComment,
    string SourceLabel);

public interface IReturnService
{
    Task<List<ReturnRequestRow>> GetReturnRequestsAsync(ReturnRequestStatus? status = ReturnRequestStatus.Pending);
    Task<Dictionary<int, ReturnRequestStatus>> GetReturnStatusesForRequestsAsync(IEnumerable<int> equipmentRequestIds);
    Task<Dictionary<int, ReturnRequestStatus>> GetReturnStatusesForOrderLinesAsync(IEnumerable<int> equipmentOrderLineIds);
    Task<(bool ok, string error)> RequestReturnAsync(int equipmentRequestId, string userEmail, string comment);
    Task<(bool ok, string error)> RequestOrderLineReturnAsync(int equipmentOrderLineId, string userEmail, string comment);
    Task<(bool ok, string error)> ConfirmReturnAsync(int returnRequestId, string adminComment);
    Task<(bool ok, string error)> CancelReturnAsync(int returnRequestId, string adminComment);
}

public class ReturnService : IReturnService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly IEmailService _email;
    private readonly IConfiguration _cfg;
    private readonly ILogger<ReturnService> _log;

    public ReturnService(
        IDbContextFactory<AppDbContext> dbFactory,
        IEmailService email,
        IConfiguration cfg,
        ILogger<ReturnService> log)
    {
        _dbFactory = dbFactory;
        _email = email;
        _cfg = cfg;
        _log = log;
    }

    public async Task<List<ReturnRequestRow>> GetReturnRequestsAsync(ReturnRequestStatus? status = ReturnRequestStatus.Pending)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var query = BaseReturnQuery(db).AsQueryable();

        if (status.HasValue)
            query = query.Where(r => r.Status == status.Value);

        var rows = await query
            .OrderByDescending(r => r.RequestedAt)
            .ToListAsync();

        return rows.Select(ToRow).ToList();
    }

    public async Task<Dictionary<int, ReturnRequestStatus>> GetReturnStatusesForRequestsAsync(IEnumerable<int> equipmentRequestIds)
    {
        var ids = equipmentRequestIds
            .Where(id => id > 0)
            .Distinct()
            .ToList();

        if (ids.Count == 0)
            return new Dictionary<int, ReturnRequestStatus>();

        await using var db = await _dbFactory.CreateDbContextAsync();

        var rows = await db.ReturnRequests
            .AsNoTracking()
            .Where(r => r.EquipmentRequestId.HasValue && ids.Contains(r.EquipmentRequestId.Value))
            .OrderByDescending(r => r.RequestedAt)
            .Select(r => new { EquipmentRequestId = r.EquipmentRequestId!.Value, r.Status })
            .ToListAsync();

        return rows
            .GroupBy(x => x.EquipmentRequestId)
            .ToDictionary(g => g.Key, g => g.First().Status);
    }

    public async Task<Dictionary<int, ReturnRequestStatus>> GetReturnStatusesForOrderLinesAsync(IEnumerable<int> equipmentOrderLineIds)
    {
        var ids = equipmentOrderLineIds
            .Where(id => id > 0)
            .Distinct()
            .ToList();

        if (ids.Count == 0)
            return new Dictionary<int, ReturnRequestStatus>();

        await using var db = await _dbFactory.CreateDbContextAsync();

        var rows = await db.ReturnRequests
            .AsNoTracking()
            .Where(r => r.EquipmentOrderLineId.HasValue && ids.Contains(r.EquipmentOrderLineId.Value))
            .OrderByDescending(r => r.RequestedAt)
            .Select(r => new { EquipmentOrderLineId = r.EquipmentOrderLineId!.Value, r.Status })
            .ToListAsync();

        return rows
            .GroupBy(x => x.EquipmentOrderLineId)
            .ToDictionary(g => g.Key, g => g.First().Status);
    }

    public async Task<(bool ok, string error)> RequestReturnAsync(int equipmentRequestId, string userEmail, string comment)
    {
        if (equipmentRequestId <= 0)
            return (false, "Ungültige Anfrage-ID.");

        var normalizedUserEmail = NormalizeEmail(userEmail);
        if (string.IsNullOrWhiteSpace(normalizedUserEmail))
            return (false, "Benutzer-E-Mail konnte nicht erkannt werden.");

        await using var db = await _dbFactory.CreateDbContextAsync();

        var request = await db.EquipmentRequests
            .Include(r => r.Position).ThenInclude(p => p!.Category)
            .FirstOrDefaultAsync(r => r.Id == equipmentRequestId);

        if (request is null)
            return (false, "Anfrage wurde nicht gefunden.");

        if (!string.Equals(NormalizeEmail(request.RequesterEmail), normalizedUserEmail, StringComparison.OrdinalIgnoreCase))
            return (false, "Du darfst nur deine eigenen Rückgaben anmelden.");

        if (request.Status != RequestStatus.Collected)
            return (false, "Nur abgeholte Artikel können zurückgegeben werden.");

        var existing = await db.ReturnRequests
            .Where(r => r.EquipmentRequestId == equipmentRequestId)
            .OrderByDescending(r => r.RequestedAt)
            .FirstOrDefaultAsync();

        if (existing is not null)
            return ExistingReturnError(existing.Status, "diese Anfrage");

        var returnRequest = new ReturnRequest
        {
            EquipmentRequestId = request.Id,
            EquipmentOrderLineId = null,
            PositionId = request.PositionId,
            RequesterName = request.RequesterName,
            RequesterEmail = request.RequesterEmail,
            Status = ReturnRequestStatus.Pending,
            RequestedAt = DateTime.Now,
            UserComment = NormalizeOptional(comment),
            AdminComment = " "
        };

        db.ReturnRequests.Add(returnRequest);
        await db.SaveChangesAsync();

        _ = SendReturnRequestedEmailAsync(returnRequest.Id);

        return (true, "");
    }

    public async Task<(bool ok, string error)> RequestOrderLineReturnAsync(int equipmentOrderLineId, string userEmail, string comment)
    {
        if (equipmentOrderLineId <= 0)
            return (false, "Ungültige Bestellpositions-ID.");

        var normalizedUserEmail = NormalizeEmail(userEmail);
        if (string.IsNullOrWhiteSpace(normalizedUserEmail))
            return (false, "Benutzer-E-Mail konnte nicht erkannt werden.");

        await using var db = await _dbFactory.CreateDbContextAsync();

        var line = await db.EquipmentOrderLines
            .Include(l => l.EquipmentOrder)
            .Include(l => l.Position).ThenInclude(p => p!.Category)
            .FirstOrDefaultAsync(l => l.Id == equipmentOrderLineId);

        if (line is null)
            return (false, "Bestellposition wurde nicht gefunden.");

        if (line.EquipmentOrder is null)
            return (false, "Zugehörige Bestellung wurde nicht gefunden.");

        var order = line.EquipmentOrder;
        var allowedEmails = new[]
        {
            order.OrderedByEmail,
            order.RequestedForEmail,
            order.PickupContactEmail
        };

        if (!allowedEmails.Any(email => string.Equals(NormalizeEmail(email), normalizedUserEmail, StringComparison.OrdinalIgnoreCase)))
            return (false, "Du darfst nur Rückgaben für eigene oder von dir bestellte Artikel anmelden.");

        if (line.Status == OrderLineStatus.Returned)
            return (false, "Diese Bestellposition wurde bereits zurückgegeben.");

        if (line.Status != OrderLineStatus.Fulfilled)
            return (false, "Nur ausgegebene Artikel können zurückgegeben werden.");

        if (line.EffectiveFulfillmentMode != EffectiveFulfillmentMode.UseStorage || !line.PositionId.HasValue)
            return (false, "Diese Bestellposition ist keine rückgabefähige Lagerposition.");

        var positionId = line.PositionId.Value;
        var ticketNumber = order.DisplayTicketNumber;

        var openIssueExists = await db.Issues.AnyAsync(i =>
            i.PositionId == positionId &&
            i.TicketNumber == ticketNumber &&
            i.TakeBackDate == null);

        if (!openIssueExists)
            return (false, "Für diese Bestellposition wurde kein offener Issue gefunden. Bitte IT kontaktieren.");

        var existing = await db.ReturnRequests
            .Where(r => r.EquipmentOrderLineId == equipmentOrderLineId)
            .OrderByDescending(r => r.RequestedAt)
            .FirstOrDefaultAsync();

        if (existing is not null)
            return ExistingReturnError(existing.Status, "diese Bestellposition");

        var requesterName = string.IsNullOrWhiteSpace(order.RequestedForName)
            ? order.OrderedByName
            : order.RequestedForName;

        var requesterEmail = string.IsNullOrWhiteSpace(order.RequestedForEmail)
            ? order.OrderedByEmail
            : order.RequestedForEmail;

        var returnRequest = new ReturnRequest
        {
            EquipmentRequestId = null,
            EquipmentOrderLineId = line.Id,
            PositionId = positionId,
            RequesterName = requesterName,
            RequesterEmail = requesterEmail,
            Status = ReturnRequestStatus.Pending,
            RequestedAt = DateTime.Now,
            UserComment = NormalizeOptional(comment),
            AdminComment = " "
        };

        db.ReturnRequests.Add(returnRequest);
        await db.SaveChangesAsync();

        _ = SendReturnRequestedEmailAsync(returnRequest.Id);

        return (true, "");
    }

    public async Task<(bool ok, string error)> ConfirmReturnAsync(int returnRequestId, string adminComment)
    {
        if (returnRequestId <= 0)
            return (false, "Ungültige Rückgabe-ID.");

        await using var db = await _dbFactory.CreateDbContextAsync();

        var returnRequest = await BaseReturnQuery(db)
            .FirstOrDefaultAsync(r => r.Id == returnRequestId);

        if (returnRequest is null)
            return (false, "Rückgabe wurde nicht gefunden.");

        if (returnRequest.Status != ReturnRequestStatus.Pending)
            return (false, "Diese Rückgabe wurde bereits bearbeitet.");

        if (returnRequest.Position is null)
            return (false, "Der zugehörige Artikel wurde nicht gefunden.");

        var openIssueQuery = db.Issues
            .Where(i => i.PositionId == returnRequest.PositionId && i.TakeBackDate == null);

        if (returnRequest.EquipmentOrderLine?.EquipmentOrder is not null)
        {
            var ticket = returnRequest.EquipmentOrderLine.EquipmentOrder.DisplayTicketNumber;
            openIssueQuery = openIssueQuery.Where(i => i.TicketNumber == ticket);
        }

        var openIssue = await openIssueQuery
            .OrderByDescending(i => i.IssueDate)
            .FirstOrDefaultAsync();

        if (openIssue is null)
            return (false, "Für diese Rückgabe wurde kein offener Issue gefunden.");

        openIssue.TakeBackDate = DateTime.Now;
        returnRequest.Position.StockCondition = StockCondition.Used;
        returnRequest.Status = ReturnRequestStatus.ConfirmedReturned;
        returnRequest.ConfirmedAt = DateTime.Now;
        returnRequest.AdminComment = NormalizeOptional(adminComment);

        if (returnRequest.EquipmentOrderLine is not null)
        {
            returnRequest.EquipmentOrderLine.Status = OrderLineStatus.Returned;
            returnRequest.EquipmentOrderLine.AdminComment = "Rückgabe bestätigt.";

            var order = await db.EquipmentOrders
                .Include(o => o.Lines)
                .FirstOrDefaultAsync(o => o.Id == returnRequest.EquipmentOrderLine.EquipmentOrderId);

            if (order is not null)
                RecalculateOrderStatus(order);
        }

        await db.SaveChangesAsync();

        _ = SendReturnConfirmedEmailAsync(returnRequest.Id);

        return (true, "");
    }

    public async Task<(bool ok, string error)> CancelReturnAsync(int returnRequestId, string adminComment)
    {
        if (returnRequestId <= 0)
            return (false, "Ungültige Rückgabe-ID.");

        await using var db = await _dbFactory.CreateDbContextAsync();

        var returnRequest = await db.ReturnRequests
            .FirstOrDefaultAsync(r => r.Id == returnRequestId);

        if (returnRequest is null)
            return (false, "Rückgabe wurde nicht gefunden.");

        if (returnRequest.Status != ReturnRequestStatus.Pending)
            return (false, "Diese Rückgabe wurde bereits bearbeitet.");

        returnRequest.Status = ReturnRequestStatus.Cancelled;
        returnRequest.AdminComment = NormalizeOptional(adminComment);
        returnRequest.ConfirmedAt = DateTime.Now;

        await db.SaveChangesAsync();

        return (true, "");
    }

    private IQueryable<ReturnRequest> BaseReturnQuery(AppDbContext db)
    {
        return db.ReturnRequests
            .Include(r => r.EquipmentRequest)
            .Include(r => r.EquipmentOrderLine)!
                .ThenInclude(l => l!.EquipmentOrder)
            .Include(r => r.Position)!
                .ThenInclude(p => p!.Category);
    }

    private async Task SendReturnRequestedEmailAsync(int returnRequestId)
    {
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var row = await BaseReturnQuery(db)
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == returnRequestId);

            if (row is null)
                return;

            var itEmail = _cfg["Email:ItEmail"] ?? "";
            if (string.IsNullOrWhiteSpace(itEmail))
            {
                _log.LogWarning("Email:ItEmail is missing. Return notification not sent for return request {Id}", returnRequestId);
                return;
            }

            var baseUrl = _cfg["App:BaseUrl"] ?? "";
            var view = ToRow(row);
            var subject = $"[Rückgabe] [{view.TicketNumber}] {view.RequesterName} möchte Equipment zurückgeben";
            var adminLink = string.IsNullOrWhiteSpace(baseUrl)
                ? ""
                : BuildReturnButton($"{baseUrl.TrimEnd('/')}/admin", "Im Admin-Panel öffnen", "#005691");

            var content = $@"
<p style='margin:0 0 16px;color:#27313d;font-size:15px;line-height:1.55;'>
  <strong>{H(view.RequesterName)}</strong> hat eine Rückgabe angemeldet. Bitte nach physischer Rückgabe im Admin-Bereich bestätigen.
</p>
{BuildReturnDetailsTable(view)}
{(string.IsNullOrWhiteSpace(adminLink) ? "" : $"<p style='margin:18px 0 0;'>{adminLink}</p>")}";

            var body = BuildReturnEmailShell(
                title: "Rückgabe angemeldet",
                subtitle: view.TicketNumber,
                accentColor: "#005691",
                content: content);

            await _email.SendAsync(itEmail, subject, body);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to send return-request email for return request {Id}", returnRequestId);
        }
    }

    private async Task SendReturnConfirmedEmailAsync(int returnRequestId)
    {
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var row = await BaseReturnQuery(db)
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == returnRequestId);

            if (row is null || string.IsNullOrWhiteSpace(row.RequesterEmail))
                return;

            var view = ToRow(row);
            var subject = $"[{view.TicketNumber}] Rückgabe bestätigt";

            var content = $@"
<p style='margin:0 0 16px;color:#27313d;font-size:15px;line-height:1.55;'>
  Hallo {H(view.RequesterName)}, die Rückgabe wurde bestätigt und die Lagerposition ist wieder im Rücknahmeprozess.
</p>
{BuildReturnDetailsTable(view)}
<p style='margin:18px 0 0;color:#667085;font-size:13px;'>Vielen Dank.</p>";

            var body = BuildReturnEmailShell(
                title: "Rückgabe bestätigt",
                subtitle: view.TicketNumber,
                accentColor: "#2cb34a",
                content: content);

            await _email.SendAsync(row.RequesterEmail, subject, body);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to send return-confirmed email for return request {Id}", returnRequestId);
        }
    }

    private static string BuildReturnEmailShell(string title, string subtitle, string accentColor, string content)
    {
        return $@"
<div style='font-family:Arial,Helvetica,sans-serif;max-width:720px;margin:0 auto;color:#1f2937;background:#ffffff;'>
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

    private static string BuildReturnDetailsTable(ReturnRequestRow view)
    {
        return $@"
<table role='presentation' style='width:100%;border-collapse:separate;border-spacing:0;margin:18px 0;border:1px solid #e5e7eb;border-radius:12px;overflow:hidden;'>
  {BuildReturnRow("Quelle", H(view.SourceLabel))}
  {BuildReturnRow("Ticket", H(view.TicketNumber))}
  {BuildReturnRow("Benutzer", $"{H(view.RequesterName)} ({H(view.RequesterEmail)})")}
  {BuildReturnRow("Kategorie", H(view.CategoryName))}
  {BuildReturnRow("Artikel", H(view.Description))}
  {BuildReturnRow("Lagerposition", $"#{view.PositionId}")}
  {BuildReturnRow("Interne Nr.", string.IsNullOrWhiteSpace(view.OrderNumber) ? "–" : H(view.OrderNumber))}
  {BuildReturnRow("Kommentar", string.IsNullOrWhiteSpace(view.UserComment) || view.UserComment == " " ? "–" : H(view.UserComment))}
</table>";
    }

    private static string BuildReturnRow(string label, string value)
    {
        return $"<tr><td style='width:150px;padding:10px 12px;background:#f5f8fb;border-bottom:1px solid #e5e7eb;font-weight:bold;color:#344054;'>{H(label)}</td><td style='padding:10px 12px;border-bottom:1px solid #e5e7eb;color:#27313d;'>{value}</td></tr>";
    }

    private static string BuildReturnButton(string url, string label, string backgroundColor)
    {
        return $"<a href='{H(url)}' style='display:inline-block;background:{backgroundColor};color:white;padding:12px 22px;border-radius:999px;text-decoration:none;font-weight:bold;margin:0 8px 8px 0;box-shadow:0 10px 18px rgba(0,0,0,0.12);'>{H(label)}</a>";
    }

    private static ReturnRequestRow ToRow(ReturnRequest request)
    {
        var orderLine = request.EquipmentOrderLine;
        var order = orderLine?.EquipmentOrder;
        var legacyRequest = request.EquipmentRequest;

        var ticketNumber = order?.DisplayTicketNumber
            ?? legacyRequest?.DisplayTicketNumber
            ?? (request.EquipmentOrderLineId.HasValue
                ? $"WEB-O-LINE-{request.EquipmentOrderLineId.Value}"
                : $"WEB-{request.EquipmentRequestId ?? 0}");

        var categoryName = orderLine?.CategoryName?.Trim();
        if (string.IsNullOrWhiteSpace(categoryName))
            categoryName = request.Position?.Category?.Name?.Trim() ?? "";

        var description = orderLine?.DisplayName?.Trim();
        if (string.IsNullOrWhiteSpace(description))
            description = request.Position?.Description?.Trim() ?? "";

        return new ReturnRequestRow(
            ReturnRequestId: request.Id,
            EquipmentRequestId: request.EquipmentRequestId,
            EquipmentOrderLineId: request.EquipmentOrderLineId,
            PositionId: request.PositionId,
            TicketNumber: ticketNumber,
            RequesterName: request.RequesterName?.Trim() ?? "",
            RequesterEmail: request.RequesterEmail?.Trim() ?? "",
            CategoryName: categoryName,
            Description: description,
            OrderNumber: request.Position?.OrderNumber?.Trim() ?? "",
            StockCondition: request.Position?.StockCondition ?? StockCondition.New,
            Status: request.Status,
            RequestedAt: request.RequestedAt,
            ConfirmedAt: request.ConfirmedAt,
            UserComment: request.UserComment?.Trim() ?? "",
            AdminComment: request.AdminComment?.Trim() ?? "",
            SourceLabel: request.EquipmentOrderLineId.HasValue ? "Neue Bestellung" : "Legacy-Einzelanfrage");
    }

    private static (bool ok, string error) ExistingReturnError(ReturnRequestStatus status, string targetLabel)
    {
        return status switch
        {
            ReturnRequestStatus.Pending => (false, $"Für {targetLabel} ist bereits eine Rückgabe angemeldet."),
            ReturnRequestStatus.ConfirmedReturned => (false, $"{Capitalize(targetLabel)} wurde bereits zurückgegeben."),
            _ => (false, $"Für {targetLabel} existiert bereits ein Rückgabe-Eintrag.")
        };
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
                or OrderLineStatus.Fulfilled
                or OrderLineStatus.Returned))
        {
            order.Status = OrderStatus.PartiallyFulfilled;
        }
    }

    private static string NormalizeEmail(string? email)
    {
        return email?.Trim().ToLowerInvariant() ?? "";
    }

    private static string NormalizeOptional(string? value)
    {
        var normalized = value?.Trim() ?? "";
        return string.IsNullOrWhiteSpace(normalized) ? " " : normalized;
    }

    private static string H(string? value)
    {
        return System.Net.WebUtility.HtmlEncode(value?.Trim() ?? "");
    }

    private static string Capitalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        value = value.Trim();
        return char.ToUpperInvariant(value[0]) + value[1..];
    }
}
