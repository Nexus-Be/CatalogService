using System.Text.Json.Serialization;
using Catalog.Api.Extensions;
using Catalog.Api.Middleware;
using Catalog.Application;
using Catalog.Infrastructure;
using Catalog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// 0. Observability / OpenTelemetry Tracing & Metrics
builder.Services.AddServiceObservability(builder.Configuration, "catalog-service");

// Cấu hình Kestrel chỉ định HTTP/1 & HTTP/2
builder.WebHost.ConfigureKestrel(options =>
{
    options.ConfigureEndpointDefaults(listenOptions =>
    {
        listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1AndHttp2;
    });
});

// 1. Application & Infrastructure Dependency Injection
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);

// 2. Swagger / OpenAPI & JSON Enum Conversion
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.Configure<Microsoft.AspNetCore.Mvc.JsonOptions>(options =>
{
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

var app = builder.Build();

// 3. Global Exception Handler Middleware
app.UseGlobalExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Automatically apply database migrations on startup if relational DB is available
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        if (db.Database.IsRelational())
        {
            db.Database.Migrate();
        }
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning("Database migration deferred on startup: {Message}", ex.Message);
    }
}

// 4. Standard Health & Test Endpoint
app.MapGet("/health", () => Results.Ok(new { status = "Healthy", service = "Catalog.Api" }));

app.Run();

// Expose Program for WebApplicationFactory<Program> in Catalog.Tests
public partial class Program { }
