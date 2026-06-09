using Lagerverwaltung.Web.Data;
using Lagerverwaltung.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace Lagerverwaltung.Web.Services
{
    public record CatalogItem(
        string GroupKey,
        string CategoryName,
        string Description,
        string OrderNumber,
        double Price,
        PositionAvailability Availability,
        int AvailableCount,
        int NewAvailableCount,
        int UsedAvailableCount,
        int TotalCount
    );

    public enum PositionAvailability { Available, Reserved, Unavailable }

    public interface IEquipmentService
    {
        Task<List<Category>> GetCategoriesAsync();
        Task<List<CatalogItem>> GetCatalogAsync(int? categoryId, string? search);
        Task<(bool ok, string error)> SubmitRequestAsync(
            string groupKey,
            string name,
            string email,
            string supervisorName,
            string supervisorEmail,
            string reason,
            bool usedItemOk);

        Task<List<EquipmentRequest>> GetRequestsAsync(RequestStatus? status = null);
        Task<List<EquipmentRequest>> GetRequestsByEmailAsync(string email);
        Task<List<EquipmentRequest>> GetRequestsForSupervisorAsync(string email);
        Task<bool> HasSupervisorRequestsAsync(string supervisorEmail);
        Task<EquipmentRequest?> GetRequestByTokenAsync(string token, bool isApprove);

        Task<(bool ok, string error)> ApproveAsync(
            int requestId,
            string actorEmail,
            bool actorIsAdmin,
            string bossComment = "");

        Task<(bool ok, string error)> DenyAsync(
            int requestId,
            string actorEmail,
            bool actorIsAdmin,
            string bossComment);


        Task<(bool ok, string error)> ApproveByTokenAsync(int requestId, string token);
        Task<(bool ok, string error)> DenyByTokenAsync(int requestId, string token, string bossComment);


        Task<(bool ok, string error)> MarkPreparingAsync(int requestId, bool actorIsAdmin);
        Task<(bool ok, string error)> MarkCollectedAsync(int requestId, bool actorIsAdmin);
        Task<(bool ok, string error)> SendPickupEmailAndCollectAsync(int requestId, bool actorIsAdmin);
        Task<(bool ok, string error)> CompleteHandoverAsync(
            int requestId,
            bool actorIsAdmin,
            RequestFulfillmentType fulfillmentType);
    }

    public class EquipmentService : IEquipmentService
    {
        private readonly IDbContextFactory<AppDbContext> _dbFactory;
        private readonly IEmailService _email;
        private readonly IConfiguration _cfg;
        private readonly ILogger<EquipmentService> _log;
        private readonly IApproverService _approvers;
        private readonly ILowStockNotificationService _lowStock;

        public EquipmentService(
            IDbContextFactory<AppDbContext> dbFactory,
            IEmailService email,
            IConfiguration cfg,
            ILogger<EquipmentService> log,
            IApproverService approvers,
            ILowStockNotificationService lowStock)
        {
            _dbFactory = dbFactory;
            _email = email;
            _cfg = cfg;
            _log = log;
            _approvers = approvers;
            _lowStock = lowStock;
        }

        public async Task<List<Category>> GetCategoriesAsync()
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            return await db.Categories
                .AsNoTracking()
                .Where(c => c.Name != null && c.Name.Trim() != "")
                .OrderBy(c => c.Name)
                .ToListAsync();
        }

        public async Task<List<CatalogItem>> GetCatalogAsync(int? categoryId, string? search)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var posQuery = db.Positions
                .AsNoTracking()
                .Include(p => p.Category)
                .Include(p => p.Issued)
                .AsQueryable();

            if (categoryId.HasValue)
                posQuery = posQuery.Where(p => p.CategoryId == categoryId.Value);

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim().ToLower();

                posQuery = posQuery.Where(p =>
                    p.Description.ToLower().Contains(search) ||
                    p.OrderNumber.ToLower().Contains(search) ||
                    p.Category.Name.ToLower().Contains(search));
            }

            var positions = await posQuery
                .OrderBy(p => p.Category.Name)
                .ThenBy(p => p.Description)
                .ThenBy(p => p.ID)
                .ToListAsync();

            var pendingPositionIds = await db.EquipmentRequests
                .Where(r =>
                    r.Status == RequestStatus.Pending ||
                    r.Status == RequestStatus.Approved ||
                    r.Status == RequestStatus.Preparing)
                .Select(r => r.PositionId)
                .ToListAsync();

            var pendingSet = pendingPositionIds.ToHashSet();

            string BuildGroupKey(Position p)
            {
                var category = p.Category?.Name?.Trim() ?? "";
                var description = p.Description?.Trim() ?? "";
                var orderNumber = p.OrderNumber?.Trim() ?? "";
                return $"{p.CategoryId}|{category}|{description}|{orderNumber}";
            }

            var grouped = positions
                .GroupBy(BuildGroupKey)
                .Select(group =>
                {
                    var first = group.First();

                    var availablePositions = group
                        .Where(p => p.Avaliable == "yes" && !pendingSet.Contains(p.ID))
                        .ToList();

                    var availableCount = availablePositions.Count;
                    var newAvailableCount = availablePositions.Count(p => p.StockCondition == StockCondition.New);
                    var usedAvailableCount = availablePositions.Count(p => p.StockCondition == StockCondition.Used);

                    var reservedCount = group.Count(p =>
                        p.Avaliable == "yes" &&
                        pendingSet.Contains(p.ID));

                    PositionAvailability availability;
                    if (availableCount > 0)
                        availability = PositionAvailability.Available;
                    else if (reservedCount > 0)
                        availability = PositionAvailability.Reserved;
                    else
                        availability = PositionAvailability.Unavailable;

                    return new CatalogItem(
                        BuildGroupKey(first),
                        first.Category?.Name?.Trim() ?? "Ohne Kategorie",
                        first.Description?.Trim() ?? "",
                        first.OrderNumber?.Trim() ?? "",
                        first.Price,
                        availability,
                        availableCount,
                        newAvailableCount,
                        usedAvailableCount,
                        group.Count());
                })
                .OrderBy(x => x.CategoryName)
                .ThenBy(x => x.Description)
                .ThenBy(x => x.OrderNumber)
                .ToList();

            return grouped;
        }

        public async Task<(bool ok, string error)> SubmitRequestAsync(
            string groupKey,
            string name,
            string email,
            string supervisorName,
            string supervisorEmail,
            string reason,
            bool usedItemOk)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            static string Normalize(string? value) => value?.Trim() ?? "";

            var positions = await db.Positions
                .Include(p => p.Category)
                .Include(p => p.Issued)
                .OrderBy(p => p.ID)
                .ToListAsync();

            string BuildGroupKey(Position p)
            {
                var category = Normalize(p.Category?.Name);
                var description = Normalize(p.Description);
                var orderNumber = Normalize(p.OrderNumber);
                return $"{p.CategoryId}|{category}|{description}|{orderNumber}";
            }

            var matchingPositions = positions
                .Where(p => BuildGroupKey(p) == groupKey)
                .OrderBy(p => p.ID)
                .ToList();

            if (!matchingPositions.Any())
                return (false, "Kein passender Artikel gefunden.");

            var pendingPositionIds = await db.EquipmentRequests
                .Where(r =>
                    r.Status == RequestStatus.Pending ||
                    r.Status == RequestStatus.Approved ||
                    r.Status == RequestStatus.Preparing)
                .Select(r => r.PositionId)
                .ToListAsync();

            var pendingSet = pendingPositionIds.ToHashSet();

            var availableCandidates = matchingPositions
                .Where(p => p.Avaliable == "yes" && !pendingSet.Contains(p.ID))
                .ToList();

            if (!usedItemOk)
            {
                availableCandidates = availableCandidates
                    .Where(p => p.StockCondition == StockCondition.New)
                    .ToList();
            }

            var selectedPosition = availableCandidates
                .OrderBy(p => usedItemOk && p.StockCondition == StockCondition.Used ? 0 : 1)
                .ThenBy(p => p.ID)
                .FirstOrDefault();

            if (selectedPosition is null)
            {
                var hasOnlyUsedAvailable = matchingPositions.Any(p =>
                    p.Avaliable == "yes" &&
                    !pendingSet.Contains(p.ID) &&
                    p.StockCondition == StockCondition.Used);

                if (!usedItemOk && hasOnlyUsedAvailable)
                    return (false, "Aktuell ist nur Gebrauchtware verfügbar. Bitte bestätige, dass ein Gebrauchtgerät okay ist.");

                return (false, "Dieser Artikel ist aktuell nicht mehr verfügbar.");
            }


            var approver = await _approvers.FindActiveByEmailAsync(supervisorEmail);

            if (approver is null)
                return (false, "Bitte einen gültigen Vorgesetzten aus der Liste auswählen.");

            var normalizedSupervisorName = approver.Name;
            var normalizedSupervisorEmail = approver.Email;

            var request = new EquipmentRequest
            {
                PositionId = selectedPosition.ID,
                RequesterName = Normalize(name),
                RequesterEmail = Normalize(email),
                Department = normalizedSupervisorName,
                SupervisorName = normalizedSupervisorName,
                SupervisorEmail = normalizedSupervisorEmail,
                Reason = Normalize(reason),
                UsedItemOk = usedItemOk,
                TicketNumber = $"PENDING-{Guid.NewGuid():N}",
                BossComment = " ",
                Status = RequestStatus.Pending,
                RequestDate = DateTime.Now,
                DecisionDate = null,
                FulfillmentType = null,
                FulfillmentDate = null,
                ApproveToken = Guid.NewGuid().ToString("N"),
                DenyToken = Guid.NewGuid().ToString("N")
            };

            db.EquipmentRequests.Add(request);
            await db.SaveChangesAsync();

            request.TicketNumber = EquipmentRequest.BuildTicketNumber(request.Id);
            await db.SaveChangesAsync();

            _ = SendBossNotificationAsync(request, selectedPosition);

            return (true, "");
        }

        public async Task<List<EquipmentRequest>> GetRequestsAsync(RequestStatus? status = null)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var q = db.EquipmentRequests
                .Include(r => r.Position).ThenInclude(p => p!.Category)
                .AsNoTracking()
                .AsQueryable();

            if (status.HasValue)
                q = q.Where(r => r.Status == status.Value);

            return await q
                .OrderByDescending(r => r.RequestDate)
                .ToListAsync();
        }

        public async Task<List<EquipmentRequest>> GetRequestsByEmailAsync(string email)
        {
            var normalizedEmail = NormalizeEmail(email);

            if (string.IsNullOrWhiteSpace(normalizedEmail))
                return new List<EquipmentRequest>();

            await using var db = await _dbFactory.CreateDbContextAsync();

            return await db.EquipmentRequests
                .Include(r => r.Position).ThenInclude(p => p!.Category)
                .AsNoTracking()
                .Where(r => r.RequesterEmail.ToLower() == normalizedEmail)
                .OrderByDescending(r => r.RequestDate)
                .ToListAsync();
        }

        public async Task<List<EquipmentRequest>> GetRequestsForSupervisorAsync(string supervisorEmail)
        {
            var normalized = NormalizeEmail(supervisorEmail);

            if (string.IsNullOrWhiteSpace(normalized))
                return new List<EquipmentRequest>();

            await using var db = await _dbFactory.CreateDbContextAsync();

            return await db.EquipmentRequests
                .Include(r => r.Position).ThenInclude(p => p!.Category)
                .AsNoTracking()
                .Where(r => r.SupervisorEmail.ToLower() == normalized)
                .OrderByDescending(r => r.RequestDate)
                .ToListAsync();
        }

        public async Task<bool> HasSupervisorRequestsAsync(string supervisorEmail)
        {
            var normalized = NormalizeEmail(supervisorEmail);

            if (string.IsNullOrWhiteSpace(normalized))
                return false;

            await using var db = await _dbFactory.CreateDbContextAsync();

            return await db.EquipmentRequests
                .AnyAsync(r => r.SupervisorEmail.ToLower() == normalized);
        }

        public async Task<EquipmentRequest?> GetRequestByTokenAsync(string token, bool isApprove)
        {
            if (string.IsNullOrWhiteSpace(token))
                return null;

            await using var db = await _dbFactory.CreateDbContextAsync();

            return isApprove
                ? await db.EquipmentRequests
                    .Include(r => r.Position).ThenInclude(p => p!.Category)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(r => r.ApproveToken == token)
                : await db.EquipmentRequests
                    .Include(r => r.Position).ThenInclude(p => p!.Category)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(r => r.DenyToken == token);
        }

        public async Task<(bool ok, string error)> ApproveAsync(
            int requestId,
            string actorEmail,
            bool actorIsAdmin,
            string bossComment = "")
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var req = await db.EquipmentRequests
                .Include(r => r.Position).ThenInclude(p => p!.Category)
                .FirstOrDefaultAsync(r => r.Id == requestId);

            if (req is null)
                return (false, "Anfrage wurde nicht gefunden.");

            if (req.Status != RequestStatus.Pending)
                return (false, "Diese Anfrage wurde bereits bearbeitet.");
            if (!CanActOnRequest(req, actorEmail, actorIsAdmin))
                return (false, "Du darfst diese Anfrage nicht bearbeiten.");

            req.Status = RequestStatus.Approved;
            req.DecisionDate = DateTime.Now;
            req.BossComment = string.IsNullOrWhiteSpace(bossComment) ? " " : bossComment.Trim();

            await db.SaveChangesAsync();

            _ = SendItNotificationAsync(req);

            return (true, "");
        }

        public async Task<(bool ok, string error)> DenyAsync(
            int requestId,
            string actorEmail,
            bool actorIsAdmin,
            string bossComment)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var req = await db.EquipmentRequests
                .Include(r => r.Position).ThenInclude(p => p!.Category)
                .FirstOrDefaultAsync(r => r.Id == requestId);

            if (req is null)
                return (false, "Anfrage wurde nicht gefunden.");

            if (req.Status != RequestStatus.Pending)
                return (false, "Diese Anfrage wurde bereits bearbeitet.");

            if (!CanActOnRequest(req, actorEmail, actorIsAdmin))
                return (false, "Du darfst diese Anfrage nicht bearbeiten.");

            req.Status = RequestStatus.Denied;
            req.DecisionDate = DateTime.Now;
            req.BossComment = string.IsNullOrWhiteSpace(bossComment) ? " " : bossComment.Trim();

            await db.SaveChangesAsync();

            _ = SendDenialToUserAsync(req);

            return (true, "");
        }

        public async Task<(bool ok, string error)> ApproveByTokenAsync(int requestId, string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return (false, "Invalid link");

            await using var db = await _dbFactory.CreateDbContextAsync();

            var req = await db.EquipmentRequests
                .Include(r => r.Position).ThenInclude(p => p!.Category)
                .FirstOrDefaultAsync(r => r.Id == requestId && r.ApproveToken == token);

            if (req is null)
                return (false, "Invalid link");

            if (req.Status != RequestStatus.Pending)
                return (false, "Already handled");

            req.Status = RequestStatus.Approved;
            req.DecisionDate = DateTime.Now;
            req.BossComment = " ";

            await db.SaveChangesAsync();

            _ = SendItNotificationAsync(req);

            return (true, "");
        }

        public async Task<(bool ok, string error)> DenyByTokenAsync(int requestId, string token, string bossComment)
        {
            if (string.IsNullOrWhiteSpace(token))
                return (false, "Invalid link");

            await using var db = await _dbFactory.CreateDbContextAsync();

            var req = await db.EquipmentRequests
                .Include(r => r.Position).ThenInclude(p => p!.Category)
                .FirstOrDefaultAsync(r => r.Id == requestId && r.DenyToken == token);

            if (req is null)
                return (false, "Invalid link");

            if (req.Status != RequestStatus.Pending)
                return (false, "Already handled");

            req.Status = RequestStatus.Denied;
            req.DecisionDate = DateTime.Now;
            req.BossComment = string.IsNullOrWhiteSpace(bossComment) ? " " : bossComment.Trim();

            await db.SaveChangesAsync();

            _ = SendDenialToUserAsync(req);

            return (true, "");
        }

        public async Task<(bool ok, string error)> MarkPreparingAsync(int requestId, bool actorIsAdmin)
        {
            if (!actorIsAdmin)
                return (false, "Nur Admins dürfen den Status auf In Vorbereitung setzen.");

            await using var db = await _dbFactory.CreateDbContextAsync();

            var req = await db.EquipmentRequests.FindAsync(requestId);
            if (req is null)
                return (false, "Anfrage wurde nicht gefunden.");

            if (req.Status != RequestStatus.Approved)
                return (false, "Nur genehmigte Anfragen können vorbereitet werden.");

            req.Status = RequestStatus.Preparing;
            await db.SaveChangesAsync();

            return (true, "");
        }

        public Task<(bool ok, string error)> MarkCollectedAsync(int requestId, bool actorIsAdmin)
        {
            return CompleteHandoverInternalAsync(
                requestId,
                actorIsAdmin,
                RequestFulfillmentType.NewFromStock,
                sendPickupEmail: false);
        }

        public Task<(bool ok, string error)> SendPickupEmailAndCollectAsync(int requestId, bool actorIsAdmin)
        {
            return CompleteHandoverAsync(
                requestId,
                actorIsAdmin,
                RequestFulfillmentType.NewFromStock);
        }

        public Task<(bool ok, string error)> CompleteHandoverAsync(
            int requestId,
            bool actorIsAdmin,
            RequestFulfillmentType fulfillmentType)
        {
            return CompleteHandoverInternalAsync(
                requestId,
                actorIsAdmin,
                fulfillmentType,
                sendPickupEmail: true);
        }

        private async Task<(bool ok, string error)> CompleteHandoverInternalAsync(
            int requestId,
            bool actorIsAdmin,
            RequestFulfillmentType fulfillmentType,
            bool sendPickupEmail)
        {
            if (!actorIsAdmin)
                return (false, "Nur Admins dürfen Equipment herausgeben.");

            if (!Enum.IsDefined(typeof(RequestFulfillmentType), fulfillmentType))
                return (false, "Ungültige Herausgabe-Art.");

            await using var db = await _dbFactory.CreateDbContextAsync();

            var req = await db.EquipmentRequests
                .Include(r => r.Position).ThenInclude(p => p!.Category)
                .FirstOrDefaultAsync(r => r.Id == requestId);

            if (req is null)
                return (false, "Anfrage wurde nicht gefunden.");

            if (req.Status != RequestStatus.Preparing)
                return (false, "Nur vorbereitete Anfragen können herausgegeben werden.");

            if (fulfillmentType == RequestFulfillmentType.UsedReturnedItem && !req.UsedItemOk)
                return (false, "Der Benutzer hat kein Gebrauchtgerät akzeptiert. Bitte Neuware ausgeben.");

            if (req.Position is null)
                return (false, "Der reservierte Lagerartikel wurde nicht gefunden.");

            var expectedStockCondition = fulfillmentType == RequestFulfillmentType.NewFromStock
                ? StockCondition.New
                : StockCondition.Used;

            if (req.Position.StockCondition != expectedStockCondition)
            {
                var actualLabel = req.Position.StockCondition == StockCondition.New ? "Neuware" : "Gebrauchtware";

                return (false, $"Diese Anfrage hat {actualLabel} reserviert. Bitte als {actualLabel} herausgeben oder die Reservierung später gezielt umbuchen.");
            }

            EnsureTicketNumber(req);

            req.Status = RequestStatus.Collected;
            req.FulfillmentType = fulfillmentType;
            req.FulfillmentDate = DateTime.Now;

            await AddIssueIfMissingAsync(db, req);

            await db.SaveChangesAsync();

            try
            {
                await _lowStock.CheckAndNotifyAsync($"Herausgabe {GetTicketNumber(req)}");
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Mindestbestand-Prüfung nach Herausgabe {RequestId} fehlgeschlagen.", req.Id);
            }

            if (sendPickupEmail)
            {
                try
                {
                    await SendPickupEmailAsync(req, fulfillmentType);
                }
                catch (Exception ex)
                {
                    _log.LogError(ex, "Failed to send pickup email for request {Id}", req.Id);
                    return (true, "Herausgabe wurde gespeichert, aber die Abholbenachrichtigung konnte nicht gesendet werden.");
                }
            }

            return (true, "");
        }

        private async Task SendPickupEmailAsync(EquipmentRequest req, RequestFulfillmentType fulfillmentType)
        {
            var subject = $"[{GetTicketNumber(req)}] Dein Equipment ist abholbereit! 📦";
            var fulfillmentLabel = fulfillmentType == RequestFulfillmentType.NewFromStock
                ? "Neuware aus dem Lager"
                : "Gebrauchtgerät aus Rückläuferbestand";

            var body = $@"
<div style='font-family:Arial,sans-serif;max-width:600px;'>
  <div style='background:#005691;color:white;padding:16px 24px;border-radius:4px 4px 0 0;'>
    <h2 style='margin:0;'>📦 Equipment abholbereit</h2>
  </div>
  <div style='border:1px solid #ddd;border-top:0;padding:24px;border-radius:0 0 4px 4px;'>
    <p>Hallo {req.RequesterName},</p>
    <p>dein angefordertes Equipment liegt für dich bereit:</p>
    <table style='width:100%;border-collapse:collapse;margin:16px 0;'>
      <tr><td style='padding:8px;background:#f5f5f5;font-weight:bold;'>Ticket-Nr.</td><td style='padding:8px;'>{GetTicketNumber(req)}</td></tr>
      <tr><td style='padding:8px;background:#f5f5f5;font-weight:bold;'>Kategorie</td><td style='padding:8px;'>{req.Position?.Category?.Name}</td></tr>
      <tr><td style='padding:8px;background:#f5f5f5;font-weight:bold;'>Beschreibung</td><td style='padding:8px;'>{req.Position?.Description?.Trim()}</td></tr>
      <tr><td style='padding:8px;background:#f5f5f5;font-weight:bold;'>Interne Nr.</td><td style='padding:8px;'>{req.Position?.OrderNumber?.Trim()}</td></tr>
      <tr><td style='padding:8px;background:#f5f5f5;font-weight:bold;'>Herausgabe</td><td style='padding:8px;'>{fulfillmentLabel}</td></tr>
    </table>
    <p>Bitte komm zum IT-Büro zur Abholung. Bei Fragen melde dich gerne.</p>
    <p style='color:#999;font-size:12px;'>IT Lagerverwaltung – Robert Bosch GmbH</p>
  </div>
</div>";

            await _email.SendAsync(req.RequesterEmail, subject, body);
        }

        private static bool CanActOnRequest(EquipmentRequest req, string actorEmail, bool actorIsAdmin)
        {

            if (actorIsAdmin)
                return true;

            return string.Equals(
                req.SupervisorEmail?.Trim(),
                actorEmail?.Trim(),
                StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeEmail(string? email)
        {
            return email?.Trim().ToLowerInvariant() ?? "";
        }

        private static string UsedItemOkText(EquipmentRequest req)
        {
            return req.UsedItemOk
                ? "Ja - gebrauchtes / zurückgenommenes Gerät ist akzeptiert"
                : "Nein - bitte Neuware ausgeben";
        }

        private static void EnsureTicketNumber(EquipmentRequest req)
        {
            if (req.Id > 0 && string.IsNullOrWhiteSpace(req.TicketNumber))
                req.TicketNumber = EquipmentRequest.BuildTicketNumber(req.Id);
        }

        private static string GetTicketNumber(EquipmentRequest req)
        {
            EnsureTicketNumber(req);

            return string.IsNullOrWhiteSpace(req.TicketNumber)
                ? EquipmentRequest.BuildTicketNumber(req.Id)
                : req.TicketNumber.Trim();
        }

        private static async Task AddIssueIfMissingAsync(AppDbContext db, EquipmentRequest req)
        {
            var ticketNumber = GetTicketNumber(req);

            var alreadyIssued = await db.Issues
                .AnyAsync(i => i.PositionId == req.PositionId && i.TicketNumber == ticketNumber);

            if (alreadyIssued)
                return;

            db.Issues.Add(new Issue
            {
                PositionId = req.PositionId,
                TicketNumber = ticketNumber,
                Username = req.RequesterName,
                CostCentre = req.SupervisorName,
                IssueDate = DateTime.Now,
                TakeBackDate = null
            });
        }

        private async Task SendBossNotificationAsync(EquipmentRequest req, Position pos)
        {
            try
            {
                var baseUrl = _cfg["App:BaseUrl"] ?? "https://your-server";
                var bossEmail = req.SupervisorEmail?.Trim();
                var approveUrl = $"{baseUrl}/api/requests/{req.Id}/approve/{req.ApproveToken}";
                var denyUrl = $"{baseUrl}/api/requests/{req.Id}/deny/{req.DenyToken}";
                var subject = $"[{GetTicketNumber(req)}] Equipment Request – {req.RequesterName} möchte {pos.Category.Name}";

                if (string.IsNullOrWhiteSpace(bossEmail))
                {
                    _log.LogWarning("No supervisor email stored for request {Id}", req.Id);
                    return;
                }

                var body = $@"
<div style='font-family:Arial,sans-serif;max-width:600px;'>
  <div style='background:#005691;color:white;padding:16px 24px;border-radius:4px 4px 0 0;'>
    <h2 style='margin:0;'>📦 Equipment Request</h2>
  </div>
  <div style='border:1px solid #ddd;border-top:0;padding:24px;border-radius:0 0 4px 4px;'>
    <p><strong>{req.RequesterName}</strong> hat folgendes Equipment angefragt:</p>
    <table style='width:100%;border-collapse:collapse;margin:16px 0;'>
      <tr><td style='padding:8px;background:#f5f5f5;font-weight:bold;'>Ticket-Nr.</td><td style='padding:8px;'>{GetTicketNumber(req)}</td></tr>
      <tr><td style='padding:8px;background:#f5f5f5;font-weight:bold;'>Vorgesetzter</td><td style='padding:8px;'>{req.SupervisorName}</td></tr>
      <tr><td style='padding:8px;background:#f5f5f5;font-weight:bold;'>Kategorie</td><td style='padding:8px;'>{pos.Category.Name}</td></tr>
      <tr><td style='padding:8px;background:#f5f5f5;font-weight:bold;'>Beschreibung</td><td style='padding:8px;'>{pos.Description.Trim()}</td></tr>
      <tr><td style='padding:8px;background:#f5f5f5;font-weight:bold;'>Interne Nr.</td><td style='padding:8px;'>{pos.OrderNumber.Trim()}</td></tr>
      <tr><td style='padding:8px;background:#f5f5f5;font-weight:bold;'>Requester Email</td><td style='padding:8px;'>{req.RequesterEmail}</td></tr>
      <tr><td style='padding:8px;background:#f5f5f5;font-weight:bold;'>Supervisor Email</td><td style='padding:8px;'>{req.SupervisorEmail}</td></tr>
      <tr><td style='padding:8px;background:#f5f5f5;font-weight:bold;'>Begründung</td><td style='padding:8px;'>{req.Reason}</td></tr>
      <tr><td style='padding:8px;background:#f5f5f5;font-weight:bold;'>Gebrauchtgerät akzeptiert</td><td style='padding:8px;'>{UsedItemOkText(req)}</td></tr>
      <tr><td style='padding:8px;background:#f5f5f5;font-weight:bold;'>Reservierter Zustand</td><td style='padding:8px;'>{pos.StockConditionLabel}</td></tr>
      <tr><td style='padding:8px;background:#f5f5f5;font-weight:bold;'>Datum</td><td style='padding:8px;'>{req.RequestDate:dd.MM.yyyy HH:mm}</td></tr>
    </table>
    <p style='margin-top:24px;'>Bitte wähle eine Option:</p>
    <a href='{approveUrl}' style='display:inline-block;background:#4caf50;color:white;padding:12px 28px;border-radius:4px;text-decoration:none;font-weight:bold;margin-right:12px;'>✅ Genehmigen</a>
    <a href='{denyUrl}' style='display:inline-block;background:#EA0016;color:white;padding:12px 28px;border-radius:4px;text-decoration:none;font-weight:bold;'>❌ Ablehnen</a>
    <p style='margin-top:24px;color:#999;font-size:12px;'>Diese Links sind einmalig verwendbar. Alternativ kannst du die Anfrage im <a href='{baseUrl}/admin'>Admin-Panel</a> verwalten.</p>
  </div>
</div>";

                await _email.SendAsync(bossEmail, subject, body);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Failed to send boss notification for request {Id}", req.Id);
            }
        }

        private async Task SendItNotificationAsync(EquipmentRequest req)
        {
            try
            {
                var itEmail = _cfg["Email:ItEmail"] ?? "it@bosch.com";
                var baseUrl = _cfg["App:BaseUrl"] ?? "https://your-server";
                var subject = $"[GENEHMIGT] [{GetTicketNumber(req)}] Equipment vorbereiten für {req.RequesterName}";

                var body = $@"
<div style='font-family:Arial,sans-serif;max-width:600px;'>
  <div style='background:#005691;color:white;padding:16px 24px;border-radius:4px 4px 0 0;'>
    <h2 style='margin:0;'>🛠️ Equipment vorbereiten</h2>
  </div>
  <div style='border:1px solid #ddd;border-top:0;padding:24px;border-radius:0 0 4px 4px;'>
    <p>Eine Equipment-Anfrage wurde vom Vorgesetzten <strong>genehmigt</strong>. Bitte das folgende Equipment vorbereiten:</p>
    <table style='width:100%;border-collapse:collapse;margin:16px 0;'>
      <tr><td style='padding:8px;background:#f5f5f5;font-weight:bold;'>Ticket-Nr.</td><td style='padding:8px;'>{GetTicketNumber(req)}</td></tr>
      <tr><td style='padding:8px;background:#f5f5f5;font-weight:bold;'>Für</td><td style='padding:8px;'>{req.RequesterName}</td></tr>
      <tr><td style='padding:8px;background:#f5f5f5;font-weight:bold;'>Requester Email</td><td style='padding:8px;'>{req.RequesterEmail}</td></tr>
      <tr><td style='padding:8px;background:#f5f5f5;font-weight:bold;'>Vorgesetzter</td><td style='padding:8px;'>{req.SupervisorName} ({req.SupervisorEmail})</td></tr>
      <tr><td style='padding:8px;background:#f5f5f5;font-weight:bold;'>Equipment</td><td style='padding:8px;'>{req.Position?.Category?.Name} – {req.Position?.Description?.Trim()}</td></tr>
      <tr><td style='padding:8px;background:#f5f5f5;font-weight:bold;'>Interne Nr.</td><td style='padding:8px;'>{req.Position?.OrderNumber?.Trim()}</td></tr>
      <tr><td style='padding:8px;background:#f5f5f5;font-weight:bold;'>Gebrauchtgerät akzeptiert</td><td style='padding:8px;'>{UsedItemOkText(req)}</td></tr>
    </table>
    <p>
      Wenn das Equipment bereit ist, kannst du im
      <a href='{baseUrl}/admin'>Admin-Panel</a> zwischen
      <strong>„Gebraucht“</strong> und <strong>„Neu“</strong> wählen – das sendet automatisch eine Abholbenachrichtigung an den Benutzer.
    </p>
  </div>
</div>";

                await _email.SendAsync(itEmail, subject, body);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Failed to send IT notification for request {Id}", req.Id);
            }
        }

        private async Task SendDenialToUserAsync(EquipmentRequest req)
        {
            try
            {
                var subject = $"[{GetTicketNumber(req)}] Equipment Request – Request Denied";

                var body = $@"
<div style='font-family:Arial,sans-serif;max-width:600px;'>
  <div style='background:#EA0016;color:white;padding:16px 24px;border-radius:4px 4px 0 0;'>
    <h2 style='margin:0;'>Equipment Request – Abgelehnt</h2>
  </div>
  <div style='border:1px solid #ddd;border-top:0;padding:24px;border-radius:0 0 4px 4px;'>
    <p>Hallo {req.RequesterName},</p>
    <p>leider wurde deine Anfrage für <strong>{req.Position?.Category?.Name} – {req.Position?.Description?.Trim()}</strong> abgelehnt.</p>
    <p><strong>Ticket-Nr.:</strong> {GetTicketNumber(req)}</p>
    <p><strong>Vorgesetzter:</strong> {req.SupervisorName}</p>
    {(string.IsNullOrWhiteSpace(req.BossComment) || req.BossComment == " " ? "" : $"<p><strong>Kommentar:</strong> {req.BossComment}</p>")}
    <p>Bei Fragen wende dich bitte direkt an {req.SupervisorName}.</p>
    <p style='color:#999;font-size:12px;'>IT Lagerverwaltung</p>
  </div>
</div>";

                await _email.SendAsync(req.RequesterEmail, subject, body);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Failed to send denial email for request {Id}", req.Id);
            }
        }
    }
}
