using System.Net;
using System.Text;
using Lagerverwaltung.Web.Models;
using Lagerverwaltung.Web.Services;

namespace Lagerverwaltung.Web.Api
{
    public static class ApiEndpoints
    {
        public static void MapApiEndpoints(this WebApplication app)
        {
            MapEmailEndpoints(app);
            EmailTemplateTestEndpoints.MapEmailTemplateTestEndpoints(app);
            MapRequestEndpoints(app);
            MapOrderEndpoints(app);
            MapApproverImportEndpoints(app);
        }

        private static void MapEmailEndpoints(WebApplication app)
        {
            var group = app
                .MapGroup("/api/email")
                .RequireAuthorization("AdminOnly");

            group.MapPost("/send", async (
                SendEmailRequest req,
                IEmailService email,
                ILogger<Program> log) =>
            {
                if (string.IsNullOrWhiteSpace(req.To) ||
                    string.IsNullOrWhiteSpace(req.Subject) ||
                    string.IsNullOrWhiteSpace(req.HtmlBody))
                {
                    return Results.BadRequest(new { error = "Missing fields" });
                }

                try
                {
                    await email.SendAsync(req.To, req.Subject, req.HtmlBody);
                    return Results.Ok();
                }
                catch (Exception ex)
                {
                    log.LogError(ex, "Email failed");
                    return Results.Problem("Email failed");
                }
            });
        }

        private static void MapRequestEndpoints(WebApplication app)
        {
            app.MapGet("/api/requests/{id:int}/approve/{token}", async (
                int id,
                string token,
                IEquipmentService svc) =>
            {

                var result = await svc.ApproveByTokenAsync(id, token);

                if (!result.ok)
                {
                    var color = result.error == "Already handled" ? "#005691" : "#EA0016";
                    return Results.Content(Html(result.error, color), "text/html");
                }

                return Results.Content(Html("Approved ✅", "#4caf50"), "text/html");
            });

            app.MapGet("/api/requests/{id:int}/deny/{token}", async (
                int id,
                string token,
                string? comment,
                IEquipmentService svc) =>
            {

                var result = await svc.DenyByTokenAsync(id, token, comment ?? "");

                if (!result.ok)
                {
                    var color = result.error == "Already handled" ? "#005691" : "#EA0016";
                    return Results.Content(Html(result.error, color), "text/html");
                }

                return Results.Content(Html("Denied ❌", "#EA0016"), "text/html");
            });


        }
        private static void MapOrderEndpoints(WebApplication app)
        {
            app.MapGet("/api/orders/{id:int}/approve/{token}", async (
                int id,
                string token,
                IEquipmentOrderService svc) =>
            {
                var result = await svc.ApproveByTokenAsync(id, token);

                if (!result.ok)
                {
                    var message = FriendlyTokenMessage(result.error);
                    var color = IsInvalidTokenMessage(result.error) ? "#EA0016" : "#005691";

                    return Results.Content(Html(message, color), "text/html");
                }

                if (!string.IsNullOrWhiteSpace(result.error))
                    return Results.Content(Html(result.error, "#005691"), "text/html");

                return Results.Content(Html("Order approved ✅", "#4caf50"), "text/html");
            });

            app.MapGet("/api/orders/{id:int}/deny/{token}", async (
                int id,
                string token,
                string? comment,
                IEquipmentOrderService svc) =>
            {
                var result = await svc.DenyByTokenAsync(id, token, comment ?? "");

                if (!result.ok)
                {
                    var message = FriendlyTokenMessage(result.error);
                    var color = IsInvalidTokenMessage(result.error) ? "#EA0016" : "#005691";

                    return Results.Content(Html(message, color), "text/html");
                }

                if (!string.IsNullOrWhiteSpace(result.error))
                    return Results.Content(Html(result.error, "#005691"), "text/html");

                return Results.Content(Html("Order denied ❌", "#EA0016"), "text/html");
            });
        }

        private static void MapApproverImportEndpoints(WebApplication app)
        {
            var group = app
                .MapGroup("/api/admin/approvers")
                .RequireAuthorization("AdminOnly");

            group.MapPost("/folder-import/run", async (
                IApproverFolderImportRunner runner,
                CancellationToken token) =>
            {
                var result = await runner.RunOnceAsync(token);
                return Results.Ok(result);
            });

            group.MapPost("/import", async (
                IFormFile file,
                IApproverCsvReader csvReader,
                IApproverService approvers,
                CancellationToken token) =>
            {
                if (file is null || file.Length == 0)
                    return Results.BadRequest(new { error = "CSV-Datei fehlt oder ist leer." });

                await using var stream = file.OpenReadStream();

                var rows = await csvReader.ReadAsync(stream, token);

                if (rows.Count == 0)
                    return Results.BadRequest(new { error = "Keine gültigen Approver-Zeilen gefunden." });

                var result = await approvers.ReplaceFromImportAsync(rows);

                return Results.Ok(new
                {
                    importedRows = rows.Count,
                    result.Added,
                    result.Updated,
                    result.Deactivated
                });
            })
                .DisableAntiforgery();
        }

        private static string Html(string title, string color)
        {
            var safeTitle = WebUtility.HtmlEncode(title);

            return $@"
<!DOCTYPE html>
<html>
<head>
<meta charset='utf-8'/>
<style>
body {{ font-family: Arial; display:flex; justify-content:center; align-items:center; height:100vh; background:#f5f5f5; }}
.card {{ background:white; padding:30px; border-radius:8px; box-shadow:0 2px 10px rgba(0,0,0,.1); }}
h1 {{ color:{color}; }}
</style>
</head>
<body>
<div class='card'>
<h1>{safeTitle}</h1>
</div>
</body>
</html>";
        }

        public class SendEmailRequest
        {
            public string To { get; set; } = "";
            public string Subject { get; set; } = "";
            public string HtmlBody { get; set; } = "";
        }


        private static int FindHeaderIndex(List<string> headers, params string[] acceptedNames)
        {
            for (var i = 0; i < headers.Count; i++)
            {
                foreach (var acceptedName in acceptedNames)
                {
                    if (string.Equals(headers[i], acceptedName, StringComparison.OrdinalIgnoreCase))
                        return i;
                }
            }

            return -1;
        }

        private static bool IsInvalidTokenMessage(string message)
        {
            return string.Equals(message, "Invalid link", StringComparison.OrdinalIgnoreCase);
        }

        private static string FriendlyTokenMessage(string message)
        {
            return message switch
            {
                "Invalid link" =>
                    "Dieser Link ist ungültig oder abgelaufen.",

                "Already handled" =>
                    "Diese Bestellung wurde bereits bearbeitet. Es ist keine weitere Aktion notwendig.",

                "Approved, but IT email failed" =>
                    "Die Bestellung wurde genehmigt, aber die IT-E-Mail konnte nicht gesendet werden. Bitte IT manuell informieren.",

                "Denied, but user email failed" =>
                    "Die Bestellung wurde abgelehnt, aber die Benutzer-E-Mail konnte nicht gesendet werden.",

                _ => message
            };
        }

        private static List<string> ParseCsvLine(string line)
        {
            var result = new List<string>();
            var current = new StringBuilder();
            var inQuotes = false;

            for (var i = 0; i < line.Length; i++)
            {
                var c = line[i];

                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == ',' && !inQuotes)
                {
                    result.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }

            result.Add(current.ToString());

            return result;
        }
    }
}
