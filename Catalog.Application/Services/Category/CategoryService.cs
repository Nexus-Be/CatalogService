namespace Catalog.Application.Services.Category;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Catalog.Application.Common;
using Catalog.Application.Common.Models;
using Catalog.Application.Interfaces;
using Catalog.Domain.Exceptions;

public sealed class CategoryService : ICategoryService
{
    private readonly ICatalogDbContext _dbContext;

    public CategoryService(ICatalogDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<CategoryDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var category = await _dbContext.Categories
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new EntityNotFoundException("CATEGORY_NOT_FOUND", "Category", id);

        return new CategoryDto(
            category.Id,
            category.ParentId,
            category.Name,
            category.Description,
            category.Level,
            category.SortOrder,
            category.IsDisabled);
    }

    public async Task<PagedData<CategoryDto>> ListAllAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var (safePage, safePageSize) = PaginationConstants.Normalize(page, pageSize);

        var query = _dbContext.Categories.AsNoTracking();

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(c => c.SortOrder)
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .Select(category => new CategoryDto(
                category.Id,
                category.ParentId,
                category.Name,
                category.Description,
                category.Level,
                category.SortOrder,
                category.IsDisabled))
            .ToListAsync(cancellationToken);

        return new PagedData<CategoryDto>(items, PaginationMetadata.Create(safePage, safePageSize, totalCount));
    }
}
