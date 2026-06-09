using Lagerverwaltung.Web.Api;
using Lagerverwaltung.Web.Startup;
using Serilog;

// ============================================================================
// LOGGING
// ============================================================================

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.AddAppConfiguration();

    builder.Host.UseSerilog((ctx, cfg) =>
        cfg.ReadFrom.Configuration(ctx.Configuration));

    builder.UseCorporateProxyDefaults();

    builder.Services
        .AddAppAuthentication(builder.Configuration)
        .AddAppAuthorizationPolicies()
        .AddAppUi(builder.Environment)
        .AddAppDatabase(builder.Configuration)
        .AddAppApplicationServices(builder.Configuration)
        .AddAppSwagger()
        .AddAppHttpsRedirection();

    var app = builder.Build();

    app.MapAuthenticationEndpoints();

    app.UseAppPipeline();
    app.UseAdminOnlySwaggerGate();
    app.UseAppSwagger();

    app.MapApiEndpoints();
    app.MapAppUi();
    app.MapDiagnosticEndpoints();
    app.MapTestAuthEndpoint();

    app.UseStatusCodeLogging();
    app.LogStartup();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application startup failed");
}
finally
{
    Log.CloseAndFlush();
}
