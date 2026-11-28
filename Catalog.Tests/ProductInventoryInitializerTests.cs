namespace Catalog.Tests;

using Catalog.Application.Interfaces;
using Catalog.Application.Services.Product;
using Catalog.Domain.Entities;
using Catalog.Domain.Enums;
using Catalog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

public sealed class ProductInventoryInitializerTests
{
    private static readonly Guid SellerId = Guid.NewGuid();

    [Fact]
    public async Task TryInitialize_WhenFulfillmentSucceeds_MarksSkuDone()
    {
        await using var db = CreateDb();
        var variant = await SeedPendingVariantAsync(db);
        var fulfillment = new FakeFulfillmentClient();

        await CreateInitializer(db, fulfillment).TryInitializeAsync(new[] { variant }, SellerId);

        Assert.Equal(InventoryInitState.Done, variant.InventoryInitState);
        Assert.Null(variant.InventoryInitNextRetryAt);
        Assert.Equal(1, fulfillment.Declares);
    }

    [Fact]
    public async Task TryInitialize_WhenDeclareFails_KeepsSkuPendingWithBackoff()
    {
        await using var db = CreateDb();
        var variant = await SeedPendingVariantAsync(db);
        var before = DateTimeOffset.UtcNow;

        await CreateInitializer(db, new FakeFulfillmentClient { DeclareSucceeds = false }).TryInitializeAsync(new[] { variant }, SellerId);

        Assert.Equal(InventoryInitState.Pending, variant.InventoryInitState);
        Assert.Equal(1, variant.InventoryInitAttemptCount);
        Assert.NotNull(variant.InventoryInitLastError);
        Assert.True(variant.InventoryInitNextRetryAt > before);
    }

    [Fact]
    public async Task TryInitialize_WhenSellerHasNoActiveWarehouse_StaysPendingWithReason()
    {
        await using var db = CreateDb();
        var variant = await SeedPendingVariantAsync(db);
        var fulfillment = new FakeFulfillmentClient { HasWarehouse = false };

        await CreateInitializer(db, fulfillment).TryInitializeAsync(new[] { variant }, SellerId);

        Assert.Equal(InventoryInitState.Pending, variant.InventoryInitState);
        Assert.Contains("warehouse", variant.InventoryInitLastError);
        Assert.Equal(0, fulfillment.Declares);
    }

    [Fact]
    public async Task TryInitialize_WhenFulfillmentThrows_DoesNotThrowAndStaysPending()
    {
        await using var db = CreateDb();
        var variant = await SeedPendingVariantAsync(db);

        await CreateInitializer(db, new FakeFulfillmentClient { Throws = true }).TryInitializeAsync(new[] { variant }, SellerId);

        Assert.Equal(InventoryInitState.Pending, variant.InventoryInitState);
        Assert.Contains("boom", variant.InventoryInitLastError);
    }

    [Fact]
    public async Task TryInitialize_LooksUpTheSellerWarehouseOnceForAllSkus()
    {
        await using var db = CreateDb();
        var first = await SeedPendingVariantAsync(db);
        var second = await SeedPendingVariantAsync(db);
        var fulfillment = new FakeFulfillmentClient();

        await CreateInitializer(db, fulfillment).TryInitializeAsync(new[] { first, second }, SellerId);

        Assert.Equal(1, fulfillment.WarehouseLookups);
        Assert.Equal(2, fulfillment.Declares);
    }

    [Fact]
    public async Task ProcessDue_RetriesPendingSkuAfterFulfillmentRecovers()
    {
        await using var db = CreateDb();
        var variant = await SeedPendingVariantAsync(db);
        var fulfillment = new FakeFulfillmentClient { DeclareSucceeds = false };
        var initializer = CreateInitializer(db, fulfillment);

        await initializer.TryInitializeAsync(new[] { variant }, SellerId);
        Assert.Equal(InventoryInitState.Pending, variant.InventoryInitState);

        // Fulfillment is back and the retry time has come.
        fulfillment.DeclareSucceeds = true;
        await MakeDueAsync(db, variant);

        var processed = await initializer.ProcessDueAsync(10);

        Assert.Equal(1, processed);
        Assert.Equal(InventoryInitState.Done, variant.InventoryInitState);
        Assert.Equal(2, fulfillment.Declares);
    }

    [Fact]
    public async Task ProcessDue_SkipsSkuWhoseRetryTimeHasNotCome()
    {
        await using var db = CreateDb();
        var variant = await SeedPendingVariantAsync(db);
        variant.InventoryInitNextRetryAt = DateTimeOffset.UtcNow.AddMinutes(5);
        await db.SaveChangesAsync();
        var fulfillment = new FakeFulfillmentClient();

        var processed = await CreateInitializer(db, fulfillment).ProcessDueAsync(10);

        Assert.Equal(0, processed);
        Assert.Equal(0, fulfillment.Declares);
        Assert.Equal(InventoryInitState.Pending, variant.InventoryInitState);
    }

    [Fact]
    public void MarkPending_LeavesTheSkuToTheCallerForOneLease()
    {
        // The approval fast path initializes the SKU right after saving it; the worker must not pick it up at the same time.
        var variant = NewVariant(Guid.NewGuid());
        var now = DateTimeOffset.UtcNow;

        ProductInventoryInitializer.MarkPending(variant, now);

        Assert.Equal(InventoryInitState.Pending, variant.InventoryInitState);
        Assert.Equal(now.Add(ProductInventoryInitializer.ClaimLease), variant.InventoryInitNextRetryAt);
    }

