using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Lagerverwaltung.Web.Startup;

public static class AppPipelineExtensions
{
    public static WebApplication UseAppPipeline(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error");
            app.UseHsts();
        }
        else
        {
            app.UseDeveloperExceptionPage();
        }

        app.UseHttpsRedirection();
        app.UseStaticFiles();
        

        app.UseCookiePolicy();

        // Authentication must come before authorization.
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseAntiforgery();
        return app;
    }

    public static WebApplication UseAdminOnlySwaggerGate(this WebApplication app)
    {
        // Protect Swagger UI and swagger.json behind AdminOnly.
        // Covers /swagger, /swagger/index.html, /swagger/v1/swagger.json, etc.
        app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments("/swagger"))
            {
                var authorizationService =
                    context.RequestServices.GetRequiredService<IAuthorizationService>();

                var result = await authorizationService.AuthorizeAsync(
                    context.User,
                    resource: null,
                    policyName: "AdminOnly");

                if (!result.Succeeded)
                {
                    if (context.User?.Identity?.IsAuthenticated == true)
                    {
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        await context.Response.WriteAsync("Forbidden - Admin only.");
                    }
                    else
                    {
                        await context.ChallengeAsync(OpenIdConnectDefaults.AuthenticationScheme);
                    }

                    return;
                }
            }

            await next();
        });

        return app;
    }

    public static WebApplication UseAppSwagger(this WebApplication app)
    {
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "Lagerverwaltung API v1");
        });

        return app;
    }

    public static WebApplication UseStatusCodeLogging(this WebApplication app)
    {
        app.UseStatusCodePages(async context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogWarning(
                "Status Code: {StatusCode}, Original Path: {OriginalPath}",
                context.HttpContext.Response.StatusCode,
                context.HttpContext.Request.Path);

            await Task.CompletedTask;
        });

        return app;
    }

    public static WebApplication LogStartup(this WebApplication app)
    {
        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        logger.LogInformation(
            "Application started in {Environment} environment",
            app.Environment.EnvironmentName);

        return app;
    }
}
