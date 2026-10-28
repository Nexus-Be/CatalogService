namespace Catalog.Infrastructure;

using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Catalog.Application.Interfaces;
using Catalog.Infrastructure.Consumers;
using Catalog.Infrastructure.Persistence;
using Catalog.Infrastructure.Services;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        var rawConnection = configuration.GetConnectionString("DefaultConnection")
            ?? configuration["CONNECTION_STRING"]
            ?? configuration["CATALOG_CONNECTION_STRING"]
            ?? configuration["CATALOG_DB_CONNECTION"]
            ?? "Host=localhost;Port=5434;Database=category_db;Username=postgres;Password=postgres";
        var connectionString = NormalizeConnectionString(rawConnection);

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<ICatalogDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<IImageStorageService, CloudinaryStorageService>();

        // MassTransit — dùng In-Memory khi chạy local Development (không có RabbitMQ),
        // dùng RabbitMQ thật khi deploy Staging/Production.
        var rabbitHost = configuration["RabbitMq:Host"] ?? "localhost";
        var environment = configuration["ASPNETCORE_ENVIRONMENT"] ?? Environments.Production;
        var useInMemory = bool.TryParse(configuration["RabbitMq:UseInMemory"], out var explicitInMemory)
            ? explicitInMemory
            : (environment == Environments.Development && rabbitHost == "localhost");

        services.AddMassTransit(x =>
        {
            x.SetKebabCaseEndpointNameFormatter();
            x.AddConsumer<CategoryCreatedConsumer>();

            if (useInMemory)
            {
                // Development local: dùng In-Memory, không cần RabbitMQ chạy
                x.UsingInMemory((context, cfg) =>
                {
                    cfg.ConfigureEndpoints(context);
                });
            }
            else
            {
                // Staging/Production: dùng RabbitMQ thật + EF Outbox
                x.AddEntityFrameworkOutbox<AppDbContext>(o =>
                {
                    o.UsePostgres();
                    o.UseBusOutbox();
                });

                x.UsingRabbitMq((context, cfg) =>
                {
                    var rabbitPort = ushort.TryParse(configuration["RabbitMq:Port"], out var port) ? port : (ushort)5672;
                    var rabbitUser = configuration["RabbitMq:User"] ?? "guest";
                    var rabbitPass = configuration["RabbitMq:Password"] ?? "guest";

                    cfg.Host(rabbitHost, rabbitPort, "/", h =>
                    {
                        h.Username(rabbitUser);
                        h.Password(rabbitPass);
                    });

                    cfg.ConfigureEndpoints(context);
                });
            }
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
