namespace Catalog.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Catalog.Application.Interfaces;
using Catalog.Infrastructure.Persistence;
using Catalog.Infrastructure.Services.Common;
using Catalog.Infrastructure.Services.Fulfillment;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        var rawConnection = configuration.GetConnectionString("DefaultConnection")
            ?? configuration["CONNECTION_STRING"]
            ?? configuration["CATALOG_CONNECTION_STRING"]
            ?? configuration["CATALOG_DB_CONNECTION"]
            ?? Environment.GetEnvironmentVariable("CATALOG_CONNECTION_STRING")
            ?? Environment.GetEnvironmentVariable("CONNECTION_STRING");

        if (string.IsNullOrWhiteSpace(rawConnection) || rawConnection.Contains("REPLACE_WITH"))
        {
            throw new InvalidOperationException("Database connection string for CatalogService is not configured. Set 'ConnectionStrings:DefaultConnection' or environment variable 'CATALOG_CONNECTION_STRING'.");
        }
        var connectionString = NormalizeConnectionString(rawConnection);

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<ICatalogDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        // Internal Fulfillment Client
        var fulfillmentBaseUrl = Environment.GetEnvironmentVariable("FULFILLMENT_SERVICE_URL")
            ?? configuration["FULFILLMENT_SERVICE_URL"]
            ?? configuration["Services:Fulfillment:BaseUrl"]
            ?? configuration["FulfillmentService:BaseUrl"]
            ?? "http://localhost:8006/";

        // Internal service key handler is temporarily disabled. Inter-service calls use plain REST routes.
        // services.AddTransient<InternalServiceAuthHandler>();
        services.AddHttpClient<IFulfillmentClient, FulfillmentClient>(client =>
        {
            client.BaseAddress = new Uri(fulfillmentBaseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(10);
        });

        return services;
    }

    private static string NormalizeConnectionString(string? connStr)
    {
        if (string.IsNullOrWhiteSpace(connStr)) return string.Empty;
        if (connStr.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) || 
            connStr.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            var uri = new Uri(connStr);
            var userInfo = uri.UserInfo.Split(':');
            var user = userInfo.Length > 0 ? Uri.UnescapeDataString(userInfo[0]) : "";
            var pass = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "";
            var host = uri.Host;
            var port = uri.Port > 0 ? uri.Port : 5432;
            var db = uri.AbsolutePath.TrimStart('/');
            return $"Host={host};Port={port};Database={db};Username={user};Password={pass};SSL Mode=Require;Trust Server Certificate=true";
        }
        return connStr;
    }
}
