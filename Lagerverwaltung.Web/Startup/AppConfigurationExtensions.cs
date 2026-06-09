using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Lagerverwaltung.Web.Startup;

public static class AppConfigurationExtensions
{
    public static WebApplicationBuilder AddAppConfiguration(this WebApplicationBuilder builder)
    {
        // JSON first.
        builder.Configuration
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile(
                $"appsettings.{builder.Environment.EnvironmentName}.json",
                optional: true,
                reloadOnChange: true);

        // User Secrets only for local Development, after JSON so they override JSON.
        if (builder.Environment.IsDevelopment())
        {
            builder.Configuration.AddUserSecrets<Program>(optional: true);
        }

        // Environment variables last, so server/IIS values override everything.
        builder.Configuration.AddEnvironmentVariables();

        return builder;
    }

    public static WebApplicationBuilder UseCorporateProxyDefaults(this WebApplicationBuilder builder)
    {
        HttpClient.DefaultProxy.Credentials = CredentialCache.DefaultCredentials;
        return builder;
    }
}
