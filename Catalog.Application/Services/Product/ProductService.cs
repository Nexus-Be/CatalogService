namespace Catalog.Application.Services.Product;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Catalog.Application.Interfaces;

public sealed class ProductService : IProductService
{
    private readonly ICatalogDbContext _dbContext;

    public ProductService(ICatalogDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ProductDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var product = await _dbContext.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (product == null) return null;

        return new ProductDto(
            product.Id,
            product.SellerId,
            product.CategoryId,
            product.Name,
            product.Description,
            product.Brand,
            product.Status.ToString());
    }

    public async Task<IReadOnlyList<ProductDto>> ListAsync(int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
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

        return products;
    }
}