    [Fact]
    public async Task ProcessDue_AfterMaxAttempts_FlagsNeedsAttentionButKeepsRetryingSlowly()
    {
        await using var db = CreateDb();
        var variant = await SeedPendingVariantAsync(db);
        variant.InventoryInitAttemptCount = ProductInventoryInitializer.MaxAttempts - 1;
        await db.SaveChangesAsync();
        var fulfillment = new FakeFulfillmentClient { DeclareSucceeds = false };
        var initializer = CreateInitializer(db, fulfillment);
        var before = DateTimeOffset.UtcNow;

        await initializer.ProcessDueAsync(10);

        Assert.Equal(InventoryInitState.NeedsAttention, variant.InventoryInitState);
        Assert.True(variant.InventoryInitNextRetryAt >= before.Add(ProductInventoryInitializer.AttentionRetryInterval));

        // Not retried right away...
        Assert.Equal(0, await initializer.ProcessDueAsync(10));

        // ...but once the slow retry comes and the cause is fixed, the SKU recovers by itself.
        fulfillment.DeclareSucceeds = true;
        await MakeDueAsync(db, variant);
        Assert.Equal(1, await initializer.ProcessDueAsync(10));
        Assert.Equal(InventoryInitState.Done, variant.InventoryInitState);
    }

    [Fact]
    public async Task ProcessDue_ForAuctionProduct_MarksSkuNotRequiredWithoutCallingFulfillment()
    {
        await using var db = CreateDb();
        var variant = await SeedPendingVariantAsync(db, SaleType.Auction);
        var fulfillment = new FakeFulfillmentClient();

        await CreateInitializer(db, fulfillment).ProcessDueAsync(10);

        Assert.Equal(InventoryInitState.NotRequired, variant.InventoryInitState);
        Assert.Equal(0, fulfillment.Declares);
    }

    [Fact]
    public void MarkPending_DoesNotReopenSkuThatIsAlreadyDone()
    {
        var variant = NewVariant(Guid.NewGuid());
        variant.InventoryInitState = InventoryInitState.Done;

        ProductInventoryInitializer.MarkPending(variant, DateTimeOffset.UtcNow);

        Assert.Equal(InventoryInitState.Done, variant.InventoryInitState);
    }

    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"catalog-tests-{Guid.NewGuid():N}")
            .Options);

    private static ProductInventoryInitializer CreateInitializer(AppDbContext db, IFulfillmentClient fulfillment) =>
        new(db, fulfillment, NullLogger<ProductInventoryInitializer>.Instance);

    private static async Task MakeDueAsync(AppDbContext db, ProductVariant variant)
    {
        variant.InventoryInitNextRetryAt = DateTimeOffset.UtcNow.AddSeconds(-1);
        await db.SaveChangesAsync();
    }

    private static async Task<ProductVariant> SeedPendingVariantAsync(AppDbContext db, SaleType saleType = SaleType.Regular)
    {
        var product = new Product
        {
            Id = Guid.NewGuid(),
            SellerId = SellerId,
            CategoryId = Guid.NewGuid(),
            Name = "Test product",
            SaleType = saleType,
            Status = ProductStatus.Active
        };
        var variant = NewVariant(product.Id);
        ProductInventoryInitializer.MarkPending(variant, DateTimeOffset.UtcNow);
        variant.InventoryInitNextRetryAt = DateTimeOffset.UtcNow.AddSeconds(-1);
        product.Variants.Add(variant);
        db.Products.Add(product);
        await db.SaveChangesAsync();
        return variant;
    }

    private static ProductVariant NewVariant(Guid productId) => new()
    {
        Id = Guid.NewGuid(),
        ProductId = productId,
        SkuCode = $"SKU-{Guid.NewGuid():N}",
        Name = "Default",
        Price = 100_000m,
        Status = VariantStatus.Active,
        IsActive = true
    };

    private sealed class FakeFulfillmentClient : IFulfillmentClient
    {
        private static readonly Guid WarehouseId = Guid.NewGuid();

        public bool HasWarehouse { get; set; } = true;
        public bool DeclareSucceeds { get; set; } = true;
        public bool Throws { get; set; }
        public int WarehouseLookups { get; private set; }
        public int Declares { get; private set; }

        public Task<IReadOnlyList<WarehouseClientDto>> GetWarehousesBySellerIdAsync(Guid sellerId, CancellationToken cancellationToken = default)
        {
            WarehouseLookups++;
            if (Throws)
                throw new HttpRequestException("boom");
            IReadOnlyList<WarehouseClientDto> list = HasWarehouse
                ? new[] { new WarehouseClientDto(WarehouseId, "WH", "Seller warehouse", null, SellerId: sellerId) }
                : Array.Empty<WarehouseClientDto>();
            return Task.FromResult(list);
        }

        public Task<InventoryClientResponse?> DeclareInventoryAsync(DeclareInventoryClientRequest request, CancellationToken cancellationToken = default)
        {
            Declares++;
            return Task.FromResult(DeclareSucceeds
                ? new InventoryClientResponse(Guid.NewGuid(), request.SkuId, request.WarehouseId, 0, 0, 0)
                : null);
        }

        public Task<IReadOnlyList<WarehouseClientDto>> GetActiveWarehousesAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<InventoryClientResponse?> InitializeInventoryForSkuAsync(
            Guid skuId, Guid sellerId, int initialQuantity = 0, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<InventoryClientResponse?> InitializeInventoryAsync(InitializeInventoryClientRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyDictionary<Guid, SkuStockBalanceDto>> GetStockBalancesAsync(
            IReadOnlyList<Guid> skuIds, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<Guid, SkuStockBalanceDto>>(new Dictionary<Guid, SkuStockBalanceDto>());
    }
}
