using System.Security.Claims;
using Lagerverwaltung.Web.Data;
using Lagerverwaltung.Web.Options;
using Lagerverwaltung.Web.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Web;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using MudBlazor;
using MudBlazor.Services;

namespace Lagerverwaltung.Web.Startup;

public static class AppServiceCollectionExtensions
{
    public static IServiceCollection AddAppAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
            .AddMicrosoftIdentityWebApp(configuration.GetSection("AzureAd"))
            .EnableTokenAcquisitionToCallDownstreamApi()
            .AddInMemoryTokenCaches();

        services.Configure<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme, options =>
        {
            options.ResponseType = OpenIdConnectResponseType.Code;
            options.UsePkce = true;
            options.SaveTokens = true;

            options.MapInboundClaims = false;

            // These claims are used for names and roles.
            options.TokenValidationParameters.NameClaimType = "name";
            options.TokenValidationParameters.RoleClaimType = ClaimTypes.Role;

            options.Events = new OpenIdConnectEvents
            {
                OnAuthenticationFailed = context =>
                {
                    var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                    logger.LogError(context.Exception, "Authentication failed");
                    return Task.CompletedTask;
                },
                OnRemoteFailure = context =>
                {
                    var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                    logger.LogError(context.Failure, "Remote failure during authentication");
                    return Task.CompletedTask;
                }
            };
        });

        services.Configure<CookiePolicyOptions>(options =>
        {
            options.MinimumSameSitePolicy = SameSiteMode.None;
            options.Secure = CookieSecurePolicy.Always;
        });

        services.Configure<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme, options =>
        {
            options.Cookie.SameSite = SameSiteMode.None;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.ExpireTimeSpan = TimeSpan.FromMinutes(60);
            options.SlidingExpiration = true;
        });

        return services;
    }

    public static IServiceCollection AddAppAuthorizationPolicies(this IServiceCollection services)
    {
        var authz = services.AddAuthorizationBuilder();

        authz.AddPolicy("Users", policy =>
            policy.RequireAuthenticatedUser());

        authz.AddPolicy("SupervisorOrAdmin", policy =>
            policy.RequireRole(
                "Lagerverwaltung.Supervisor",
                "Lagerverwaltung.Admin"));

        authz.AddPolicy("AdminOnly", policy =>
            policy.RequireRole("Lagerverwaltung.Admin"));

        return services;
    }

    public static IServiceCollection AddAppUi(
        this IServiceCollection services,
        IWebHostEnvironment environment)
    {
        services.AddRazorPages();

        services.AddRazorComponents()
            .AddInteractiveServerComponents();

        services.AddCascadingAuthenticationState();

        services.AddServerSideBlazor(options =>
        {
            options.DetailedErrors = environment.IsDevelopment();
        });

        services.AddMudServices(config =>
        {
            config.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.BottomRight;
            config.SnackbarConfiguration.PreventDuplicates = false;
            config.SnackbarConfiguration.ShowCloseIcon = true;
            config.SnackbarConfiguration.VisibleStateDuration = 4000;
        });

        return services;
    }

    public static IServiceCollection AddAppDatabase(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContextFactory<AppDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString("Oracle");

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "Oracle connection string not found. Set ConnectionStrings:Oracle.");
            }

            options.UseOracle(
                connectionString,
                oracle => oracle.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19));
        });

        return services;
    }

    public static IServiceCollection AddAppApplicationServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<ILowStockNotificationService, LowStockNotificationService>();
        services.AddScoped<IReturnService, ReturnService>();
        services.AddScoped<IEquipmentService, EquipmentService>();
        services.AddScoped<IStorageService, StorageService>();
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<ICurrentSiteService, CurrentSiteService>();
        services.AddScoped<IApproverService, ApproverService>();
        services.AddScoped<ICatalogService, CatalogService>();
        services.AddScoped<IRequestCatalogService, RequestCatalogService>();
        services.AddScoped<IEquipmentOrderService, EquipmentOrderService>();

        services.Configure<OrderingOptions>(
            configuration.GetSection(OrderingOptions.SectionName));

        services.Configure<ApproverCsvImportOptions>(
            configuration.GetSection("ApproverCsvImport"));
        services.AddScoped<IApproverCsvReader, ApproverCsvReader>();
        services.AddScoped<IApproverFolderImportRunner, ApproverCsvImportRunner>();
        services.AddHostedService<ApproverCsvImportWorker>();

        return services;
    }

    public static IServiceCollection AddAppSwagger(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();
        return services;
    }

    public static IServiceCollection AddAppHttpsRedirection(this IServiceCollection services)
    {
        services.AddHttpsRedirection(options =>
        {
            options.HttpsPort = 443;
        });

        return services;
    }
}
