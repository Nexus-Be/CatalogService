namespace Catalog.Infrastructure;

using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Catalog.Application.Interfaces;
using Catalog.Infrastructure.Persistence;
using Catalog.Infrastructure.Services;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Host=localhost;Port=5434;Database=category_db;Username=postgres;Password=MatkhaucuaTui123@";

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<ICatalogDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<IImageStorageService, CloudinaryStorageService>();

        // MassTransit — dùng In-Memory khi chạy local Development (không có RabbitMQ),
        // dùng RabbitMQ thật khi deploy Staging/Production.
        var rabbitHost = configuration["RabbitMq:Host"] ?? "localhost";
        var environment = configuration["ASPNETCORE_ENVIRONMENT"] ?? Environments.Production;
        var useInMemory = environment == Environments.Development && rabbitHost == "localhost";

        services.AddMassTransit(x =>
        {
            x.SetKebabCaseEndpointNameFormatter();

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
}
