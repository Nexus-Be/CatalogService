namespace Catalog.Application.Services.Product;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Catalog.Application.Common.Models;
using Catalog.Application.Interfaces;
using Catalog.Domain.Entities;
using Catalog.Domain.Enums;
using Catalog.Domain.Exceptions;

public sealed class ProductService : IProductService
{
    private readonly ICatalogDbContext _dbContext;
    private readonly IFulfillmentInternalClient _fulfillmentClient;

    public ProductService(
        ICatalogDbContext dbContext,
        IFulfillmentInternalClient fulfillmentClient)
    {
        _dbContext = dbContext;
        _fulfillmentClient = fulfillmentClient;
    }

    public async Task<ProductDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var product = await _dbContext.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new EntityNotFoundException("PRODUCT_NOT_FOUND", "Product", id);

        return new ProductDto(
            product.Id,
            product.SellerId,
            product.CategoryId,
            product.Name,
            product.Description,
            product.Brand,
            product.Status.ToString());
    }

    public async Task<PagedData<ProductDto>> ListAsync(int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var totalCount = await _dbContext.Products.CountAsync(cancellationToken);

        var products = await _dbContext.Products
            .AsNoTracking()
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(product => new ProductDto(
                product.Id,
                product.SellerId,
                product.CategoryId,
                product.Name,
                product.Description,
                product.Brand,
                product.Status.ToString()))
            .ToListAsync(cancellationToken);

        return new PagedData<ProductDto>(products, PaginationMetadata.Create(page, pageSize, totalCount));
    }

    public async Task<ProductDto> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken = default)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Name))
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "Product name cannot be empty.");
        }

        var product = new Product
        {
            Id = Guid.NewGuid(),
            SellerId = request.SellerId,
            CategoryId = request.CategoryId,
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            Brand = request.Brand?.Trim(),
            SaleType = SaleType.Regular,
            Status = ProductStatus.Draft,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _dbContext.Products.Add(product);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _fulfillmentClient.InitializeInventoryAsync(
            new InitializeInventoryClientRequest(product.Id, null, 0),
            cancellationToken);

        return new ProductDto(
            product.Id,
            product.SellerId,
            product.CategoryId,
            product.Name,
            product.Description,
            product.Brand,
            product.Status.ToString());
    }
}
