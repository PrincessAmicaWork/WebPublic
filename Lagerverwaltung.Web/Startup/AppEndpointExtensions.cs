using Lagerverwaltung.Web.Components;
using Lagerverwaltung.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;

namespace Lagerverwaltung.Web.Startup;

public static class AppEndpointExtensions
{
    public static WebApplication MapAuthenticationEndpoints(this WebApplication app)
    {
        app.MapGet("/login", (string? returnUrl) =>
        {
            if (string.IsNullOrWhiteSpace(returnUrl) ||
                !returnUrl.StartsWith("/", StringComparison.Ordinal) ||
                returnUrl.StartsWith("//", StringComparison.Ordinal) ||
                returnUrl.Contains("://", StringComparison.Ordinal))
            {
                returnUrl = "/";
            }

            return Results.Challenge(
                new AuthenticationProperties { RedirectUri = returnUrl },
                new[] { OpenIdConnectDefaults.AuthenticationScheme });
        });

        app.MapGet("/logout", async (HttpContext context) =>
        {
            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            await context.SignOutAsync(
                OpenIdConnectDefaults.AuthenticationScheme,
                new AuthenticationProperties
                {
                    RedirectUri = "/"
                });

            return Results.Empty;
        });

        return app;
    }

    public static WebApplication MapAppUi(this WebApplication app)
    {
        app.MapRazorPages();

        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode()
            .RequireAuthorization();

        return app;
    }

    public static WebApplication MapDiagnosticEndpoints(this WebApplication app)
    {
        var diag = app.MapGroup("/diag")
            .RequireAuthorization("AdminOnly");

        diag.MapGet("/whoami", (HttpContext ctx) =>
            Results.Text(ctx.User?.Identity?.Name ?? "(anonymous)", "text/plain"));

        diag.MapGet("/require", () =>
            Results.Ok("ok - admin authorized"));

        diag.MapGet("/roles", (HttpContext ctx) =>
        {
            var name = ctx.User?.Identity?.Name ?? "(anonymous)";

            var roles = ctx.User?.Claims
                .Where(c => c.Type == "roles" || c.Type == ClaimTypes.Role)
                .Select(c => $"{c.Type} = {c.Value}")
                .ToList() ?? new List<string>();

            var text = $"User: {name}\nRoles:\n" +
                       (roles.Count == 0 ? "  (none)" : "  " + string.Join("\n  ", roles));

            return Results.Text(text, "text/plain");
        });
        diag.MapGet("/send-test-email", async (
            IConfiguration cfg,
            IEmailService email,
            ILogger<Program> log) =>
        {
            var to =
                cfg["Email:ItEmail"]?.Trim()
                ?? cfg["Email:SmtpSender"]?.Trim()
                ?? "";

            if (string.IsNullOrWhiteSpace(to))
                return Results.BadRequest(new { error = "No test recipient configured." });

            try
            {
                await email.SendAsync(
                    to,
                    $"Lagerverwaltung SMTP test {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                    "<p>This is a Lagerverwaltung SMTP diagnostic email.</p>");

                return Results.Ok(new
                {
                    ok = true,
                    sentTo = to,
                    message = "SMTP accepted the message."
                });
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Diagnostic test email failed");

                return Results.Problem(
                    detail: $"{ex.GetType().Name}: {ex.Message}",
                    title: "Diagnostic test email failed");
            }
        });

        diag.MapGet("/send-test-email-to", async (
            string to,
            IEmailService email,
            ILogger<Program> log) =>
        {
            to = to?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(to))
                return Results.BadRequest(new { error = "Missing ?to=email@domain" });

            try
            {
                await email.SendAsync(
                    to,
                    $"Lagerverwaltung supervisor SMTP test {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                    $"""
            <p>This is a Lagerverwaltung diagnostic test email.</p>
            <p>Recipient: <strong>{System.Net.WebUtility.HtmlEncode(to)}</strong></p>
            """);

                return Results.Ok(new
                {
                    ok = true,
                    sentTo = to,
                    message = "SMTP accepted the message."
                });
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Diagnostic test email to {To} failed", to);

                return Results.Problem(
                    detail: $"{ex.GetType().Name}: {ex.Message}",
                    title: "Diagnostic test email failed");
            }
        });
        diag.MapGet("/email-config", (IConfiguration cfg, IWebHostEnvironment env) =>
        {
            static string State(string? value) =>
                string.IsNullOrWhiteSpace(value) ? "MISSING" : "SET";

            return Results.Ok(new
            {
                Environment = env.EnvironmentName,
                SmtpHost = cfg["Email:SmtpHost"],
                SmtpPort = cfg["Email:SmtpPort"],
                SmtpSender = cfg["Email:SmtpSender"],
                SmtpUsername = State(cfg["Email:SmtpUsername"]),
                SmtpPassword = State(cfg["Email:SmtpPassword"]),
                OracleConnectionString = State(cfg.GetConnectionString("Oracle"))
            });
        });

        return app;
    }

    public static WebApplication MapTestAuthEndpoint(this WebApplication app)
    {
        app.MapGet("/test-auth", async (HttpContext context) =>
        {
            var config = context.RequestServices.GetRequiredService<IConfiguration>();
            var tenantId = config["AzureAd:TenantId"];
            var clientId = config["AzureAd:ClientId"];
            var clientSecret = config["AzureAd:ClientSecret"];

            await Task.CompletedTask;

            return Results.Text(
                $"Tenant: {tenantId}\n" +
                $"Client: {clientId}\n" +
                $"Secret Set: {!string.IsNullOrEmpty(clientSecret)}",
                "text/plain");
        })
        .RequireAuthorization("AdminOnly");

        return app;
    }
}
