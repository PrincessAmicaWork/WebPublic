using Lagerverwaltung.Web.Data;
using Lagerverwaltung.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace Lagerverwaltung.Web.Services;

public record LowStockCheckResult(
    int CriticalCategories,
    int EmailsSent,
    List<string> Messages);

public interface ILowStockNotificationService
{
    Task<LowStockCheckResult> CheckAndNotifyAsync(
        string triggerReason,
        CancellationToken token = default);
}

public class LowStockNotificationService : ILowStockNotificationService
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
    private readonly IConfiguration _cfg;
    private readonly ILogger<LowStockNotificationService> _log;

    public LowStockNotificationService(
        IDbContextFactory<AppDbContext> dbFactory,
        IEmailService email,
        IConfiguration cfg,
        ILogger<LowStockNotificationService> log)
    {
        _dbFactory = dbFactory;
        _email = email;
        _cfg = cfg;
        _log = log;
    }

    public async Task<LowStockCheckResult> CheckAndNotifyAsync(
        string triggerReason,
        CancellationToken token = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(token);

        var messages = new List<string>();
        var sent = 0;

        // Wichtig:
        // Kritisch = Mindestbestand erreicht/unterschritten.
        // E-Mail = nur wenn die Kategorie Benachrichtigung aktiv hat.
        // Darum laden wir ALLE kritischen Kategorien, nicht nur die Mail-aktiven.
        var rows = await db.Categories
            .AsNoTracking()
            .Where(c =>
                c.MinimumAmount > 0 &&
                c.Name != null &&
                c.Name.Trim() != "")
            .OrderBy(c => c.Name)
            .Select(c => new LowStockCategoryProjection
            {
                CategoryId = c.ID,
                CategoryName = c.Name,
                RestockEmail = c.RestockEmail,
                MinimumAmount = c.MinimumAmount,
                NeedsNotification = c.NeedsNotificationValue == 1,
                AvailableAmount = db.Positions.Count(p =>
                    p.CategoryId == c.ID &&
                    !db.Issues.Any(i => i.PositionId == p.ID && i.TakeBackDate == null) &&
                    !db.EquipmentRequests.Any(r => r.PositionId == p.ID && ReservedRequestStatuses.Contains(r.Status)) &&
                    !db.EquipmentOrderLines.Any(l => l.PositionId == p.ID && ReservedOrderLineStatuses.Contains(l.Status))),
                NewAvailableAmount = db.Positions.Count(p =>
                    p.CategoryId == c.ID &&
                    p.StockCondition == StockCondition.New &&
                    !db.Issues.Any(i => i.PositionId == p.ID && i.TakeBackDate == null) &&
                    !db.EquipmentRequests.Any(r => r.PositionId == p.ID && ReservedRequestStatuses.Contains(r.Status)) &&
                    !db.EquipmentOrderLines.Any(l => l.PositionId == p.ID && ReservedOrderLineStatuses.Contains(l.Status))),
                UsedAvailableAmount = db.Positions.Count(p =>
                    p.CategoryId == c.ID &&
                    p.StockCondition == StockCondition.Used &&
                    !db.Issues.Any(i => i.PositionId == p.ID && i.TakeBackDate == null) &&
                    !db.EquipmentRequests.Any(r => r.PositionId == p.ID && ReservedRequestStatuses.Contains(r.Status)) &&
                    !db.EquipmentOrderLines.Any(l => l.PositionId == p.ID && ReservedOrderLineStatuses.Contains(l.Status)))
            })
            .ToListAsync(token);

        var criticalRows = rows
            .Where(x => x.AvailableAmount <= x.MinimumAmount)
            .ToList();

        var notificationCooldown = GetNotificationCooldown();

        foreach (var category in criticalRows)
        {
            token.ThrowIfCancellationRequested();

            var categoryName = category.CategoryName?.Trim() ?? $"Kategorie {category.CategoryId}";

            if (!category.NeedsNotification)
            {
                messages.Add($"{categoryName}: kritisch, aber Benachrichtigung ist in der Kategorie deaktiviert.");
                continue;
            }

            var recipient = GetRecipient(category.RestockEmail);
            if (string.IsNullOrWhiteSpace(recipient))
            {
                messages.Add($"{categoryName}: kritisch, aber keine Empf\u00e4ngeradresse konfiguriert.");
                continue;
            }

            var lastLog = await db.LowStockNotificationLogs
                .AsNoTracking()
                .Where(x => x.CategoryId == category.CategoryId)
                .OrderByDescending(x => x.SentAt)
                .FirstOrDefaultAsync(token);

            if (ShouldSuppress(lastLog, notificationCooldown, out var nextAllowedAt))
            {
                messages.Add($"{categoryName}: kritisch, E-Mail wurde bereits am {lastLog!.SentAt:dd.MM.yyyy HH:mm} gesendet. Nächste E-Mail frühestens am {nextAllowedAt:dd.MM.yyyy HH:mm}.");
                continue;
            }

            var subject = $"[Lager] Mindestbestand erreicht: {categoryName}";
            var body = BuildBody(
                category,
                recipient,
                triggerReason);

            try
            {
                await _email.SendAsync(recipient, subject, body);

                db.LowStockNotificationLogs.Add(new LowStockNotificationLog
                {
                    CategoryId = category.CategoryId,
                    SentAt = DateTime.Now,
                    AvailableAmount = category.AvailableAmount,
                    MinimumAmount = category.MinimumAmount,
                    RecipientEmail = recipient,
                    TriggerReason = string.IsNullOrWhiteSpace(triggerReason) ? "Manuelle Pr\u00fcfung" : triggerReason.Trim()
                });

                await db.SaveChangesAsync(token);

                sent++;
                messages.Add($"{categoryName}: E-Mail an {recipient} gesendet.");
            }
            catch (Exception ex)
            {
                _log.LogError(
                    ex,
                    "Low-stock email failed for category {CategoryId}",
                    category.CategoryId);

                messages.Add($"{categoryName}: E-Mail konnte nicht gesendet werden: {ex.Message}");
            }
        }

        return new LowStockCheckResult(criticalRows.Count, sent, messages);
    }

    private string GetRecipient(string? categoryEmail)
    {
        var stockEmail = _cfg["Email:StockEmail"]?.Trim();

        if (!string.IsNullOrWhiteSpace(stockEmail))
            return stockEmail;

        var normalizedCategoryEmail = categoryEmail?.Trim();

        if (!string.IsNullOrWhiteSpace(normalizedCategoryEmail))
            return normalizedCategoryEmail;

        var itEmail = _cfg["Email:ItEmail"]?.Trim();

        if (!string.IsNullOrWhiteSpace(itEmail))
            return itEmail;

        return "";
    }

    private TimeSpan GetNotificationCooldown()
    {
        var configuredHours = _cfg["LowStock:NotificationCooldownHours"]
            ?? _cfg["Email:LowStockCooldownHours"];

        if (int.TryParse(configuredHours, out var hours) && hours > 0)
            return TimeSpan.FromHours(Math.Min(hours, 24 * 365));

        // Default: no daily spam. One reminder per critical category per week.
        return TimeSpan.FromDays(7);
    }

    private static bool ShouldSuppress(
        LowStockNotificationLog? lastLog,
        TimeSpan cooldown,
        out DateTime nextAllowedAt)
    {
        nextAllowedAt = DateTime.MinValue;

        if (lastLog is null)
            return false;

        nextAllowedAt = lastLog.SentAt.Add(cooldown);

        return DateTime.Now < nextAllowedAt;
    }

    private static string BuildBody(
        LowStockCategoryProjection category,
        string recipient,
        string triggerReason)
    {
        var categoryName = category.CategoryName?.Trim() ?? "";
        var reason = string.IsNullOrWhiteSpace(triggerReason)
            ? "Automatische Pr\u00fcfung"
            : triggerReason.Trim();

        return $@"
<div style='font-family:Arial,sans-serif;max-width:650px;'>
  <div style='background:#EA0016;color:white;padding:16px 24px;border-radius:4px 4px 0 0;'>
    <h2 style='margin:0;'>Mindestbestand erreicht</h2>
  </div>
  <div style='border:1px solid #ddd;border-top:0;padding:24px;border-radius:0 0 4px 4px;'>
    <p>F&uuml;r die Kategorie <strong>{categoryName}</strong> wurde der Mindestbestand erreicht oder unterschritten.</p>
    <table style='width:100%;border-collapse:collapse;margin:16px 0;'>
      <tr><td style='padding:8px;background:#f5f5f5;font-weight:bold;'>Kategorie</td><td style='padding:8px;'>{categoryName}</td></tr>
      <tr><td style='padding:8px;background:#f5f5f5;font-weight:bold;'>Verf&uuml;gbar gesamt</td><td style='padding:8px;'>{category.AvailableAmount}</td></tr>
      <tr><td style='padding:8px;background:#f5f5f5;font-weight:bold;'>Davon Neu</td><td style='padding:8px;'>{category.NewAvailableAmount}</td></tr>
      <tr><td style='padding:8px;background:#f5f5f5;font-weight:bold;'>Davon Gebraucht</td><td style='padding:8px;'>{category.UsedAvailableAmount}</td></tr>
      <tr><td style='padding:8px;background:#f5f5f5;font-weight:bold;'>Mindestbestand</td><td style='padding:8px;'>{category.MinimumAmount}</td></tr>
      <tr><td style='padding:8px;background:#f5f5f5;font-weight:bold;'>Ausl&ouml;ser</td><td style='padding:8px;'>{reason}</td></tr>
      <tr><td style='padding:8px;background:#f5f5f5;font-weight:bold;'>Empf&auml;nger</td><td style='padding:8px;'>{recipient}</td></tr>
    </table>
    <p>Bitte pr&uuml;fen, ob nachbestellt werden muss.</p>
    <p style='color:#999;font-size:12px;'>IT Lagerverwaltung</p>
  </div>
</div>";
    }

    private sealed class LowStockCategoryProjection
    {
        public int CategoryId { get; set; }
        public string? CategoryName { get; set; }
        public string? RestockEmail { get; set; }
        public int MinimumAmount { get; set; }
        public bool NeedsNotification { get; set; }
        public int AvailableAmount { get; set; }
        public int NewAvailableAmount { get; set; }
        public int UsedAvailableAmount { get; set; }
    }
}
