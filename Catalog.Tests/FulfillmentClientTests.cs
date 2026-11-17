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
