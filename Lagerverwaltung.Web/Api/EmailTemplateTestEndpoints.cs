using System.Net;
using Lagerverwaltung.Web.Data;
using Lagerverwaltung.Web.Models;
using Lagerverwaltung.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Lagerverwaltung.Web.Api;

public static class EmailTemplateTestEndpoints
{
    public static void MapEmailTemplateTestEndpoints(this WebApplication app)
    {
        var group = app
            .MapGroup("/api/admin/email-tests")
            .RequireAuthorization("AdminOnly")
            .WithTags("Email Template Tests");

        group.MapGet("/templates", () => Results.Ok(new
        {
            templates = new[]
            {
                "order-approval-request",
                "order-approved-it",
                "order-denied",
                "return-requested",
                "return-confirmed",
                "all"
            },
            example = new EmailTemplateTestRequest
            {
                To = "pol2sn@bosch.com",
                OrderId = 1,
                ReturnRequestId = null,
                Comment = "Test-Kommentar aus Swagger"
            }
        }));

        group.MapPost("/order-approval-request", async (
            [FromBody] EmailTemplateTestRequest req,
            IDbContextFactory<AppDbContext> dbFactory,
            IEmailService email,
            IConfiguration cfg,
            CancellationToken ct) =>
        {
            var validation = Validate(req);
            if (validation is not null)
                return validation;

            var order = await LoadOrderOrSampleAsync(req.OrderId, dbFactory, ct);
            var subject = $"[TEST] [{order.DisplayTicketNumber}] Equipment Bestellung - Freigabe benötigt";
            var body = BuildOrderApprovalRequestEmail(order, cfg, req.Comment);

            await email.SendAsync(req.To.Trim(), subject, body);

            return Results.Ok(new
            {
                ok = true,
                template = "order-approval-request",
                sentTo = req.To.Trim(),
                orderId = req.OrderId,
                ticket = order.DisplayTicketNumber
            });
        })
        .WithName("SendTestOrderApprovalRequestEmail")
        .WithSummary("Sendet eine Testmail für die Freigabe-Anfrage an einen frei wählbaren Empfänger.");

        group.MapPost("/order-approved-it", async (
            [FromBody] EmailTemplateTestRequest req,
            IDbContextFactory<AppDbContext> dbFactory,
            IEmailService email,
            IConfiguration cfg,
            CancellationToken ct) =>
        {
            var validation = Validate(req);
            if (validation is not null)
                return validation;

            var order = await LoadOrderOrSampleAsync(req.OrderId, dbFactory, ct);
            var subject = $"[TEST] [GENEHMIGT] [{order.DisplayTicketNumber}] Equipment Bestellung vorbereiten";
            var body = BuildOrderApprovedItEmail(order, cfg, req.Comment);

            await email.SendAsync(req.To.Trim(), subject, body);

            return Results.Ok(new
            {
                ok = true,
                template = "order-approved-it",
                sentTo = req.To.Trim(),
                orderId = req.OrderId,
                ticket = order.DisplayTicketNumber
            });
        })
        .WithName("SendTestOrderApprovedItEmail")
        .WithSummary("Sendet eine Testmail für die IT-Benachrichtigung nach Genehmigung.");

        group.MapPost("/order-denied", async (
            [FromBody] EmailTemplateTestRequest req,
            IDbContextFactory<AppDbContext> dbFactory,
            IEmailService email,
            IConfiguration cfg,
            CancellationToken ct) =>
        {
            var validation = Validate(req);
            if (validation is not null)
                return validation;

            var order = await LoadOrderOrSampleAsync(req.OrderId, dbFactory, ct);
            var subject = $"[TEST] [{order.DisplayTicketNumber}] Equipment Bestellung - abgelehnt";
            var body = BuildOrderDeniedEmail(order, cfg, req.Comment);

            await email.SendAsync(req.To.Trim(), subject, body);

            return Results.Ok(new
            {
                ok = true,
                template = "order-denied",
                sentTo = req.To.Trim(),
                orderId = req.OrderId,
                ticket = order.DisplayTicketNumber
            });
        })
        .WithName("SendTestOrderDeniedEmail")
        .WithSummary("Sendet eine Testmail für eine abgelehnte Bestellung.");

        group.MapPost("/return-requested", async (
            [FromBody] EmailTemplateTestRequest req,
            IDbContextFactory<AppDbContext> dbFactory,
            IEmailService email,
            IConfiguration cfg,
            CancellationToken ct) =>
        {
            var validation = Validate(req);
            if (validation is not null)
                return validation;

            var data = await LoadReturnOrSampleAsync(req.ReturnRequestId, dbFactory, ct);
            var subject = $"[TEST] Rückgabe angemeldet - {data.TicketNumber}";
            var body = BuildReturnRequestedEmail(data, cfg, req.Comment);

            await email.SendAsync(req.To.Trim(), subject, body);

            return Results.Ok(new
            {
                ok = true,
                template = "return-requested",
                sentTo = req.To.Trim(),
                returnRequestId = req.ReturnRequestId,
                ticket = data.TicketNumber
            });
        })
        .WithName("SendTestReturnRequestedEmail")
        .WithSummary("Sendet eine Testmail für eine angemeldete Rückgabe.");

        group.MapPost("/return-confirmed", async (
            [FromBody] EmailTemplateTestRequest req,
            IDbContextFactory<AppDbContext> dbFactory,
            IEmailService email,
            IConfiguration cfg,
            CancellationToken ct) =>
        {
            var validation = Validate(req);
            if (validation is not null)
                return validation;

            var data = await LoadReturnOrSampleAsync(req.ReturnRequestId, dbFactory, ct);
            var subject = $"[TEST] Rückgabe bestätigt - {data.TicketNumber}";
            var body = BuildReturnConfirmedEmail(data, cfg, req.Comment);

            await email.SendAsync(req.To.Trim(), subject, body);

            return Results.Ok(new
            {
                ok = true,
                template = "return-confirmed",
                sentTo = req.To.Trim(),
                returnRequestId = req.ReturnRequestId,
                ticket = data.TicketNumber
            });
        })
        .WithName("SendTestReturnConfirmedEmail")
        .WithSummary("Sendet eine Testmail für eine bestätigte Rückgabe.");

        group.MapPost("/all", async (
            [FromBody] EmailTemplateTestRequest req,
            IDbContextFactory<AppDbContext> dbFactory,
            IEmailService email,
            IConfiguration cfg,
            CancellationToken ct) =>
        {
            var validation = Validate(req);
            if (validation is not null)
                return validation;

            var to = req.To.Trim();
            var order = await LoadOrderOrSampleAsync(req.OrderId, dbFactory, ct);
            var returnData = await LoadReturnOrSampleAsync(req.ReturnRequestId, dbFactory, ct);

            var messages = new List<(string Template, string Subject, string Body)>
            {
                ("order-approval-request", $"[TEST] [{order.DisplayTicketNumber}] Equipment Bestellung - Freigabe benötigt", BuildOrderApprovalRequestEmail(order, cfg, req.Comment)),
                ("order-approved-it", $"[TEST] [GENEHMIGT] [{order.DisplayTicketNumber}] Equipment Bestellung vorbereiten", BuildOrderApprovedItEmail(order, cfg, req.Comment)),
                ("order-denied", $"[TEST] [{order.DisplayTicketNumber}] Equipment Bestellung - abgelehnt", BuildOrderDeniedEmail(order, cfg, req.Comment)),
                ("return-requested", $"[TEST] Rückgabe angemeldet - {returnData.TicketNumber}", BuildReturnRequestedEmail(returnData, cfg, req.Comment)),
                ("return-confirmed", $"[TEST] Rückgabe bestätigt - {returnData.TicketNumber}", BuildReturnConfirmedEmail(returnData, cfg, req.Comment))
            };

            foreach (var message in messages)
            {
                await email.SendAsync(to, message.Subject, message.Body);
            }

            return Results.Ok(new
            {
                ok = true,
                sentTo = to,
                sent = messages.Select(x => x.Template).ToArray(),
                orderId = req.OrderId,
                ticket = order.DisplayTicketNumber,
                returnRequestId = req.ReturnRequestId
            });
        })
        .WithName("SendAllTestEmails")
        .WithSummary("Sendet alle Testmail-Vorlagen an einen frei wählbaren Empfänger.");
    }

