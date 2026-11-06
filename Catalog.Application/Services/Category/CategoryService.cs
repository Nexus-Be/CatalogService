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
        var rawSortBy = query.SortBy?.Trim().ToLowerInvariant() ?? "sortorder";
        var allowedSortFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "sortorder", "name", "level", "createdat" };
        if (!allowedSortFields.Contains(rawSortBy))
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", $"Invalid sortBy parameter '{query.SortBy}'. Allowed values: sortOrder, name, level, createdAt.");
        }

        var rawSortDir = query.SortDirection?.Trim().ToLowerInvariant() ?? "asc";
        if (rawSortDir != "asc" && rawSortDir != "desc")
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", $"Invalid sortDirection parameter '{query.SortDirection}'. Allowed values: asc, desc.");
        }
        var isDesc = rawSortDir == "desc";

        var categoriesQuery = _dbContext.Categories
            .AsNoTracking()
            .Where(c => !c.IsDeleted);

        if (!query.IncludeDisabled)
        {
            categoriesQuery = categoriesQuery.Where(c => !c.IsDisabled);
        }

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var kw = query.Keyword.Trim();
            var pattern = $"%{kw}%";
            categoriesQuery = categoriesQuery.Where(c => EF.Functions.ILike(EF.Functions.Unaccent(c.Name), EF.Functions.Unaccent(pattern)));
        }

        if (!query.Tree)
        {
            if (query.ParentId.HasValue)
            {
                categoriesQuery = categoriesQuery.Where(c => c.ParentId == query.ParentId.Value);
            }

            var totalCount = await categoriesQuery.CountAsync(cancellationToken);

            IOrderedQueryable<Category> orderedQuery = (rawSortBy, isDesc) switch
            {
                ("name", false) => categoriesQuery.OrderBy(c => c.Name).ThenBy(c => c.SortOrder).ThenBy(c => c.Id),
                ("name", true) => categoriesQuery.OrderByDescending(c => c.Name).ThenBy(c => c.SortOrder).ThenBy(c => c.Id),
                ("level", false) => categoriesQuery.OrderBy(c => c.Level).ThenBy(c => c.SortOrder).ThenBy(c => c.Name).ThenBy(c => c.Id),
                ("level", true) => categoriesQuery.OrderByDescending(c => c.Level).ThenBy(c => c.SortOrder).ThenBy(c => c.Name).ThenBy(c => c.Id),
                ("createdat", false) => categoriesQuery.OrderBy(c => c.CreatedAt).ThenBy(c => c.SortOrder).ThenBy(c => c.Name).ThenBy(c => c.Id),
                ("createdat", true) => categoriesQuery.OrderByDescending(c => c.CreatedAt).ThenBy(c => c.SortOrder).ThenBy(c => c.Name).ThenBy(c => c.Id),
                ("sortorder", true) => categoriesQuery.OrderByDescending(c => c.SortOrder).ThenBy(c => c.Name).ThenBy(c => c.Id),
                _ => categoriesQuery.OrderBy(c => c.SortOrder).ThenBy(c => c.Name).ThenBy(c => c.Id)
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
                    c.IsDisabled,
                    null))
                .ToListAsync(cancellationToken);

            return new PagedData<CategoryItemDto>(items, PaginationMetadata.Create(safePage, safePageSize, totalCount));
        }

        // Tree hierarchy: sort and paginate root categories (default pageSize = 10), then attach descendants
        var rootQuery = categoriesQuery.Where(c => c.ParentId == query.ParentId);
        var totalRootCount = await rootQuery.CountAsync(cancellationToken);

        IOrderedQueryable<Category> orderedRoots = (rawSortBy, isDesc) switch
        {
            ("name", false) => rootQuery.OrderBy(c => c.Name).ThenBy(c => c.SortOrder).ThenBy(c => c.Id),
            ("name", true) => rootQuery.OrderByDescending(c => c.Name).ThenBy(c => c.SortOrder).ThenBy(c => c.Id),
            ("level", false) => rootQuery.OrderBy(c => c.Level).ThenBy(c => c.SortOrder).ThenBy(c => c.Name).ThenBy(c => c.Id),
            ("level", true) => rootQuery.OrderByDescending(c => c.Level).ThenBy(c => c.SortOrder).ThenBy(c => c.Name).ThenBy(c => c.Id),
            ("createdat", false) => rootQuery.OrderBy(c => c.CreatedAt).ThenBy(c => c.SortOrder).ThenBy(c => c.Name).ThenBy(c => c.Id),
            ("createdat", true) => rootQuery.OrderByDescending(c => c.CreatedAt).ThenBy(c => c.SortOrder).ThenBy(c => c.Name).ThenBy(c => c.Id),
            ("sortorder", true) => rootQuery.OrderByDescending(c => c.SortOrder).ThenBy(c => c.Name).ThenBy(c => c.Id),
            _ => rootQuery.OrderBy(c => c.SortOrder).ThenBy(c => c.Name).ThenBy(c => c.Id)
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
                c.IsDisabled,
                new List<CategoryItemDto>()))
            .ToListAsync(cancellationToken);

        if (pagedRoots.Count > 0)
        {
            var descendantsQuery = _dbContext.Categories
                .AsNoTracking()
                .Where(c => !c.IsDeleted && c.ParentId != null);

            if (!query.IncludeDisabled)
            {
                descendantsQuery = descendantsQuery.Where(c => !c.IsDisabled);
            }

            var allDescendants = await descendantsQuery
                .OrderBy(c => c.SortOrder)
                .ThenBy(c => c.Name)
                .ThenBy(c => c.Id)
                .Select(c => new CategoryItemDto(
                    c.Id,
                    c.ParentId,
                    c.Name,
                    c.Description,
                    c.Level,
                    c.SortOrder,
                    c.IsDisabled,
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

        if (request.Level < 1)
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "Category level must be greater than or equal to 1.");
        }

        if (request.SortOrder < 0)
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "Sort order must be greater than or equal to 0.");
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

        short resolvedLevel;
        Guid? resolvedParentId = null;

        if (request.ParentId.HasValue && request.ParentId.Value != Guid.Empty)
        {
            var parent = await _dbContext.Categories
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == request.ParentId.Value && !c.IsDeleted, cancellationToken)
                ?? throw new BusinessRuleValidationException("VAL_INVALID_INPUT", $"Parent category with ID '{request.ParentId.Value}' does not exist.");

            resolvedParentId = parent.Id;
            resolvedLevel = (short)(parent.Level + 1);

            // If caller explicitly specified a Level, it must match parent.Level + 1
            if (request.Level != 1 && request.Level != resolvedLevel)
            {
                throw new BusinessRuleValidationException(
                    "VAL_INVALID_INPUT",
                    $"Category level must be {resolvedLevel} when parent category is at level {parent.Level}. Provided level: {request.Level}.");
            }
        }
        else
        {
            // Root category (ParentId is null or empty)
            if (request.Level > 1)
            {
                throw new BusinessRuleValidationException(
                    "VAL_INVALID_INPUT",
                    $"Parent category ID is required for categories at level {request.Level}. Only root categories (level 1) can have no parent.");
            }

            resolvedLevel = 1;
        }

        var now = DateTimeOffset.UtcNow;
        var category = new Category
        {
            Id = Guid.NewGuid(),
            Name = trimmedName,
            Description = request.Description?.Trim(),
            ParentId = resolvedParentId,
            Level = resolvedLevel,
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

        if (request.Level < 1)
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "Category level must be greater than or equal to 1.");
        }

        if (request.SortOrder < 0)
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "Sort order must be greater than or equal to 0.");
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

        if (category.ParentId.HasValue)
        {
            var parent = await _dbContext.Categories
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == category.ParentId.Value && !c.IsDeleted, cancellationToken);

            if (parent != null)
            {
                short expectedLevel = (short)(parent.Level + 1);
                if (request.Level != expectedLevel)
                {
                    throw new BusinessRuleValidationException(
                        "VAL_INVALID_INPUT",
                        $"Category level must match parent level + 1 ({expectedLevel}). Provided level: {request.Level}.");
                }
            }
        }
        else
        {
            if (request.Level != 1)
            {
                throw new BusinessRuleValidationException(
                    "VAL_INVALID_INPUT",
                    "Root categories (categories without a parent) must remain at level 1.");
            }
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
            throw new BusinessRuleValidationException("CATEGORY_HAS_CHILDREN", "Cannot delete a category that contains child categories. Please delete or reassign child categories first.");
        }

        var hasProducts = await _dbContext.Products
            .AsNoTracking()
            .AnyAsync(p => p.CategoryId == id && !p.IsDeleted, cancellationToken);

        if (hasProducts)
        {
            throw new BusinessRuleValidationException("CATEGORY_HAS_PRODUCTS", "Cannot delete a category that contains products. Please reassign or delete associated products first.");
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
