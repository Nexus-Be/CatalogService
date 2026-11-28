namespace Catalog.Tests;

using System.Reflection;
using Catalog.Application.Interfaces;
using Catalog.Application.Services.Product;
using Catalog.Domain.Entities;
using Catalog.Domain.Enums;
using Catalog.Domain.Exceptions;
using Catalog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

public sealed class SkuLogicTests
{
    [Fact]
    public async Task CreateProduct_AllowsSameSkuAcrossDifferentSellers()
    {
        await using var db = CreateDb();
        var categoryId = await SeedCategoryAsync(db);
        var sellerA = Guid.NewGuid();
        var sellerB = Guid.NewGuid();
        var service = CreateService(db);

        var p1 = await service.CreateAsync(new CreateProductRequest(
            SellerId: sellerA,
            CategoryId: categoryId,
            Name: "Watch A",
            SaleType: SaleType.Regular,
            Variants: new List<CreateVariantRequest>
            {
                new("SHARED-SKU-01", "Black", 100000m)
            },
            WeightGrams: 200));

        var p2 = await service.CreateAsync(new CreateProductRequest(
            SellerId: sellerB,
            CategoryId: categoryId,
            Name: "Watch B",
            SaleType: SaleType.Regular,
            Variants: new List<CreateVariantRequest>
            {
                new("SHARED-SKU-01", "Black", 120000m)
            },
            WeightGrams: 200));

        Assert.NotEqual(p1.Id, p2.Id);
    }

    [Fact]
    public async Task CreateProduct_DuplicateSkuInSameShop_CaseInsensitive_ThrowsConflict()
    {
        await using var db = CreateDb();
        var categoryId = await SeedCategoryAsync(db);
        var seller = Guid.NewGuid();
        var service = CreateService(db);

        await service.CreateAsync(new CreateProductRequest(
            SellerId: seller,
            CategoryId: categoryId,
            Name: "Item 1",
            SaleType: SaleType.Regular,
            Variants: new List<CreateVariantRequest>
            {
                new("SHOP-ITEM-01", "Default", 50000m)
            },
            WeightGrams: 100));

        var ex = await Assert.ThrowsAsync<BusinessRuleValidationException>(() =>
            service.CreateAsync(new CreateProductRequest(
                SellerId: seller,
                CategoryId: categoryId,
                Name: "Item 2",
                SaleType: SaleType.Regular,
                Variants: new List<CreateVariantRequest>
                {
                    new("shop-item-01", "Default", 60000m)
                },
                WeightGrams: 100)));

        Assert.Equal("DUPLICATE_SKU", ex.ErrorCode);
    }

    [Fact]
    public async Task CreateProduct_BlankSku_GeneratesUniqueSkuCode()
    {
        await using var db = CreateDb();
        var categoryId = await SeedCategoryAsync(db);
        var seller = Guid.NewGuid();
        var service = CreateService(db);

        var product = await service.CreateAsync(new CreateProductRequest(
            SellerId: seller,
            CategoryId: categoryId,
            Name: "Gối ôm công thái học",
            SaleType: SaleType.Regular,
            Variants: new List<CreateVariantRequest>
            {
                new("   ", "Màu xám", 50000m),
                new(null, "Màu xanh", 60000m)
            },
            WeightGrams: 100));

        var detail = await service.GetInternalProductDetailAsync(product.Id);
        Assert.NotNull(detail);
        Assert.Equal(2, detail.Variants.Count);
        Assert.All(detail.Variants, v => Assert.False(string.IsNullOrWhiteSpace(v.SkuCode)));
        Assert.Contains("GOI-OM", detail.Variants[0].SkuCode);
        Assert.NotEqual(detail.Variants[0].SkuCode, detail.Variants[1].SkuCode);
    }

