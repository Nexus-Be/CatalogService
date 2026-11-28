namespace Catalog.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Catalog.Application.Interfaces;
using Catalog.Application.Services.Product;
using Catalog.Domain.Entities;
using Catalog.Domain.Enums;
using Catalog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

public sealed class ProductInventoryEnrichmentTests
{
    private static readonly Guid SellerId = Guid.NewGuid();

    [Fact]
    public async Task SearchAsync_EnrichesAggregatedStockFromFulfillment()
    {
        await using var db = CreateDb();
        var (product, variant1, variant2) = await SeedProductWithVariantsAsync(db, SellerId, ProductStatus.Active);

        var stubFulfillment = new StubFulfillmentClient(new Dictionary<Guid, SkuStockBalanceDto>
        {
            [variant1.Id] = new(variant1.Id, TotalQuantity: 10, AvailableQuantity: 8, ReservedQuantity: 2),
            [variant2.Id] = new(variant2.Id, TotalQuantity: 5, AvailableQuantity: 4, ReservedQuantity: 1)
        });

        var service = CreateService(db, stubFulfillment);

        var result = await service.SearchAsync(new ProductListQuery(Keyword: "Test Product"));

        var item = Assert.Single(result.Items);
        Assert.Equal(product.Id, item.ProductId);
        Assert.Equal(15, item.TotalQuantity); // 10 + 5
        Assert.Equal(12, item.AvailableQuantity); // 8 + 4
    }

    [Fact]
    public async Task GetDetailAsync_EnrichesProductAndVariantStock_WithoutExposingReservedToBuyer()
    {
        await using var db = CreateDb();
        var (product, variant1, variant2) = await SeedProductWithVariantsAsync(db, SellerId, ProductStatus.Active);

        var stubFulfillment = new StubFulfillmentClient(new Dictionary<Guid, SkuStockBalanceDto>
        {
            [variant1.Id] = new(variant1.Id, TotalQuantity: 10, AvailableQuantity: 8, ReservedQuantity: 2),
            [variant2.Id] = new(variant2.Id, TotalQuantity: 5, AvailableQuantity: 4, ReservedQuantity: 1)
        });

        var service = CreateService(db, stubFulfillment);

        var detail = await service.GetDetailAsync(product.Id);

        Assert.NotNull(detail);
        Assert.Equal(15, detail.TotalQuantity);
        Assert.Equal(12, detail.AvailableQuantity);

        var v1Dto = detail.Variants.First(v => v.VariantId == variant1.Id);
        Assert.Equal(10, v1Dto.TotalQuantity);
        Assert.Equal(8, v1Dto.AvailableQuantity);

        var v2Dto = detail.Variants.First(v => v.VariantId == variant2.Id);
        Assert.Equal(5, v2Dto.TotalQuantity);
        Assert.Equal(4, v2Dto.AvailableQuantity);
    }

    [Fact]
    public async Task SellerEndpoints_EnrichStockAndIncludeReservedQuantity()
    {
        await using var db = CreateDb();
        var (product, variant1, variant2) = await SeedProductWithVariantsAsync(db, SellerId, ProductStatus.Active);

        var stubFulfillment = new StubFulfillmentClient(new Dictionary<Guid, SkuStockBalanceDto>
        {
            [variant1.Id] = new(variant1.Id, TotalQuantity: 20, AvailableQuantity: 15, ReservedQuantity: 5),
            [variant2.Id] = new(variant2.Id, TotalQuantity: 10, AvailableQuantity: 8, ReservedQuantity: 2)
        });

        var service = CreateService(db, stubFulfillment);

        // Test Seller List
        var searchResult = await service.SearchSellerProductsAsync(SellerId, new SellerProductSearchQuery(1, 20));
        var sellerItem = Assert.Single(searchResult.Items);
        Assert.Equal(30, sellerItem.TotalQuantity); // 20 + 10
        Assert.Equal(23, sellerItem.AvailableQuantity); // 15 + 8
        Assert.Equal(7, sellerItem.ReservedQuantity); // 5 + 2

        // Test Seller Detail
        var sellerDetail = await service.GetSellerProductDetailAsync(product.Id, SellerId);
        Assert.NotNull(sellerDetail);
        Assert.Equal(30, sellerDetail.TotalQuantity);
        Assert.Equal(23, sellerDetail.AvailableQuantity);
        Assert.Equal(7, sellerDetail.ReservedQuantity);

        var v1 = sellerDetail.Variants.First(v => v.VariantId == variant1.Id);
        Assert.Equal(20, v1.TotalQuantity);
        Assert.Equal(15, v1.AvailableQuantity);
        Assert.Equal(5, v1.ReservedQuantity);
    }

