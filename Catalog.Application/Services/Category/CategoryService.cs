namespace Catalog.Application.Services.Category;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Catalog.Application.Interfaces;

public sealed class CategoryService : ICategoryService
{
    private readonly ICatalogDbContext _dbContext;

    public CategoryService(ICatalogDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<CategoryDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var category = await _dbContext.Categories
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (category == null) return null;

        return new CategoryDto(
            category.Id,
            category.ParentId,
            category.Name,
            category.Description,
            category.Level,
            category.SortOrder,
            category.IsDisabled);
    }

    public async Task<IReadOnlyList<CategoryDto>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        var categories = await _dbContext.Categories
            .AsNoTracking()
            .OrderBy(c => c.SortOrder)
            .Select(category => new CategoryDto(
                category.Id,
                category.ParentId,
                category.Name,
                category.Description,
                category.Level,
                category.SortOrder,
                category.IsDisabled))
            .ToListAsync(cancellationToken);

        return categories;
    }
}
