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

public sealed class ProductUpdateTests
{
    private static readonly Guid SellerId = Guid.NewGuid();

    [Fact]
    public async Task Update_WithWeight_SavesTheShippingWeight()
    {
        await using var db = CreateDb();
        var product = await SeedProductAsync(db, weightGrams: null);

        var result = await CreateService(db).UpdateAsync(
            product.Id, new UpdateProductRequest("Helmet", WeightGrams: 750), SellerId);

        Assert.Equal(750, result.WeightGrams);
        Assert.Equal(750, (await db.Products.FindAsync(product.Id))!.WeightGrams);
    }

    [Fact]
    public async Task Update_WithoutWeight_KeepsTheExistingWeight()
    {
        await using var db = CreateDb();
        var product = await SeedProductAsync(db, weightGrams: 500);

        await CreateService(db).UpdateAsync(product.Id, new UpdateProductRequest("Helmet renamed"), SellerId);

        var saved = (await db.Products.FindAsync(product.Id))!;
        Assert.Equal(500, saved.WeightGrams);
        Assert.Equal("Helmet renamed", saved.Name);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task Update_WithNonPositiveWeight_IsRejected(int weight)
    {
        await using var db = CreateDb();
        var product = await SeedProductAsync(db, weightGrams: 500);

        await Assert.ThrowsAsync<BusinessRuleValidationException>(() => CreateService(db).UpdateAsync(
            product.Id, new UpdateProductRequest("Helmet", WeightGrams: weight), SellerId));

        Assert.Equal(500, (await db.Products.FindAsync(product.Id))!.WeightGrams);
    }

    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase($"catalog-update-{Guid.NewGuid():N}").Options);

    private static ProductService CreateService(AppDbContext db) =>
        new(db,
            NoOp<IFulfillmentClient>.Create(),
            NoOp<IImageStorageService>.Create(),
            NoOp<IUserClient>.Create(),
            NoOp<INotificationClient>.Create(),
            NullLogger<ProductService>.Instance,
            NoOp<IProductInventoryInitializer>.Create());

    private static async Task<Product> SeedProductAsync(AppDbContext db, int? weightGrams)
    {
        var product = new Product
        {
            Id = Guid.NewGuid(),
            SellerId = SellerId,
            CategoryId = Guid.NewGuid(),
            Name = "Helmet",
            SaleType = SaleType.Auction,
            Status = ProductStatus.Active,
            WeightGrams = weightGrams
        };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        return product;
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