    [Fact]
    public async Task WhenFulfillmentReturnsEmptyOrFails_EndpointsGracefullyDefaultToZero()
    {
        await using var db = CreateDb();
        var (product, variant1, _) = await SeedProductWithVariantsAsync(db, SellerId, ProductStatus.Active);

        var stubFulfillment = new StubFulfillmentClient(throws: true);
        var service = CreateService(db, stubFulfillment);

        // Buyer list
        var searchResult = await service.SearchAsync(new ProductListQuery());
        var item = Assert.Single(searchResult.Items);
        Assert.Equal(0, item.TotalQuantity);
        Assert.Equal(0, item.AvailableQuantity);

        // Buyer detail
        var detail = await service.GetDetailAsync(product.Id);
        Assert.Equal(0, detail.TotalQuantity);
        Assert.Equal(0, detail.AvailableQuantity);
        Assert.Equal(0, detail.Variants[0].TotalQuantity);
        Assert.Equal(0, detail.Variants[0].AvailableQuantity);

        // Variant detail
        var variantDetail = await service.GetVariantByIdAsync(variant1.Id);
        Assert.Equal(0, variantDetail.TotalQuantity);
        Assert.Equal(0, variantDetail.AvailableQuantity);
    }

    private static async Task<(Product, ProductVariant, ProductVariant)> SeedProductWithVariantsAsync(
        AppDbContext db,
        Guid sellerId,
        ProductStatus status)
    {
        var category = new Category { Id = Guid.NewGuid(), Name = "Electronics", Level = 1 };
        db.Categories.Add(category);

        var product = new Product
        {
            Id = Guid.NewGuid(),
            SellerId = sellerId,
            CategoryId = category.Id,
            Name = "Test Product",
            SaleType = SaleType.Regular,
            Status = status,
            WeightGrams = 300
        };

        var variant1 = new ProductVariant
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            SellerId = sellerId,
            SkuCode = $"SKU-RED-{Guid.NewGuid():N}",
            Name = "Red L",
            Price = 150000m,
            Currency = "VND",
            IsActive = true,
            Status = VariantStatus.Active
        };

        var variant2 = new ProductVariant
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            SellerId = sellerId,
            SkuCode = $"SKU-BLUE-{Guid.NewGuid():N}",
            Name = "Blue M",
            Price = 140000m,
            Currency = "VND",
            IsActive = true,
            Status = VariantStatus.Active
        };

        db.Products.Add(product);
        db.ProductVariants.AddRange(variant1, variant2);
        await db.SaveChangesAsync();

        return (product, variant1, variant2);
    }

    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase($"catalog-inv-enrich-{Guid.NewGuid():N}").Options);

    private static ProductService CreateService(AppDbContext db, IFulfillmentClient fulfillmentClient) =>
        new(db,
            fulfillmentClient,
            NoOp<IImageStorageService>.Create(),
            NoOp<IUserClient>.Create(),
            NoOp<INotificationClient>.Create(),
            NullLogger<ProductService>.Instance,
            NoOp<IProductInventoryInitializer>.Create());

    private sealed class StubFulfillmentClient : IFulfillmentClient
    {
        private readonly IReadOnlyDictionary<Guid, SkuStockBalanceDto> _balances;
        private readonly bool _throws;

        public StubFulfillmentClient(IReadOnlyDictionary<Guid, SkuStockBalanceDto>? balances = null, bool throws = false)
        {
            _balances = balances ?? new Dictionary<Guid, SkuStockBalanceDto>();
            _throws = throws;
        }

        public Task<IReadOnlyDictionary<Guid, SkuStockBalanceDto>> GetStockBalancesAsync(
            IReadOnlyList<Guid> skuIds,
            CancellationToken cancellationToken = default)
        {
            if (_throws)
            {
                throw new HttpRequestException("FulfillmentService offline");
            }

            var result = skuIds
                .Where(id => _balances.ContainsKey(id))
                .ToDictionary(id => id, id => _balances[id]);

            return Task.FromResult<IReadOnlyDictionary<Guid, SkuStockBalanceDto>>(result);
        }

        public Task<IReadOnlyList<WarehouseClientDto>> GetWarehousesBySellerIdAsync(Guid sellerId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<WarehouseClientDto>>(Array.Empty<WarehouseClientDto>());

        public Task<IReadOnlyList<WarehouseClientDto>> GetActiveWarehousesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<WarehouseClientDto>>(Array.Empty<WarehouseClientDto>());

        public Task<InventoryClientResponse?> DeclareInventoryAsync(DeclareInventoryClientRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult<InventoryClientResponse?>(null);

        public Task<InventoryClientResponse?> InitializeInventoryForSkuAsync(Guid skuId, Guid sellerId, int initialQuantity = 0, CancellationToken cancellationToken = default) =>
            Task.FromResult<InventoryClientResponse?>(null);

        public Task<InventoryClientResponse?> InitializeInventoryAsync(InitializeInventoryClientRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult<InventoryClientResponse?>(null);
    }

    private class NoOp<T> : DispatchProxy where T : class
    {
        public static T Create() => DispatchProxy.Create<T, NoOp<T>>();

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            var returnType = targetMethod!.ReturnType;
            if (returnType == typeof(void)) return null;
            if (returnType == typeof(Task)) return Task.CompletedTask;
            if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
            {
                var resultType = returnType.GetGenericArguments()[0];
                var value = resultType.IsValueType ? Activator.CreateInstance(resultType) : null;
                return typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(resultType).Invoke(null, new[] { value });
            }
            return returnType.IsValueType ? Activator.CreateInstance(returnType) : null;
        }
    }
}
