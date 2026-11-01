namespace Catalog.Application.Services.Category;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Catalog.Application.Common;
using Catalog.Application.Common.Extensions;
using Catalog.Application.Common.Models;
using Catalog.Application.Interfaces;
using Catalog.Domain.Entities;
using Catalog.Domain.Enums;
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
            .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted, cancellationToken)
            ?? throw new EntityNotFoundException("CATEGORY_NOT_FOUND", "Category", id);

        var children = await _dbContext.Categories
            .AsNoTracking()
            .Where(c => c.ParentId == id && !c.IsDeleted)
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .Select(c => new CategoryDto(
                c.Id,
                c.ParentId,
                c.Name,
                c.Description,
                c.Level,
                c.SortOrder,
                c.IsDisabled,
                null))
            .ToListAsync(cancellationToken);

        return new CategoryDto(
            category.Id,
            category.ParentId,
            category.Name,
            category.Description,
            category.Level,
            category.SortOrder,
            category.IsDisabled,
            children.Count > 0 ? children : null);
    }

    public async Task<PagedData<CategoryDto>> ListAllAsync(int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var (safePage, safePageSize) = PaginationConstants.Normalize(page, pageSize);

        var query = _dbContext.Categories
            .AsNoTracking()
            .Where(c => !c.IsDeleted);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .Select(category => new CategoryDto(
                category.Id,
                category.ParentId,
                category.Name,
                category.Description,
                category.Level,
                category.SortOrder,
                category.IsDisabled,
                null))
            .ToListAsync(cancellationToken);

        return new PagedData<CategoryDto>(items, PaginationMetadata.Create(safePage, safePageSize, totalCount));
    }

    public async Task<PagedData<CategoryItemDto>> GetCategoriesAsync(CategoryListQuery query, CancellationToken cancellationToken = default)
    {
        var (safePage, safePageSize) = PaginationConstants.Normalize(query.Page, query.PageSize);
        var isDesc = string.Equals(query.SortDirection, "desc", StringComparison.OrdinalIgnoreCase);

        var categoriesQuery = _dbContext.Categories
            .AsNoTracking()
            .Where(c => !c.IsDeleted && !c.IsDisabled);

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var kw = query.Keyword.Trim().ToLower();
            categoriesQuery = categoriesQuery.Where(c => c.Name.ToLower().Contains(kw));
        }

        if (!query.Tree)
        {
            if (query.ParentId.HasValue)
            {
                categoriesQuery = categoriesQuery.Where(c => c.ParentId == query.ParentId.Value);
            }

            var totalCount = await categoriesQuery.CountAsync(cancellationToken);

            IOrderedQueryable<Category> orderedQuery = (query.SortBy?.ToLowerInvariant(), isDesc) switch
            {
                ("name", false) => categoriesQuery.OrderBy(c => c.Name).ThenBy(c => c.SortOrder),
                ("name", true) => categoriesQuery.OrderByDescending(c => c.Name).ThenBy(c => c.SortOrder),
                ("level", false) => categoriesQuery.OrderBy(c => c.Level).ThenBy(c => c.SortOrder),
                ("level", true) => categoriesQuery.OrderByDescending(c => c.Level).ThenBy(c => c.SortOrder),
                ("createdat", false) => categoriesQuery.OrderBy(c => c.CreatedAt).ThenBy(c => c.SortOrder),
                ("createdat", true) => categoriesQuery.OrderByDescending(c => c.CreatedAt).ThenBy(c => c.SortOrder),
                ("sortorder", true) => categoriesQuery.OrderByDescending(c => c.SortOrder).ThenBy(c => c.Name),
                _ => categoriesQuery.OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            };

            var items = await orderedQuery
                .Skip((safePage - 1) * safePageSize)
                .Take(safePageSize)
                .Select(c => new CategoryItemDto(
                    c.Id,
                    c.ParentId,
                    c.Name,
                    c.Description,
                    c.Level,
                    c.SortOrder,
                    null))
                .ToListAsync(cancellationToken);

            return new PagedData<CategoryItemDto>(items, PaginationMetadata.Create(safePage, safePageSize, totalCount));
        }

        // Tree hierarchy: sort and paginate root categories (default pageSize = 10), then attach descendants
        var rootQuery = categoriesQuery.Where(c => c.ParentId == query.ParentId);
        var totalRootCount = await rootQuery.CountAsync(cancellationToken);

        IOrderedQueryable<Category> orderedRoots = (query.SortBy?.ToLowerInvariant(), isDesc) switch
        {
            ("name", false) => rootQuery.OrderBy(c => c.Name).ThenBy(c => c.SortOrder),
            ("name", true) => rootQuery.OrderByDescending(c => c.Name).ThenBy(c => c.SortOrder),
            ("level", false) => rootQuery.OrderBy(c => c.Level).ThenBy(c => c.SortOrder),
            ("level", true) => rootQuery.OrderByDescending(c => c.Level).ThenBy(c => c.SortOrder),
            ("createdat", false) => rootQuery.OrderBy(c => c.CreatedAt).ThenBy(c => c.SortOrder),
            ("createdat", true) => rootQuery.OrderByDescending(c => c.CreatedAt).ThenBy(c => c.SortOrder),
            ("sortorder", true) => rootQuery.OrderByDescending(c => c.SortOrder).ThenBy(c => c.Name),
            _ => rootQuery.OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
        };

        var pagedRoots = await orderedRoots
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .Select(c => new CategoryItemDto(
                c.Id,
                c.ParentId,
                c.Name,
                c.Description,
                c.Level,
                c.SortOrder,
                new List<CategoryItemDto>()))
            .ToListAsync(cancellationToken);

        if (pagedRoots.Count > 0)
        {
            var allDescendants = await _dbContext.Categories
                .AsNoTracking()
                .Where(c => !c.IsDeleted && !c.IsDisabled && c.ParentId != null)
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

            var lookup = allDescendants.ToLookup(c => c.ParentId);

            void AttachChildren(CategoryItemDto parent)
            {
                var directChildren = lookup[parent.CategoryId].ToList();
                if (directChildren.Count > 0)
                {
                    parent.Children?.AddRange(directChildren);
                    foreach (var child in directChildren)
                    {
                        AttachChildren(child);
                    }
                }
            }

            foreach (var root in pagedRoots)
            {
                AttachChildren(root);
            }
        }

        return new PagedData<CategoryItemDto>(pagedRoots, PaginationMetadata.Create(safePage, safePageSize, totalRootCount));
    }

    public async Task<CategoryDto> CreateAsync(CreateCategoryRequest request, Guid? currentUserId = null, CancellationToken cancellationToken = default)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Name))
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "Category name is required.");
        }

        var trimmedName = request.Name.Trim();
        if (trimmedName.Length > 100)
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "Category name cannot exceed 100 characters.");
        }

        var isDuplicate = await _dbContext.Categories
            .AsNoTracking()
            .AnyAsync(c => !c.IsDeleted && c.Name.ToLower() == trimmedName.ToLower(), cancellationToken);

        if (isDuplicate)
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", $"Category with name '{trimmedName}' already exists.");
        }

        short level = request.Level;
        if (request.ParentId.HasValue)
        {
            var parent = await _dbContext.Categories
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == request.ParentId.Value && !c.IsDeleted, cancellationToken)
                ?? throw new BusinessRuleValidationException("VAL_INVALID_INPUT", $"Parent category with ID '{request.ParentId.Value}' does not exist.");

            level = (short)(parent.Level + 1);
        }

        var now = DateTimeOffset.UtcNow;
        var category = new Category
        {
            Id = Guid.NewGuid(),
            Name = trimmedName,
            Description = request.Description?.Trim(),
            ParentId = request.ParentId,
            Level = level,
            SortOrder = request.SortOrder,
            IsDisabled = false,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = currentUserId ?? Guid.Empty,
            UpdatedBy = currentUserId ?? Guid.Empty,
            IsDeleted = false
        };

        _dbContext.Categories.Add(category);
        _dbContext.AddAuditLog(
            AuditAction.CategoryCreate,
            "Category",
            category.Id.ToString(),
            newValues: new { category.Name, category.ParentId, category.Level, category.SortOrder },
            userId: currentUserId?.ToString());

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new CategoryDto(
            category.Id,
            category.ParentId,
            category.Name,
            category.Description,
            category.Level,
            category.SortOrder,
            category.IsDisabled);
    }

    public async Task<CategoryDto> UpdateAsync(Guid id, UpdateCategoryRequest request, Guid? currentUserId = null, CancellationToken cancellationToken = default)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Name))
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "Category name is required.");
        }

        var trimmedName = request.Name.Trim();
        if (trimmedName.Length > 100)
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "Category name cannot exceed 100 characters.");
        }

        var category = await _dbContext.Categories
            .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted, cancellationToken)
            ?? throw new EntityNotFoundException("CATEGORY_NOT_FOUND", "Category", id);

        var isDuplicate = await _dbContext.Categories
            .AsNoTracking()
            .AnyAsync(c => c.Id != id && !c.IsDeleted && c.Name.ToLower() == trimmedName.ToLower(), cancellationToken);

        if (isDuplicate)
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", $"Category with name '{trimmedName}' already exists.");
        }

        var oldValues = new { category.Name, category.Description, category.Level, category.SortOrder, category.IsDisabled };

        category.Name = trimmedName;
        category.Description = request.Description?.Trim();
        category.Level = request.Level;
        category.SortOrder = request.SortOrder;
        category.IsDisabled = request.IsDisabled;
        category.UpdatedAt = DateTimeOffset.UtcNow;
        category.UpdatedBy = currentUserId ?? Guid.Empty;

        _dbContext.AddAuditLog(
            AuditAction.CategoryUpdate,
            "Category",
            category.Id.ToString(),
            oldValues: oldValues,
            newValues: new { category.Name, category.Description, category.Level, category.SortOrder, category.IsDisabled },
            userId: currentUserId?.ToString());

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new CategoryDto(
            category.Id,
            category.ParentId,
            category.Name,
            category.Description,
            category.Level,
            category.SortOrder,
            category.IsDisabled);
    }

    public async Task<bool> DeleteAsync(Guid id, Guid? currentUserId = null, CancellationToken cancellationToken = default)
    {
        var category = await _dbContext.Categories
            .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted, cancellationToken)
            ?? throw new EntityNotFoundException("CATEGORY_NOT_FOUND", "Category", id);

        var hasChildren = await _dbContext.Categories
            .AsNoTracking()
            .AnyAsync(c => c.ParentId == id && !c.IsDeleted, cancellationToken);

        if (hasChildren)
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "Cannot delete a category that contains child categories.");
        }

        var hasActiveProducts = await _dbContext.Products
            .AsNoTracking()
            .AnyAsync(p => p.CategoryId == id && !p.IsDeleted, cancellationToken);

        if (hasActiveProducts)
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "Cannot delete a category that contains active products.");
        }

        var now = DateTimeOffset.UtcNow;
        category.IsDeleted = true;
        category.DeletedAt = now;
        category.UpdatedAt = now;
        category.UpdatedBy = currentUserId ?? Guid.Empty;

        _dbContext.AddAuditLog(
            AuditAction.CategoryDelete,
            "Category",
            category.Id.ToString(),
            oldValues: new { category.Name, category.ParentId },
            userId: currentUserId?.ToString());

        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
