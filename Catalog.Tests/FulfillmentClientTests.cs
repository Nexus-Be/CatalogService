namespace Catalog.Tests;

using System.Net;
using System.Text;
using Catalog.Infrastructure.Services.Fulfillment;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

public sealed class FulfillmentClientTests
{
    [Fact]
    public async Task DeclareInventory_UsesTheServiceToServiceRoute()
    {
        // The public /inventories/initialize route needs a signed-in seller or admin. Catalog calls on its own behalf
        // (a staff member approving a product, a background worker), so it must use the internal route.
        var handler = new RecordingHandler((HttpStatusCode.OK,
            """{"id":"11111111-1111-1111-1111-111111111111","skuId":"22222222-2222-2222-2222-222222222222","warehouseId":"33333333-3333-3333-3333-333333333333","totalQuantity":0,"reservedQuantity":0,"availableQuantity":0}"""));
        var client = new FulfillmentClient(new HttpClient(handler) { BaseAddress = new Uri("http://fulfillment.test/") }, NullLogger<FulfillmentClient>.Instance);

        var result = await client.DeclareInventoryAsync(new(Guid.NewGuid(), Guid.NewGuid(), 0));

        Assert.NotNull(result);
        var (method, path) = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, method);
        Assert.Equal("/api/v1/internal/inventories/initialize", path);
    }

    [Fact]
    public async Task DeclareInventory_WhenFulfillmentRefuses_ReturnsNullSoTheWorkerRetries()
    {
        var handler = new RecordingHandler((HttpStatusCode.Unauthorized, """{"errorCode":"UNAUTHORIZED"}"""));
        var client = new FulfillmentClient(new HttpClient(handler) { BaseAddress = new Uri("http://fulfillment.test/") }, NullLogger<FulfillmentClient>.Instance);

        var result = await client.DeclareInventoryAsync(new(Guid.NewGuid(), Guid.NewGuid(), 0));

        Assert.Null(result);
    }

    [Fact]
    public async Task GetStockBalances_UsesInternalBatchGet_AndAggregatesQuantitiesAcrossWarehouses()
    {
        var sku1 = Guid.NewGuid();
        var sku2 = Guid.NewGuid();
        var wh1 = Guid.NewGuid();
        var wh2 = Guid.NewGuid();

        // sku1 has 2 warehouse entries (10 + 5 = 15 total; 8 + 4 = 12 available; 2 + 1 = 3 reserved)
        var responseBody = $$"""
            [
                {"id":"{{Guid.NewGuid()}}","skuId":"{{sku1}}","warehouseId":"{{wh1}}","totalQuantity":10,"reservedQuantity":2,"availableQuantity":8},
                {"id":"{{Guid.NewGuid()}}","skuId":"{{sku1}}","warehouseId":"{{wh2}}","totalQuantity":5,"reservedQuantity":1,"availableQuantity":4},
                {"id":"{{Guid.NewGuid()}}","skuId":"{{sku2}}","warehouseId":"{{wh1}}","totalQuantity":20,"reservedQuantity":0,"availableQuantity":20}
            ]
            """;

        var handler = new RecordingHandler((HttpStatusCode.OK, responseBody));
        var client = new FulfillmentClient(new HttpClient(handler) { BaseAddress = new Uri("http://fulfillment.test/") }, NullLogger<FulfillmentClient>.Instance);

        var result = await client.GetStockBalancesAsync(new[] { sku1, sku2 });

        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.True(result.ContainsKey(sku1));
        Assert.Equal(15, result[sku1].TotalQuantity);
        Assert.Equal(12, result[sku1].AvailableQuantity);
        Assert.Equal(3, result[sku1].ReservedQuantity);

        Assert.True(result.ContainsKey(sku2));
        Assert.Equal(20, result[sku2].TotalQuantity);
        Assert.Equal(20, result[sku2].AvailableQuantity);
        Assert.Equal(0, result[sku2].ReservedQuantity);

        var (method, path) = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, method);
        Assert.Equal("/api/v1/internal/inventories/batch-get", path);
    }

    [Fact]
    public async Task GetStockBalances_WhenDownstreamFails_GracefullyReturnsEmptyDictionary()
    {
        var handler = new RecordingHandler((HttpStatusCode.InternalServerError, "{}"));
        var client = new FulfillmentClient(new HttpClient(handler) { BaseAddress = new Uri("http://fulfillment.test/") }, NullLogger<FulfillmentClient>.Instance);

        var result = await client.GetStockBalancesAsync(new[] { Guid.NewGuid() });

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    private sealed class RecordingHandler(params (HttpStatusCode Status, string Body)[] responses) : HttpMessageHandler
    {
        private int _next;
        public List<(HttpMethod Method, string Path)> Requests { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add((request.Method, request.RequestUri!.AbsolutePath));
            var (status, body) = responses[Math.Min(_next++, responses.Length - 1)];
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }
}