    private static IResult? Validate(EmailTemplateTestRequest req)
    {
        if (req is null)
            return Results.BadRequest(new { error = "Request body fehlt." });

        if (string.IsNullOrWhiteSpace(req.To))
            return Results.BadRequest(new { error = "Feld 'to' fehlt." });

        if (!req.To.Contains('@'))
            return Results.BadRequest(new { error = "Feld 'to' sieht nicht wie eine E-Mail-Adresse aus." });

        return null;
    }

    private static async Task<EquipmentOrder> LoadOrderOrSampleAsync(
        int? orderId,
        IDbContextFactory<AppDbContext> dbFactory,
        CancellationToken ct)
    {
        if (orderId.GetValueOrDefault() > 0)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            var order = await db.EquipmentOrders
                .AsNoTracking()
                .Include(x => x.Lines)
                .FirstOrDefaultAsync(x => x.Id == orderId!.Value, ct);

            if (order is not null)
                return order;
        }

        return BuildSampleOrder();
    }

    private static async Task<ReturnEmailData> LoadReturnOrSampleAsync(
        int? returnRequestId,
        IDbContextFactory<AppDbContext> dbFactory,
        CancellationToken ct)
    {
        if (returnRequestId.GetValueOrDefault() > 0)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            var rr = await db.ReturnRequests
                .AsNoTracking()
                .Include(x => x.Position)
                .ThenInclude(x => x!.Category)
                .FirstOrDefaultAsync(x => x.Id == returnRequestId!.Value, ct);

            if (rr is not null)
            {
                return new ReturnEmailData(
                    ReturnRequestId: rr.Id,
                    TicketNumber: $"RET-{rr.Id}",
                    RequesterName: BlankToDash(rr.RequesterName),
                    RequesterEmail: BlankToDash(rr.RequesterEmail),
                    ItemName: BlankToDash(rr.Position?.Description),
                    CategoryName: BlankToDash(rr.Position?.Category?.Name),
                    PositionId: rr.PositionId,
                    OrderNumber: BlankToDash(rr.Position?.OrderNumber),
                    UserComment: BlankToDash(rr.UserComment),
                    AdminComment: BlankToDash(rr.AdminComment),
                    RequestedAt: rr.RequestedAt,
                    ConfirmedAt: rr.ConfirmedAt);
            }
        }

        return BuildSampleReturnData();
    }

    private static EquipmentOrder BuildSampleOrder()
    {
        return new EquipmentOrder
        {
            Id = 999,
            TicketNumber = "WEB-O-999",
            OrderedByName = "Max Muster",
            OrderedByEmail = "max.muster@bosch.com",
            RequestedForName = "Erika Beispiel",
            RequestedForEmail = "erika.beispiel@bosch.com",
            PickupContactName = "Max Muster",
            PickupContactEmail = "max.muster@bosch.com",
            SupervisorName = "Vera Vorgesetzt",
            SupervisorEmail = "vera.vorgesetzt@bosch.com",
            Reason = "Testbestellung für E-Mail-Vorschau.",
            BossComment = "Test-Kommentar der Führungskraft.",
            Status = OrderStatus.PendingApproval,
            CreatedAt = DateTime.Now,
            DecisionDate = DateTime.Now,
            Lines = new List<EquipmentOrderLine>
            {
                new()
                {
                    Id = 1,
                    ActionCode = "016",
                    CategoryName = "IT Zubehör",
                    Manufacturer = "diverse",
                    ItemName = "USB-Maus, wireless",
                    Quantity = 1,
                    Currency = "CHF",
                    UnitPrice = 24m,
                    BillingType = "einmalig",
                    EffectiveFulfillmentMode = EffectiveFulfillmentMode.UseStorage,
                    Returnable = true,
                    UserComment = "Bitte wenn möglich neu."
                },
                new()
                {
                    Id = 2,
                    ActionCode = "085",
                    CategoryName = "Mobile Zubehör",
                    Manufacturer = "diverse",
                    ItemName = "iPhone Ladekabel",
                    Quantity = 2,
                    Currency = "CHF",
                    UnitPrice = 12m,
                    BillingType = "einmalig",
                    EffectiveFulfillmentMode = EffectiveFulfillmentMode.ManualNoStorage,
                    Returnable = false,
                    UserComment = " "
                },
                new()
                {
                    Id = 3,
                    ActionCode = "050",
                    CategoryName = "Telefonie",
                    Manufacturer = "Swisscom",
                    ItemName = "NATEL® go flex Neighbours Swiss voice 1GB (5.2)",
                    Quantity = 1,
                    Currency = "CHF",
                    UnitPrice = 0m,
                    BillingType = "monatlich",
                    EffectiveFulfillmentMode = EffectiveFulfillmentMode.ServiceChange,
                    Returnable = false,
                    UserComment = "Mobile Nummer wird nachgereicht."
                }
            }
        };
    }

    private static ReturnEmailData BuildSampleReturnData()
    {
        return new ReturnEmailData(
            ReturnRequestId: 999,
            TicketNumber: "WEB-O-999",
            RequesterName: "Erika Beispiel",
            RequesterEmail: "erika.beispiel@bosch.com",
            ItemName: "USB-Maus, wireless",
            CategoryName: "Maus wireless",
            PositionId: 1105,
            OrderNumber: "ORD-TEST-123",
            UserComment: "Wird nicht mehr benötigt.",
            AdminComment: "Rückgabe geprüft und bestätigt.",
            RequestedAt: DateTime.Now.AddHours(-2),
            ConfirmedAt: DateTime.Now);
    }

    private static string BuildOrderApprovalRequestEmail(EquipmentOrder order, IConfiguration cfg, string? comment)
    {
        var approveUrl = "#test-approve-disabled";
        var denyUrl = "#test-deny-disabled";

        return WrapMail(
            title: "Equipment Bestellung zur Freigabe",
            color: "#005691",
            content: $@"
<p><strong>{H(order.OrderedByName)}</strong> hat eine Equipment-Bestellung zur Freigabe eingereicht.</p>
{BuildTestNotice(comment)}
{BuildOrderHeaderTable(order)}
{BuildLinesTable(order)}
{BuildCostSummary(order)}
<p style='margin-top:24px;'>Bitte wähle eine Option:</p>
<a href='{approveUrl}' style='display:inline-block;background:#18837e;color:white;padding:12px 28px;border-radius:6px;text-decoration:none;font-weight:bold;margin-right:12px;'>Genehmigen</a>
<a href='{denyUrl}' style='display:inline-block;background:#EA0016;color:white;padding:12px 28px;border-radius:6px;text-decoration:none;font-weight:bold;'>Ablehnen</a>
<p style='margin-top:16px;color:#64748b;font-size:12px;'>Testmail: Diese Buttons lösen keine echte Aktion aus.</p>");
    }

    private static string BuildOrderApprovedItEmail(EquipmentOrder order, IConfiguration cfg, string? comment)
    {
        var baseUrl = GetBaseUrl(cfg);

        return WrapMail(
            title: "Equipment Bestellung vorbereiten",
            color: "#18837e",
            content: $@"
<p>Eine Bestellung wurde genehmigt. Bitte die Positionen einzeln vorbereiten.</p>
{BuildTestNotice(comment)}
{BuildOrderHeaderTable(order)}
{BuildLinesTable(order)}
<p style='margin-top:16px;'>Admin-Panel: <a href='{baseUrl}/admin'>{baseUrl}/admin</a></p>");
    }

    private static string BuildOrderDeniedEmail(EquipmentOrder order, IConfiguration cfg, string? comment)
    {
        var bossComment = string.IsNullOrWhiteSpace(comment)
            ? BlankToDash(order.BossComment)
            : comment!.Trim();

        return WrapMail(
            title: "Equipment Bestellung abgelehnt",
            color: "#EA0016",
            content: $@"
<p>Hallo {H(order.PickupContactName)},</p>
<p>Die Bestellung <strong>{H(order.DisplayTicketNumber)}</strong> wurde abgelehnt.</p>
{BuildTestNotice(comment)}
{BuildOrderHeaderTable(order)}
{BuildLinesTable(order)}
<p><strong>Kommentar:</strong> {H(bossComment)}</p>");
    }

    private static string BuildReturnRequestedEmail(ReturnEmailData data, IConfiguration cfg, string? comment)
    {
        var baseUrl = GetBaseUrl(cfg);

        return WrapMail(
            title: "Rückgabe angemeldet",
            color: "#005691",
            content: $@"
<p>Eine Rückgabe wurde angemeldet und muss im Adminbereich geprüft werden.</p>
{BuildTestNotice(comment)}
{BuildReturnTable(data)}
<p style='margin-top:16px;'>Admin-Panel: <a href='{baseUrl}/admin'>{baseUrl}/admin</a></p>");
    }

    private static string BuildReturnConfirmedEmail(ReturnEmailData data, IConfiguration cfg, string? comment)
    {
        return WrapMail(
            title: "Rückgabe bestätigt",
            color: "#18837e",
            content: $@"
<p>Hallo {H(data.RequesterName)},</p>
<p>Deine Rückgabe wurde bestätigt. Danke.</p>
{BuildTestNotice(comment)}
{BuildReturnTable(data)}");
    }

    private static string WrapMail(string title, string color, string content)
    {
        return $@"
<div style='font-family:Arial,sans-serif;max-width:780px;color:#1f2937;'>
  <div style='background:{color};color:white;padding:18px 24px;border-radius:10px 10px 0 0;'>
    <div style='font-size:12px;letter-spacing:.08em;text-transform:uppercase;opacity:.85;'>Lagerverwaltung</div>
    <h2 style='margin:4px 0 0 0;font-size:22px;'>{H(title)}</h2>
  </div>
  <div style='border:1px solid #d9e2ec;border-top:0;padding:24px;border-radius:0 0 10px 10px;background:#ffffff;'>
    <div style='background:#fff7ed;border:1px solid #fed7aa;color:#9a3412;border-radius:8px;padding:12px 14px;margin-bottom:16px;'>
      <strong>TESTMAIL:</strong> Diese E-Mail wurde über Swagger erzeugt. Es wurde keine echte Freigabe, Ablehnung oder Rückgabe ausgelöst.
    </div>
    {content}
  </div>
</div>";
    }

    private static string BuildTestNotice(string? comment)
    {
        if (string.IsNullOrWhiteSpace(comment))
            return "";

        return $"<p style='background:#f8fafc;border:1px solid #e5e7eb;border-radius:8px;padding:10px 12px;'><strong>Test-Kommentar:</strong> {H(comment)}</p>";
    }

    private static string BuildOrderHeaderTable(EquipmentOrder order)
    {
        return $@"
<table style='width:100%;border-collapse:collapse;margin:16px 0;border:1px solid #e5e7eb;'>
  <tr><td style='padding:9px;background:#f8fafc;font-weight:bold;width:190px;'>Ticket-Nr.</td><td style='padding:9px;'>{H(order.DisplayTicketNumber)}</td></tr>
  <tr><td style='padding:9px;background:#f8fafc;font-weight:bold;'>Bestellt von</td><td style='padding:9px;'>{H(order.OrderedByName)} ({H(order.OrderedByEmail)})</td></tr>
  <tr><td style='padding:9px;background:#f8fafc;font-weight:bold;'>Für</td><td style='padding:9px;'>{H(order.RequestedForName)} ({H(order.RequestedForEmail)})</td></tr>
  <tr><td style='padding:9px;background:#f8fafc;font-weight:bold;'>Kontakt</td><td style='padding:9px;'>{H(order.PickupContactName)} ({H(order.PickupContactEmail)})</td></tr>
  <tr><td style='padding:9px;background:#f8fafc;font-weight:bold;'>Vorgesetzte/r</td><td style='padding:9px;'>{H(order.SupervisorName)} ({H(order.SupervisorEmail)})</td></tr>
  <tr><td style='padding:9px;background:#f8fafc;font-weight:bold;'>Begründung</td><td style='padding:9px;'>{H(order.Reason)}</td></tr>
  <tr><td style='padding:9px;background:#f8fafc;font-weight:bold;'>Datum</td><td style='padding:9px;'>{order.CreatedAt:dd.MM.yyyy HH:mm}</td></tr>
</table>";
    }

    private static string BuildLinesTable(EquipmentOrder order)
    {
        var rows = string.Join(Environment.NewLine, order.Lines
            .OrderBy(x => x.Id)
            .Select(line => $@"
<tr>
  <td style='padding:9px;border-bottom:1px solid #eee;'>{H(line.ActionCode)}</td>
  <td style='padding:9px;border-bottom:1px solid #eee;'>{H(line.CategoryName)}</td>
  <td style='padding:9px;border-bottom:1px solid #eee;'><strong>{H(line.ItemName)}</strong><br><span style='color:#64748b;font-size:12px;'>{H(line.Manufacturer)}</span></td>
  <td style='padding:9px;border-bottom:1px solid #eee;text-align:center;'>{line.Quantity}</td>
  <td style='padding:9px;border-bottom:1px solid #eee;text-align:right;'>{H(line.Currency)} {line.LinePrice:0.00}</td>
  <td style='padding:9px;border-bottom:1px solid #eee;'>{H(line.BillingType)}</td>
  <td style='padding:9px;border-bottom:1px solid #eee;'>{H(GetFulfillmentLabel(line.EffectiveFulfillmentMode))}</td>
  <td style='padding:9px;border-bottom:1px solid #eee;'>{H(line.UserComment == " " ? "" : line.UserComment)}</td>
</tr>"));

        return $@"
<table style='width:100%;border-collapse:collapse;margin:16px 0;border:1px solid #e5e7eb;'>
  <thead>
    <tr>
      <th style='padding:9px;background:#f8fafc;text-align:left;'>Code</th>
      <th style='padding:9px;background:#f8fafc;text-align:left;'>Kategorie</th>
      <th style='padding:9px;background:#f8fafc;text-align:left;'>Artikel</th>
      <th style='padding:9px;background:#f8fafc;text-align:center;'>Menge</th>
      <th style='padding:9px;background:#f8fafc;text-align:right;'>Preis</th>
      <th style='padding:9px;background:#f8fafc;text-align:left;'>Abrechnung</th>
      <th style='padding:9px;background:#f8fafc;text-align:left;'>Fulfillment</th>
      <th style='padding:9px;background:#f8fafc;text-align:left;'>Notiz</th>
    </tr>
  </thead>
  <tbody>{rows}</tbody>
</table>";
    }

    private static string BuildCostSummary(EquipmentOrder order)
    {
        var oneTime = order.Lines.Where(x => IsOneTime(x.BillingType)).Sum(x => x.LinePrice);
        var monthly = order.Lines.Where(x => IsMonthly(x.BillingType)).Sum(x => x.LinePrice);
        var noCostLines = order.Lines.Count(x => IsNoCost(x.BillingType));
        var currency = order.Lines.FirstOrDefault()?.Currency ?? "CHF";

        return $@"
<div style='background:#f8fafc;border:1px solid #e5e7eb;border-radius:8px;padding:12px 16px;margin:16px 0;'>
  <p style='margin:4px 0;'><strong>Einmalige Kosten:</strong> {H(currency)} {oneTime:0.00}</p>
  <p style='margin:4px 0;'><strong>Monatliche Kosten:</strong> {H(currency)} {monthly:0.00} / Monat</p>
  <p style='margin:4px 0;'><strong>Ohne Kosten/ohne Abrechnung:</strong> {noCostLines} Position(en)</p>
</div>";
    }

    private static string BuildReturnTable(ReturnEmailData data)
    {
        return $@"
<table style='width:100%;border-collapse:collapse;margin:16px 0;border:1px solid #e5e7eb;'>
  <tr><td style='padding:9px;background:#f8fafc;font-weight:bold;width:190px;'>Ticket</td><td style='padding:9px;'>{H(data.TicketNumber)}</td></tr>
  <tr><td style='padding:9px;background:#f8fafc;font-weight:bold;'>Rückgabe-ID</td><td style='padding:9px;'>{data.ReturnRequestId}</td></tr>
  <tr><td style='padding:9px;background:#f8fafc;font-weight:bold;'>Artikel</td><td style='padding:9px;'>{H(data.ItemName)}</td></tr>
  <tr><td style='padding:9px;background:#f8fafc;font-weight:bold;'>Kategorie</td><td style='padding:9px;'>{H(data.CategoryName)}</td></tr>
  <tr><td style='padding:9px;background:#f8fafc;font-weight:bold;'>Position</td><td style='padding:9px;'>#{data.PositionId}</td></tr>
  <tr><td style='padding:9px;background:#f8fafc;font-weight:bold;'>Bestellnummer</td><td style='padding:9px;'>{H(data.OrderNumber)}</td></tr>
  <tr><td style='padding:9px;background:#f8fafc;font-weight:bold;'>Angefragt von</td><td style='padding:9px;'>{H(data.RequesterName)} ({H(data.RequesterEmail)})</td></tr>
  <tr><td style='padding:9px;background:#f8fafc;font-weight:bold;'>Angefragt am</td><td style='padding:9px;'>{data.RequestedAt:dd.MM.yyyy HH:mm}</td></tr>
  <tr><td style='padding:9px;background:#f8fafc;font-weight:bold;'>Bestätigt am</td><td style='padding:9px;'>{(data.ConfirmedAt.HasValue ? data.ConfirmedAt.Value.ToString("dd.MM.yyyy HH:mm") : "-")}</td></tr>
  <tr><td style='padding:9px;background:#f8fafc;font-weight:bold;'>User-Kommentar</td><td style='padding:9px;'>{H(data.UserComment)}</td></tr>
  <tr><td style='padding:9px;background:#f8fafc;font-weight:bold;'>Admin-Kommentar</td><td style='padding:9px;'>{H(data.AdminComment)}</td></tr>
</table>";
    }

    private static bool IsMonthly(string? billingType)
        => (billingType ?? "").Contains("monat", StringComparison.OrdinalIgnoreCase);

    private static bool IsOneTime(string? billingType)
        => (billingType ?? "").Contains("einmal", StringComparison.OrdinalIgnoreCase);

    private static bool IsNoCost(string? billingType)
    {
        var normalized = (billingType ?? "").Trim();
        return string.IsNullOrWhiteSpace(normalized)
               || normalized.Equals("ohne", StringComparison.OrdinalIgnoreCase)
               || (!IsMonthly(normalized) && !IsOneTime(normalized));
    }

    private static string GetFulfillmentLabel(EffectiveFulfillmentMode mode)
    {
        return mode switch
        {
            EffectiveFulfillmentMode.UseStorage => "Lagerposition durch IT zuweisen",
            EffectiveFulfillmentMode.ManualNoStorage => "Manuell ohne Lager",
            EffectiveFulfillmentMode.ExternalOrder => "Externe Bestellung",
            EffectiveFulfillmentMode.ServiceChange => "Service / Mutation",
            EffectiveFulfillmentMode.ReturnAction => "Rückgabe / Kündigung",
            _ => mode.ToString()
        };
    }

    private static string GetBaseUrl(IConfiguration cfg)
    {
        return (cfg["App:BaseUrl"] ?? "https://your-server").TrimEnd('/');
    }

    private static string BlankToDash(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) || trimmed == " " ? "-" : trimmed;
    }

    private static string H(string? value)
    {
        return WebUtility.HtmlEncode(value?.Trim() ?? "");
    }

    public sealed class EmailTemplateTestRequest
    {
        public string To { get; set; } = "";
        public int? OrderId { get; set; }
        public int? ReturnRequestId { get; set; }
        public string? Comment { get; set; }
    }

    private sealed record ReturnEmailData(
        int ReturnRequestId,
        string TicketNumber,
        string RequesterName,
        string RequesterEmail,
        string ItemName,
        string CategoryName,
        int PositionId,
        string OrderNumber,
        string UserComment,
        string AdminComment,
        DateTime RequestedAt,
        DateTime? ConfirmedAt);
}
