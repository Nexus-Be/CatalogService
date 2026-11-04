namespace Catalog.Infrastructure.Services.Fulfillment;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Catalog.Application.Interfaces;

public sealed class FulfillmentClient : IFulfillmentClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<FulfillmentClient> _logger;

    public FulfillmentClient(HttpClient httpClient, ILogger<FulfillmentClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<IReadOnlyList<WarehouseClientDto>> GetWarehousesBySellerIdAsync(
        Guid sellerId,
        CancellationToken cancellationToken = default)
    {
        if (sellerId == Guid.Empty)
        {
            return Array.Empty<WarehouseClientDto>();
        }

        try
        {
            var response = await _httpClient.GetAsync($"api/v1/warehouses/seller/{sellerId}", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                if (string.IsNullOrWhiteSpace(content))
                {
                    return Array.Empty<WarehouseClientDto>();
                }

                if (content.TrimStart().StartsWith("["))
                {
                    var list = JsonSerializer.Deserialize<List<WarehouseClientDto>>(content, JsonOptions);
                    return list ?? (IReadOnlyList<WarehouseClientDto>)Array.Empty<WarehouseClientDto>();
                }

                var single = JsonSerializer.Deserialize<WarehouseClientDto>(content, JsonOptions);
                return single != null ? new[] { single } : Array.Empty<WarehouseClientDto>();
            }

            _logger.LogWarning("[REST Internal Fulfillment] GetWarehousesBySeller failed. Status: {StatusCode}", response.StatusCode);
            return Array.Empty<WarehouseClientDto>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[REST Internal Fulfillment] Connection error when querying warehouses for seller '{SellerId}'.", sellerId);
            return Array.Empty<WarehouseClientDto>();
        }
    }

    public async Task<IReadOnlyList<WarehouseClientDto>> GetActiveWarehousesAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("api/v1/warehouses", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                if (string.IsNullOrWhiteSpace(content))
                {
                    return Array.Empty<WarehouseClientDto>();
                }

                if (content.TrimStart().StartsWith("["))
                {
                    var list = JsonSerializer.Deserialize<List<WarehouseClientDto>>(content, JsonOptions);
                    return list ?? (IReadOnlyList<WarehouseClientDto>)Array.Empty<WarehouseClientDto>();
                }

                using var doc = JsonDocument.Parse(content);
                if (doc.RootElement.TryGetProperty("items", out var itemsElement) && itemsElement.ValueKind == JsonValueKind.Array)
                {
                    var list = JsonSerializer.Deserialize<List<WarehouseClientDto>>(itemsElement.GetRawText(), JsonOptions);
                    return list ?? (IReadOnlyList<WarehouseClientDto>)Array.Empty<WarehouseClientDto>();
                }

                return Array.Empty<WarehouseClientDto>();
            }

            _logger.LogWarning("[REST Internal Fulfillment] GetActiveWarehouses failed. Status: {StatusCode}", response.StatusCode);
            return Array.Empty<WarehouseClientDto>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[REST Internal Fulfillment] Connection error when querying active warehouses.");
            return Array.Empty<WarehouseClientDto>();
        }
    }

    public async Task<InventoryClientResponse?> DeclareInventoryAsync(
        DeclareInventoryClientRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var payload = new
            {
                skuId = request.SkuId,
                warehouseId = request.WarehouseId,
                quantity = request.Quantity,
                note = request.Note ?? "Initial inventory declared by CatalogService"
            };

            var response = await _httpClient.PostAsJsonAsync("api/v1/inventories/initialize", payload, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<InventoryClientResponse>(cancellationToken: cancellationToken);
                _logger.LogInformation("[REST Internal Fulfillment] Declared inventory for SKU '{SkuId}' at Warehouse '{WarehouseId}' with Quantity {Quantity}.",
                    request.SkuId, request.WarehouseId, request.Quantity);
                return result;
            }

            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("[REST Internal Fulfillment] Failed to declare inventory for SKU '{SkuId}'. Status: {StatusCode}, Body: {Body}",
                request.SkuId, response.StatusCode, errorBody);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[REST Internal Fulfillment] Connection error when declaring inventory for SKU '{SkuId}'.", request.SkuId);
            return null;
        }
    }

    public async Task<InventoryClientResponse?> InitializeInventoryForSkuAsync(
        Guid skuId,
        Guid sellerId,
        int initialQuantity = 0,
        CancellationToken cancellationToken = default)
    {
        Guid? targetWarehouseId = null;

        // Step 1: Look for an active warehouse belonging to the seller (WH-03)
        if (sellerId != Guid.Empty)
        {
            var sellerWarehouses = await GetWarehousesBySellerIdAsync(sellerId, cancellationToken);
            var activeSellerWh = sellerWarehouses.FirstOrDefault(w => w.IsActive);
            if (activeSellerWh != null)
            {
                targetWarehouseId = activeSellerWh.Id;
            }
        }

        // Step 2: Fallback to active platform/central warehouse if seller has no dedicated warehouse (WH-01)
        if (!targetWarehouseId.HasValue)
        {
            var activeWarehouses = await GetActiveWarehousesAsync(cancellationToken);
            var fallbackWh = activeWarehouses.FirstOrDefault(w => !w.SellerId.HasValue || w.SellerId.Value == Guid.Empty)
                ?? activeWarehouses.FirstOrDefault(w => w.IsActive);

            if (fallbackWh != null)
            {
                targetWarehouseId = fallbackWh.Id;
            }
        }

        if (targetWarehouseId.HasValue)
        {
            return await DeclareInventoryAsync(
                new DeclareInventoryClientRequest(skuId, targetWarehouseId.Value, initialQuantity, "Initial inventory declared by CatalogService"),
                cancellationToken);
        }

        _logger.LogWarning("[REST Internal Fulfillment] No active warehouse found for seller '{SellerId}' or platform. Cannot declare inventory for SKU '{SkuId}'.",
            sellerId, skuId);
        return null;
    }

    public async Task<InventoryClientResponse?> InitializeInventoryAsync(
        InitializeInventoryClientRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.WarehouseId.HasValue && request.WarehouseId.Value != Guid.Empty)
        {
            return await DeclareInventoryAsync(
                new DeclareInventoryClientRequest(request.SkuId, request.WarehouseId.Value, request.InitialQuantity),
                cancellationToken);
        }

        return await InitializeInventoryForSkuAsync(request.SkuId, Guid.Empty, request.InitialQuantity, cancellationToken);
    }
}
