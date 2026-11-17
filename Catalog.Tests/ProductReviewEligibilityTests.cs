namespace Catalog.Tests;

using System.Reflection;
using Catalog.Application.Interfaces;
using Catalog.Application.Services.Review;
using Catalog.Domain.Entities;
using Catalog.Domain.Enums;
using Catalog.Domain.Exceptions;
using Catalog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

/// <summary>
/// A product review belongs to one product of one sub-order (the purchase). The buyer's screens ask Catalog, not UserService,
/// whether that review can still be written, and which review of theirs is which.
/// </summary>
public sealed class ProductReviewEligibilityTests
{
    private static readonly Guid Buyer = Guid.NewGuid();

    [Fact]
    public async Task ACompletedPurchase_CanBeReviewed_UntilTheWindowCloses()
    {
        await using var db = CreateDb();
        var product = await SeedProduct(db);
        var completed = DateTimeOffset.UtcNow.AddDays(-2);
        var sub = Guid.NewGuid();
        await SeedPurchase(db, product, sub, completed);

        var result = await CreateService(db).GetEligibilityAsync(product.Id, sub, Buyer);

        Assert.True(result.CanReview);
        Assert.False(result.AlreadyReviewed);
        Assert.Null(result.Reason);
        Assert.Null(result.ReviewId);
        Assert.Equal(completed.AddDays(ProductReviewService.MaxReviewPeriodDays), result.ExpiresAt);
    }

    [Fact]
    public async Task AfterReviewing_ItSaysAlreadyReviewed_AndNamesTheReview()
    {
        await using var db = CreateDb();
        var product = await SeedProduct(db);
        var sub = Guid.NewGuid();
        var purchase = await SeedPurchase(db, product, sub, DateTimeOffset.UtcNow.AddDays(-1));
        var review = await SeedReview(db, product, purchase, 4);

        var result = await CreateService(db).GetEligibilityAsync(product.Id, sub, Buyer);

        Assert.False(result.CanReview);
        Assert.True(result.AlreadyReviewed);
        Assert.Equal("ALREADY_REVIEWED", result.Reason);
        Assert.Equal(review.Id, result.ReviewId);
    }

    [Fact]
    public async Task TheSameProductInAnotherSubOrder_IsAnotherPurchase_AndStaysOpen()
    {
        await using var db = CreateDb();
        var product = await SeedProduct(db);
        var firstSub = Guid.NewGuid();
        var secondSub = Guid.NewGuid();
        var first = await SeedPurchase(db, product, firstSub, DateTimeOffset.UtcNow.AddDays(-3));
        await SeedPurchase(db, product, secondSub, DateTimeOffset.UtcNow.AddDays(-1));
        await SeedReview(db, product, first, 5);

        var service = CreateService(db);
        var reviewed = await service.GetEligibilityAsync(product.Id, firstSub, Buyer);
        var open = await service.GetEligibilityAsync(product.Id, secondSub, Buyer);

        Assert.True(reviewed.AlreadyReviewed);
        Assert.True(open.CanReview);                              // reviewing one sub-order does not lock the other
        Assert.False(open.AlreadyReviewed);
    }

    [Fact]
    public async Task ThePurchaseId_IsTheSubOrder_NotTheParentOrder()
    {
        await using var db = CreateDb();
        var product = await SeedProduct(db);
        await SeedPurchase(db, product, Guid.NewGuid(), DateTimeOffset.UtcNow.AddDays(-1));

        var parentOrderId = Guid.NewGuid();
        var result = await CreateService(db).GetEligibilityAsync(product.Id, parentOrderId, Buyer);

        Assert.False(result.CanReview);
        Assert.Equal("PURCHASE_NOT_FOUND", result.Reason);
    }

    [Fact]
    public async Task AnotherBuyer_CannotSeeOrUsePurchase()
    {
        await using var db = CreateDb();
        var product = await SeedProduct(db);
        var sub = Guid.NewGuid();
        await SeedPurchase(db, product, sub, DateTimeOffset.UtcNow.AddDays(-1));

        var result = await CreateService(db).GetEligibilityAsync(product.Id, sub, Guid.NewGuid());

        Assert.Equal("PURCHASE_NOT_FOUND", result.Reason);
        Assert.False(result.CanReview);
    }

    [Theory]
    [InlineData(31, "REVIEW_WINDOW_EXPIRED")]
    [InlineData(29, null)]
    public async Task TheWindow_IsThirtyDaysFromCompletion(int daysAgo, string? reason)
    {
        await using var db = CreateDb();
        var product = await SeedProduct(db);
        var sub = Guid.NewGuid();
        await SeedPurchase(db, product, sub, DateTimeOffset.UtcNow.AddDays(-daysAgo));

        var result = await CreateService(db).GetEligibilityAsync(product.Id, sub, Buyer);

        Assert.Equal(reason, result.Reason);
        Assert.Equal(reason is null, result.CanReview);
    }

