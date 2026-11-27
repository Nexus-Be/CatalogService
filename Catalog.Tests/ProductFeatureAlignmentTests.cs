namespace Catalog.Tests;

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Catalog.Application.Interfaces;
using Catalog.Application.Services.Product;
using Catalog.Domain.Entities;
using Catalog.Domain.Enums;
using Catalog.Domain.Exceptions;
using Catalog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

public sealed class ProductFeatureAlignmentTests
{
    private static readonly Guid SellerId = Guid.NewGuid();
    private static readonly Guid AdminId = Guid.NewGuid();

    [Fact]
    public async Task GetInternalAndAdminProductDetail_ReturnProductInDraftOrInactiveStatus()
    {
        await using var db = CreateDb();
        var category = new Category
        {
            Id = Guid.NewGuid(),
            Name = "Vehicles",
            Level = 1
        };
        db.Categories.Add(category);

        var product = new Product
        {
            Id = Guid.NewGuid(),
            SellerId = SellerId,
            CategoryId = category.Id,
            Name = "Draft Motorcycle",
            SaleType = SaleType.Regular,
            Status = ProductStatus.Draft,
            LengthCm = 200,
            WidthCm = 80,
            HeightCm = 110
        };
        db.Products.Add(product);
        await db.SaveChangesAsync();

        var service = CreateService(db);

        // Internal detail should find it
        var internalDetail = await service.GetInternalProductDetailAsync(product.Id);
        Assert.NotNull(internalDetail);
        Assert.Equal(product.Id, internalDetail.ProductId);
        Assert.Equal("Draft Motorcycle", internalDetail.Name);
        Assert.Equal(200, internalDetail.LengthCm);
        Assert.Equal(80, internalDetail.WidthCm);
        Assert.Equal(110, internalDetail.HeightCm);

        // Admin detail should find it
        var adminDetail = await service.GetAdminProductDetailAsync(product.Id);
        Assert.NotNull(adminDetail);
        Assert.Equal(product.Id, adminDetail.ProductId);

        // Public detail should throw EntityNotFoundException because Status is Draft
        await Assert.ThrowsAsync<EntityNotFoundException>(() => service.GetDetailAsync(product.Id));
    }

