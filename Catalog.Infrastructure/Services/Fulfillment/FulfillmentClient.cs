namespace Catalog.Infrastructure.Services.Fulfillment;

using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Catalog.Application.Interfaces;

public sealed class FulfillmentClient : IFulfillmentClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<FulfillmentClient> _logger;

    public FulfillmentClient(HttpClient httpClient, ILogger<FulfillmentClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<InventoryClientResponse?> InitializeInventoryAsync(
        InitializeInventoryClientRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/v1/inventories/initialize", request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<InventoryClientResponse>(cancellationToken: cancellationToken);
                _logger.LogInformation("[REST Internal Fulfillment] Initialized inventory for SKU '{SkuId}'.", request.SkuId);
                return result;
            }

            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("[REST Internal Fulfillment] Failed to initialize inventory for SKU '{SkuId}'. Status: {StatusCode}, Body: {Body}",
                request.SkuId, response.StatusCode, errorBody);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[REST Internal Fulfillment] Connection error when initializing inventory for SKU '{SkuId}'.", request.SkuId);
            return null;
        }
    }
}