    [Fact]
    public async Task ABannedProduct_CannotBeReviewed()
    {
        await using var db = CreateDb();
        var product = await SeedProduct(db, ProductStatus.Banned);
        var sub = Guid.NewGuid();
        await SeedPurchase(db, product, sub, DateTimeOffset.UtcNow.AddDays(-1));

        var result = await CreateService(db).GetEligibilityAsync(product.Id, sub, Buyer);

        Assert.Equal("PRODUCT_BANNED", result.Reason);
    }

    [Fact]
    public async Task AnUnknownProduct_IsNotFound()
    {
        await using var db = CreateDb();

        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            CreateService(db).GetEligibilityAsync(Guid.NewGuid(), Guid.NewGuid(), Buyer));
    }

    // --- my reviews --------------------------------------------------------------------------------------------------

    [Fact]
    public async Task MyReviews_ListsOnlyMyReviewsOfThisProduct_WithTheirSubOrders()
    {
        await using var db = CreateDb();
        var product = await SeedProduct(db);
        var other = await SeedProduct(db);
        var firstSub = Guid.NewGuid();
        var secondSub = Guid.NewGuid();
        var first = await SeedPurchase(db, product, firstSub, DateTimeOffset.UtcNow.AddDays(-5));
        var second = await SeedPurchase(db, product, secondSub, DateTimeOffset.UtcNow.AddDays(-2));
        var elsewhere = await SeedPurchase(db, other, Guid.NewGuid(), DateTimeOffset.UtcNow.AddDays(-2));
        var someoneElses = await SeedPurchase(db, product, Guid.NewGuid(), DateTimeOffset.UtcNow.AddDays(-2), Guid.NewGuid());
        var firstReview = await SeedReview(db, product, first, 3, createdAt: DateTimeOffset.UtcNow.AddDays(-4));
        var secondReview = await SeedReview(db, product, second, 5, createdAt: DateTimeOffset.UtcNow.AddDays(-1));
        await SeedReview(db, other, elsewhere, 1);
        await SeedReview(db, product, someoneElses, 2, buyer: someoneElses.BuyerId);

        var mine = await CreateService(db).GetMyReviewsAsync(product.Id, Buyer);

        Assert.Equal(new[] { secondReview.Id, firstReview.Id }, mine.Select(r => r.ReviewId));
        Assert.Equal(new[] { secondSub, firstSub }, mine.Select(r => r.OrderId));
        Assert.Equal(new short[] { 5, 3 }, mine.Select(r => r.Rating));
    }

    [Fact]
    public async Task MyReviews_SkipsDeletedReviews()
    {
        await using var db = CreateDb();
        var product = await SeedProduct(db);
        var purchase = await SeedPurchase(db, product, Guid.NewGuid(), DateTimeOffset.UtcNow.AddDays(-1));
        var review = await SeedReview(db, product, purchase, 4);
        review.IsDeleted = true;
        await db.SaveChangesAsync();

        var service = CreateService(db);

        Assert.Empty(await service.GetMyReviewsAsync(product.Id, Buyer));
        Assert.False((await service.GetEligibilityAsync(product.Id, purchase.OrderId, Buyer)).AlreadyReviewed);
    }

    // --- helpers -----------------------------------------------------------------------------------------------------

    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase($"catalog-review-{Guid.NewGuid():N}").Options);

    private static ProductReviewService CreateService(AppDbContext db) =>
        new(db,
            NoOp<IImageStorageService>.Create(),
            NoOp<IUserClient>.Create(),
            NoOp<INotificationClient>.Create(),
            NullLogger<ProductReviewService>.Instance);

    private static async Task<Product> SeedProduct(AppDbContext db, ProductStatus status = ProductStatus.Active)
    {
        var product = new Product
        {
            Id = Guid.NewGuid(), SellerId = Guid.NewGuid(), CategoryId = Guid.NewGuid(), Name = "Vase",
            SaleType = SaleType.Regular, Status = status
        };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        return product;
    }

    private static async Task<PurchaseRecord> SeedPurchase(
        AppDbContext db, Product product, Guid subOrderId, DateTimeOffset completedAt, Guid? buyer = null)
    {
        var purchase = new PurchaseRecord
        {
            Id = Guid.NewGuid(), BuyerId = buyer ?? Buyer, OrderId = subOrderId, ProductId = product.Id, CompletedAt = completedAt
        };
        db.PurchaseRecords.Add(purchase);
        await db.SaveChangesAsync();
        return purchase;
    }

    private static async Task<ProductReview> SeedReview(
        AppDbContext db, Product product, PurchaseRecord purchase, short rating, DateTimeOffset? createdAt = null, Guid? buyer = null)
    {
        var review = new ProductReview
        {
            Id = Guid.NewGuid(), PurchaseRecordId = purchase.Id, ProductId = product.Id, BuyerId = buyer ?? Buyer,
            Rating = rating, Status = ReviewStatus.Published
        };
        if (createdAt is { } at)
            review.CreatedAt = at;
        db.ProductReviews.Add(review);
        await db.SaveChangesAsync();
        return review;
    }

    /// <summary>Do-nothing implementation of an interface: void returns nothing, Task completes, Task&lt;T&gt; returns default.</summary>
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