    [Fact]
    public async Task DeleteVariantImage_WhenImageBelongsToAnotherVariant_ThrowsInvalidInput()
    {
        await using var db = CreateDb();
        var product = new Product
        {
            Id = Guid.NewGuid(),
            SellerId = SellerId,
            CategoryId = Guid.NewGuid(),
            Name = "Bike Helmet",
            SaleType = SaleType.Regular,
            Status = ProductStatus.Active
        };
        db.Products.Add(product);

        var variantA = new ProductVariant
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            SkuCode = "HELMET-RED",
            Name = "Red Helmet",
            Status = VariantStatus.Active
        };
        var variantB = new ProductVariant
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            SkuCode = "HELMET-BLUE",
            Name = "Blue Helmet",
            Status = VariantStatus.Active
        };
        db.ProductVariants.AddRange(variantA, variantB);

        var imageA = new ProductImage
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            VariantId = variantA.Id,
            Url = "https://img.test/helmet-red.jpg",
            IsActive = true
        };
        db.ProductImages.Add(imageA);
        await db.SaveChangesAsync();

        var service = CreateService(db);

        // Try deleting imageA using variantB ID -> should throw VAL_INVALID_INPUT
        var ex = await Assert.ThrowsAsync<BusinessRuleValidationException>(() =>
            service.DeleteVariantImageAsync(variantB.Id, imageA.Id, SellerId));

        Assert.Equal("VAL_INVALID_INPUT", ex.ErrorCode);
    }

    [Fact]
    public async Task MarkVariantsOrdered_SetsHasOrderToTrue_Idempotently()
    {
        await using var db = CreateDb();
        var product = new Product
        {
            Id = Guid.NewGuid(),
            SellerId = SellerId,
            CategoryId = Guid.NewGuid(),
            Name = "Tire",
            SaleType = SaleType.Regular,
            Status = ProductStatus.Active
        };
        db.Products.Add(product);

        var variant1 = new ProductVariant
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            SkuCode = "TIRE-01",
            Name = "Tire 1",
            HasOrder = false,
            Status = VariantStatus.Active
        };
        var variant2 = new ProductVariant
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            SkuCode = "TIRE-02",
            Name = "Tire 2",
            HasOrder = true, // already ordered
            Status = VariantStatus.Active
        };
        db.ProductVariants.AddRange(variant1, variant2);
        await db.SaveChangesAsync();

        var service = CreateService(db);

        // First call: variant1 transitions from false -> true. variant2 remains true.
        var response1 = await service.MarkVariantsOrderedAsync(
            new MarkVariantsOrderedRequest(new List<Guid> { variant1.Id, variant2.Id }));

        Assert.Equal(1, response1.UpdatedCount);
        Assert.Single(response1.UpdatedVariantIds, variant1.Id);

        // Second call: both are already HasOrder=true, so UpdatedCount is 0
        var response2 = await service.MarkVariantsOrderedAsync(
            new MarkVariantsOrderedRequest(new List<Guid> { variant1.Id, variant2.Id }));

        Assert.Equal(0, response2.UpdatedCount);
        Assert.Empty(response2.UpdatedVariantIds);
    }

    [Theory]
    [InlineData("BAN")]
    [InlineData("SUSPEND")]
    [InlineData("REJECT")]
    public async Task ModerateProduct_WhenInAuction_ThrowsAuctionForceCancelRequired(string action)
    {
        await using var db = CreateDb();
        var product = new Product
        {
            Id = Guid.NewGuid(),
            SellerId = SellerId,
            CategoryId = Guid.NewGuid(),
            Name = "Rare Vintage Watch",
            SaleType = SaleType.Auction,
            Status = ProductStatus.InAuction,
            WeightGrams = 200
        };
        db.Products.Add(product);
        await db.SaveChangesAsync();

        var service = CreateService(db);

        var ex = await Assert.ThrowsAsync<BusinessRuleValidationException>(() =>
            service.ModerateProductAsync(product.Id, new ModerateProductRequest(action, "Violation detected"), AdminId));

        Assert.Equal("AUCTION_FORCE_CANCEL_REQUIRED", ex.ErrorCode);
    }

    [Fact]
    public async Task ModerateProduct_WhenSuspendedThenUnlocked_ReactivatesProductAndVariants()
    {
        await using var db = CreateDb();
        var product = new Product
        {
            Id = Guid.NewGuid(),
            SellerId = SellerId,
            CategoryId = Guid.NewGuid(),
            Name = "Multi-Variant Test Product",
            SaleType = SaleType.Regular,
            Status = ProductStatus.PendingApproval,
            WeightGrams = 500,
            Variants = new List<ProductVariant>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    ProductId = Guid.NewGuid(),
                    SellerId = SellerId,
                    SkuCode = "TEST-SKU-1",
                    Name = "Variant 1",
                    Price = 100000,
                    Status = VariantStatus.Active,
                    WeightGrams = 500
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    ProductId = Guid.NewGuid(),
                    SellerId = SellerId,
                    SkuCode = "TEST-SKU-2",
                    Name = "Variant 2",
                    Price = 200000,
                    Status = VariantStatus.Active,
                    WeightGrams = 500
                }
            },
            Images = new List<ProductImage>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    ProductId = Guid.NewGuid(),
                    Url = "https://example.com/image.jpg",
                    IsActive = true,
                    SortOrder = 0
                }
            }
        };
        db.Products.Add(product);
        await db.SaveChangesAsync();

        var service = CreateService(db);

        // 1. Admin suspends product
        var suspendResult = await service.ModerateProductAsync(
            product.Id,
            new ModerateProductRequest("SUSPEND", "Suspicious product description"),
            AdminId);

        Assert.Equal(ProductStatus.Inactive.ToString(), suspendResult.Status);
        Assert.All(product.Variants, v => Assert.Equal(VariantStatus.Inactive, v.Status));

        // 2. Admin unlocks product
        var unlockResult = await service.ModerateProductAsync(
            product.Id,
            new ModerateProductRequest("UNLOCK", "Seller provided requested documents"),
            AdminId);

        Assert.Equal(ProductStatus.Active.ToString(), unlockResult.Status);
        Assert.All(product.Variants, v => Assert.Equal(VariantStatus.Active, v.Status));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public async Task CreateVariant_WithNonPositiveDimensions_ThrowsValidationException(int dimension)
    {
        await using var db = CreateDb();
        var product = new Product
        {
            Id = Guid.NewGuid(),
            SellerId = SellerId,
            CategoryId = Guid.NewGuid(),
            Name = "Shipping Box",
            SaleType = SaleType.Regular,
            Status = ProductStatus.Draft
        };
        db.Products.Add(product);
        await db.SaveChangesAsync();

        var service = CreateService(db);

        await Assert.ThrowsAsync<BusinessRuleValidationException>(() =>
            service.AddVariantAsync(product.Id, SellerId, new CreateVariantRequest(
                SkuCode: "BOX-01",
                Name: "Small Box",
                Price: 10000,
                LengthCm: dimension)));

        await Assert.ThrowsAsync<BusinessRuleValidationException>(() =>
            service.AddVariantAsync(product.Id, SellerId, new CreateVariantRequest(
                SkuCode: "BOX-02",
                Name: "Small Box",
                Price: 10000,
                WidthCm: dimension)));

        await Assert.ThrowsAsync<BusinessRuleValidationException>(() =>
            service.AddVariantAsync(product.Id, SellerId, new CreateVariantRequest(
                SkuCode: "BOX-03",
                Name: "Small Box",
                Price: 10000,
                HeightCm: dimension)));
    }

    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase($"catalog-align-{Guid.NewGuid():N}").Options);

    private static ProductService CreateService(AppDbContext db) =>
        new(db,
            NoOp<IFulfillmentClient>.Create(),
            NoOp<IImageStorageService>.Create(),
            NoOp<IUserClient>.Create(),
            NoOp<INotificationClient>.Create(),
            NullLogger<ProductService>.Instance,
            NoOp<IProductInventoryInitializer>.Create());

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
