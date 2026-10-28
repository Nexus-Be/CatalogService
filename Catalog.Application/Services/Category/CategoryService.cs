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

    public async Task<IReadOnlyList<CategoryItemDto>> GetCategoriesAsync(CategoryListQuery query, CancellationToken cancellationToken = default)
{
    // 1. Lọc danh mục chưa xóa và đang hoạt động
    var categoriesQuery = _dbContext.Categories
        .AsNoTracking()
        .Where(c => !c.IsDeleted && !c.IsDisabled);
    if (!string.IsNullOrWhiteSpace(query.Keyword))
    {
        var kw = query.Keyword.Trim().ToLower();
        categoriesQuery = categoriesQuery.Where(c => c.Name.ToLower().Contains(kw));
    }
    var allCategories = await categoriesQuery
        .OrderBy(c => c.SortOrder)
        .ThenBy(c => c.Name)
        .Select(c => new CategoryItemDto(
            c.Id,
            c.ParentId,
            c.Name,
            c.Description,
            c.Level,
            c.SortOrder,
            new List<CategoryItemDto>()))
        .ToListAsync(cancellationToken);
    // 2. Nếu không yêu cầu dạng cây (Tree = false) -> Trả về danh sách phẳng
    if (!query.Tree)
    {
        if (query.ParentId.HasValue)
        {
            return allCategories.Where(c => c.ParentId == query.ParentId.Value).ToList();
        }
        return allCategories;
    }
    // 3. Nếu yêu cầu dạng cây (Tree = true) -> Dựng cây cha-con
    var lookup = allCategories.ToLookup(c => c.ParentId);
    // Gán danh sách con cho từng danh mục
    foreach (var cat in allCategories)
    {
        cat.Children?.AddRange(lookup[cat.CategoryId]);
    }
    // Nếu có truyền ParentId -> Lấy các con của ParentId đó
    // Nếu không truyền -> Lấy các danh mục gốc (ParentId == null)
    var rootCategories = lookup[query.ParentId].ToList();
    return rootCategories;
}
}