    [Fact]
    public async Task AddVariant_BlankSku_GeneratesUniqueSkuCode()
    {
        await using var db = CreateDb();
        var categoryId = await SeedCategoryAsync(db);
        var seller = Guid.NewGuid();
        var service = CreateService(db);

        var product = await service.CreateAsync(new CreateProductRequest(
            SellerId: seller,
            CategoryId: categoryId,
            Name: "Bàn phím cơ",
            SaleType: SaleType.Regular,
            Variants: new List<CreateVariantRequest>
            {
                new("KB-01", "Switch Red", 50000m)
            },
            WeightGrams: 500));

        var variant = await service.AddVariantAsync(product.Id, seller, new CreateVariantRequest(
            SkuCode: null,
            Name: "Switch Blue",
            Price: 55000m));

        Assert.NotNull(variant);
        Assert.False(string.IsNullOrWhiteSpace(variant.SkuCode));
        Assert.Contains("BAN-PHIM", variant.SkuCode);
        Assert.NotEqual("KB-01", variant.SkuCode);
    }

    [Fact]
    public async Task SuggestSku_GeneratesUniqueSkuCode()
    {
        await using var db = CreateDb();
        var seller = Guid.NewGuid();
        var service = CreateService(db);

        var response = await service.SuggestSkuAsync(seller, "Tai nghe không dây", "Màu đen");
        Assert.NotNull(response);
        Assert.False(string.IsNullOrWhiteSpace(response.SkuCode));
        Assert.Contains("TAI-NGHE", response.SkuCode);
    }

    [Fact]
    public async Task CreateAuctionProduct_DuplicateSkuInSameShop_ThrowsConflict()
    {
        await using var db = CreateDb();
        var categoryId = await SeedCategoryAsync(db);
        var seller = Guid.NewGuid();
        var service = CreateService(db);

        await service.CreateAuctionProductAsync(new CreateAuctionProductRequest(
            CategoryId: categoryId,
            Name: "Auction 1",
            SkuCode: "AUC-SHOP-01",
            WeightGrams: 300), seller);

        var ex = await Assert.ThrowsAsync<BusinessRuleValidationException>(() =>
            service.CreateAuctionProductAsync(new CreateAuctionProductRequest(
                CategoryId: categoryId,
                Name: "Auction 2",
                SkuCode: "auc-shop-01",
                WeightGrams: 300), seller));

        Assert.Equal("DUPLICATE_SKU", ex.ErrorCode);
    }

    [Fact]
    public async Task UpdateVariant_DraftAndRejected_AllowsSkuChange()
    {
        await using var db = CreateDb();
        var seller = Guid.NewGuid();
        var draftVariant = await SeedVariantAsync(db, seller, ProductStatus.Draft, null, "OLD-DRAFT-SKU", false);
        var rejectedVariant = await SeedVariantAsync(db, seller, ProductStatus.Inactive, ProductInactiveReason.Rejected, "OLD-REJ-SKU", false);
        var service = CreateService(db);

        var updatedDraft = await service.UpdateVariantAsync(
            draftVariant.Id, seller, new UpdateVariantRequest("V1", 100000m, SkuCode: "NEW-DRAFT-SKU"));
        var updatedRejected = await service.UpdateVariantAsync(
            rejectedVariant.Id, seller, new UpdateVariantRequest("V2", 100000m, SkuCode: "NEW-REJ-SKU"));

        Assert.Equal("NEW-DRAFT-SKU", updatedDraft.SkuCode);
        Assert.Equal("NEW-REJ-SKU", updatedRejected.SkuCode);
    }

    [Fact]
    public async Task UpdateVariant_InactiveNonRejected_BlocksSkuChange()
    {
        await using var db = CreateDb();
        var seller = Guid.NewGuid();
        var deactivated = await SeedVariantAsync(db, seller, ProductStatus.Inactive, ProductInactiveReason.SellerPaused, "LOCKED-SKU", false);
        var service = CreateService(db);

        var ex = await Assert.ThrowsAsync<BusinessRuleValidationException>(() =>
            service.UpdateVariantAsync(deactivated.Id, seller, new UpdateVariantRequest("V1", 100000m, SkuCode: "NEW-SKU")));

        Assert.Equal("SKU_LOCKED_BY_STATUS", ex.ErrorCode);
        Assert.Equal("Sku code can only be changed while the product is in draft or rejected status.", ex.Message);
    }

