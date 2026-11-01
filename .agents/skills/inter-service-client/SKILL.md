---
name: inter-service-client
description: Add or modify outbound typed REST clients in CatalogService (port 8002). Use when CatalogService synchronizes product variants or initial inventory with FulfillmentService without using message brokers.
---

# Outbound REST Client in CatalogService

> Scope: CatalogService repository only. Never reference parent workspace paths, root documentation, or sibling service directories.

## Local Facts

- Service: CatalogService
- Local HTTP Port: 8002
- Primary Downstream Target: FulfillmentService (Port 8006)
- Target Use Case: Initializing physical warehouse inventory records upon variant creation (`IFulfillmentClient` -> `POST api/v1/inventories/initialize`)
- Interface Location: `Catalog.Application/Interfaces/`
- Implementation Location: `Catalog.Infrastructure/Services/{Target}/`
- DI Registration: `Catalog.Infrastructure/DependencyInjection.cs`

## Invariants

1. **100% RESTful HTTP/JSON:** RabbitMQ, MassTransit, and outbox tables are decommissioned. All inter-service calls use typed `HttpClient`.
2. **Strict Project Isolation:** Never reference external assemblies or query `fulfillment_db` directly.
3. **No Internal API Keys:** Inter-service routes use standard public endpoints. Do not send `X-Internal-Service-Key`.
4. **Idempotency on Creation:** Mutating inventory initialization calls should transmit an `Idempotency-Key` header with a deterministic or UUIDv4 string.
5. **Timeout:** Enforce `client.Timeout = TimeSpan.FromSeconds(10)`.

## Implementation Steps

### 1. Application Contract
Define `Catalog.Application/Interfaces/IFulfillmentClient.cs`:
```csharp
namespace Catalog.Application.Interfaces;

public interface IFulfillmentClient
{
    Task<bool> InitializeInventoryAsync(
        Guid skuId,
        Guid warehouseId,
        int initialStock,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default);
}
```

### 2. Infrastructure Typed Client
Implement `Catalog.Infrastructure/Services/Fulfillment/FulfillmentClient.cs`:
```csharp
namespace Catalog.Infrastructure.Services.Fulfillment;

public sealed class FulfillmentClient : IFulfillmentClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<FulfillmentClient> _logger;

    public FulfillmentClient(HttpClient httpClient, ILogger<FulfillmentClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<bool> InitializeInventoryAsync(
        Guid skuId,
        Guid warehouseId,
        int initialStock,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var payload = new { skuId, warehouseId, initialStock };
            using var request = new HttpRequestMessage(HttpMethod.Post, "api/v1/inventories/initialize")
            {
                Content = JsonContent.Create(payload)
            };

            if (!string.IsNullOrWhiteSpace(idempotencyKey))
            {
                request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
            }

            var response = await _httpClient.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return true;
            }

            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("[REST Fulfillment] Inventory init failed: {Status} {Error}", response.StatusCode, errorBody);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[REST Fulfillment] Connection error during inventory initialization.");
            return false;
        }
    }
}
```

### 3. Dependency Injection Registration
In `Catalog.Infrastructure/DependencyInjection.cs`:
```csharp
var fulfillmentBaseUrl = Environment.GetEnvironmentVariable("FULFILLMENT_SERVICE_URL")
    ?? configuration["FULFILLMENT_SERVICE_URL"]
    ?? configuration["Services:Fulfillment:BaseUrl"]
    ?? "http://localhost:8006/";

services.AddHttpClient<IFulfillmentClient, FulfillmentClient>(client =>
{
    client.BaseAddress = new Uri(fulfillmentBaseUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(10);
});
```

### 4. Configuration Synchronization
Ensure `FULFILLMENT_SERVICE_URL: "${FULFILLMENT_SERVICE_URL:-http://localhost:8006/}"` is configured in `docker-compose.yml` and `.env.example`.