    [Fact]
    public async Task UpdateVariant_WithOrder_BlocksSkuChangeWithSentenceCaseMessage()
    {
        await using var db = CreateDb();
        var seller = Guid.NewGuid();
        var ordered = await SeedVariantAsync(db, seller, ProductStatus.Draft, null, "ORDERED-SKU", true);
        var service = CreateService(db);

        var ex = await Assert.ThrowsAsync<BusinessRuleValidationException>(() =>
            service.UpdateVariantAsync(ordered.Id, seller, new UpdateVariantRequest("V1", 100000m, SkuCode: "NEW-SKU")));

        Assert.Equal("SKU_LOCKED_BY_ORDER", ex.ErrorCode);
        Assert.Equal("Cannot change sku code after an order has been placed.", ex.Message);
    }

    [Fact]
    public async Task SearchAsync_MatchesActiveVariantSkuCode()
    {
        await using var db = CreateDb();
        var seller = Guid.NewGuid();
        var activeVariant = await SeedVariantAsync(db, seller, ProductStatus.Active, null, "FINDME-XYZ-99", false);
        var service = CreateService(db);

        var result = await service.SearchAsync(new ProductListQuery(Keyword: "findme-xyz-99"));

        Assert.Single(result.Items);
        Assert.Equal(activeVariant.ProductId, result.Items[0].ProductId);
    }

    [Fact]
    public async Task CreateProduct_SoftDeletedVariant_AllowsReusingSkuInSameShop()
    {
        await using var db = CreateDb();
        var categoryId = await SeedCategoryAsync(db);
        var seller = Guid.NewGuid();
        var deletedVariant = await SeedVariantAsync(db, seller, ProductStatus.Draft, null, "REUSE-SKU-01", false);
        deletedVariant.IsDeleted = true;
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var created = await service.CreateAsync(new CreateProductRequest(
            SellerId: seller,
            CategoryId: categoryId,
            Name: "Recreated Item",
            SaleType: SaleType.Regular,
            Variants: new List<CreateVariantRequest>
            {
                new("REUSE-SKU-01", "Default", 90000m)
            },
            WeightGrams: 150));

        Assert.NotEqual(Guid.Empty, created.Id);
    }

    [Fact]
    public async Task UpdateVariant_BlankSkuCode_ThrowsValidationError()
    {
        await using var db = CreateDb();
        var seller = Guid.NewGuid();
        var draftVariant = await SeedVariantAsync(db, seller, ProductStatus.Draft, null, "KEEP-SKU-01", false);
        var service = CreateService(db);

        var ex = await Assert.ThrowsAsync<BusinessRuleValidationException>(() =>
            service.UpdateVariantAsync(draftVariant.Id, seller, new UpdateVariantRequest("V1", 100000m, SkuCode: "   ")));

        Assert.Equal("VAL_INVALID_INPUT", ex.ErrorCode);
        Assert.Equal("Sku code cannot be blank.", ex.Message);
    }

    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static ProductService CreateService(AppDbContext db) =>
        new(
            db,
            NoOp<IFulfillmentClient>.Create(),
            NoOp<IImageStorageService>.Create(),
            NoOp<IUserClient>.Create(),
            NoOp<INotificationClient>.Create(),
            NullLogger<ProductService>.Instance,
            NoOp<IProductInventoryInitializer>.Create());

    private static async Task<Guid> SeedCategoryAsync(AppDbContext db)
    {
        var id = Guid.NewGuid();
        db.Categories.Add(new Category { Id = id, Name = "Electronics", Level = 1, IsDisabled = false });
        await db.SaveChangesAsync();
        return id;
    }

    private static async Task<ProductVariant> SeedVariantAsync(
        AppDbContext db,
        Guid sellerId,
        ProductStatus status,
        ProductInactiveReason? reason,
        string skuCode,
        bool hasOrder)
    {
        var product = new Product
        {
            Id = Guid.NewGuid(),
            SellerId = sellerId,
            CategoryId = Guid.NewGuid(),
            Name = "Test Product",
            SaleType = SaleType.Regular,
            Status = status,
            StatusReason = reason,
            WeightGrams = 200
        };
        var variant = new ProductVariant
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            SellerId = sellerId,
            SkuCode = skuCode,
            Name = "Default",
            Price = 100000m,
            Currency = "VND",
            IsActive = true,
            HasOrder = hasOrder,
            Status = VariantStatus.Active
        };
        db.Products.Add(product);
        db.ProductVariants.Add(variant);
        await db.SaveChangesAsync();
        return variant;
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
