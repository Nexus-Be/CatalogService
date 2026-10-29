namespace Catalog.Api.Controllers;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MassTransit;
using Catalog.Api.Responses;
using Catalog.Application.Common.Contracts.Events;
using Catalog.Application.Interfaces;
using Catalog.Application.Services.Category;
using Catalog.Domain.Entities;

public sealed record CreateCategoryRequest(string Name, string? Description, Guid? ParentId, short Level, int SortOrder);
public sealed record UpdateCategoryRequest(string Name, string? Description, short Level, int SortOrder, bool IsDisabled);

[ApiController]
[Route("api/v1/categories")]
[Produces("application/json")]
public sealed class CategoriesController : ControllerBase
{
    private readonly IApiResponseFactory _responseFactory;
    private readonly ICategoryService _categoryService;
    private readonly ICatalogDbContext _dbContext;
    private readonly IPublishEndpoint _publishEndpoint;

    public CategoriesController(
        ICategoryService categoryService,
        ICatalogDbContext dbContext,
        IPublishEndpoint publishEndpoint,
        IApiResponseFactory responseFactory)
    {
        _categoryService = categoryService;
        _dbContext = dbContext;
        _publishEndpoint = publishEndpoint;
        _responseFactory = responseFactory;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<CategoryItemDto>>), StatusCodes.Status200OK)]
    public async Task<IResult> ListCategories(
        [FromQuery] Guid? parentId = null,
        [FromQuery] bool tree = false,
        [FromQuery] string? keyword = null,
        CancellationToken cancellationToken = default)
    {
        var categories = await _categoryService.GetCategoriesAsync(
            new CategoryListQuery(parentId, tree, keyword), 
            cancellationToken);
        return _responseFactory.SuccessResult(HttpContext, categories, "Categories retrieved successfully.");
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<CategoryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IResult> GetCategoryById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken = default)
    {
        var category = await _categoryService.GetByIdAsync(id, cancellationToken);
        if (category == null)
        {
            return _responseFactory.FailureResult(
                HttpContext,
                title: "Category not found.",
                status: StatusCodes.Status404NotFound,
                detail: $"Category with ID '{id}' was not found in the system.",
                errorCode: "CATEGORY_NOT_FOUND"
            );
        }

        return _responseFactory.SuccessResult(HttpContext, category, "Category retrieved successfully.");
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<CategoryDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IResult> CreateCategory([FromBody] CreateCategoryRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request?.Name))
        {
            return _responseFactory.FailureResult(
                HttpContext,
                title: "Validation error.",
                status: StatusCodes.Status400BadRequest,
                detail: "Category name is required.",
                errorCode: "VAL_INVALID_INPUT"
            );
        }

        var trimmedName = request.Name.Trim();

        // Ràng buộc tên category không được phép trùng nhau trên toàn hệ thống
        bool isDuplicate = await _dbContext.Categories
            .AsNoTracking()
            .AnyAsync(c => !c.IsDeleted && c.Name.ToLower() == trimmedName.ToLower(), cancellationToken);

        if (isDuplicate)
        {
            return _responseFactory.FailureResult(
                HttpContext,
                title: "Validation error.",
                status: StatusCodes.Status400BadRequest,
                detail: $"Category with name '{trimmedName}' already exists.",
                errorCode: "VAL_INVALID_INPUT"
            );
        }

        var category = new Category
        {
            Id = Guid.NewGuid(),
            Name = trimmedName,
            Description = request.Description?.Trim(),
            ParentId = request.ParentId,
            Level = request.Level,
            SortOrder = request.SortOrder,
            IsDisabled = false,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            IsDeleted = false
        };

        _dbContext.Categories.Add(category);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Bắn sự kiện CategoryCreatedEvent qua Message Broker
        await _publishEndpoint.Publish(new CategoryCreatedEvent
        {
            CategoryId = category.Id,
            Name = category.Name,
            Description = category.Description,
            ParentId = category.ParentId,
            Level = category.Level,
            SortOrder = category.SortOrder,
            CreatedAt = category.CreatedAt.UtcDateTime
        }, cancellationToken);

        var dto = new CategoryDto(
            category.Id,
            category.ParentId,
            category.Name,
            category.Description,
            category.Level,
            category.SortOrder,
            category.IsDisabled
        );

        return _responseFactory.SuccessResult(HttpContext, dto, "Category created successfully.", StatusCodes.Status201Created);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<CategoryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IResult> UpdateCategory([FromRoute] Guid id, [FromBody] UpdateCategoryRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request?.Name))
        {
            return _responseFactory.FailureResult(
                HttpContext,
                title: "Validation error.",
                status: StatusCodes.Status400BadRequest,
                detail: "Category name is required.",
                errorCode: "VAL_INVALID_INPUT"
            );
        }

        var trimmedName = request.Name.Trim();

        var dbCategory = await _dbContext.Categories.FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted, cancellationToken);
        if (dbCategory == null)
        {
            return _responseFactory.FailureResult(
                HttpContext,
                title: "Category not found.",
                status: StatusCodes.Status404NotFound,
                detail: $"Category with ID '{id}' was not found in the system.",
                errorCode: "CATEGORY_NOT_FOUND"
            );
        }

        // Ràng buộc tên category không được phép trùng với category khác
        bool isDuplicate = await _dbContext.Categories
            .AsNoTracking()
            .AnyAsync(c => c.Id != id && !c.IsDeleted && c.Name.ToLower() == trimmedName.ToLower(), cancellationToken);

        if (isDuplicate)
        {
            return _responseFactory.FailureResult(
                HttpContext,
                title: "Validation error.",
                status: StatusCodes.Status400BadRequest,
                detail: $"Category with name '{trimmedName}' already exists.",
                errorCode: "VAL_INVALID_INPUT"
            );
        }

        dbCategory.Name = trimmedName;
        dbCategory.Description = request.Description?.Trim();
        dbCategory.Level = request.Level;
        dbCategory.SortOrder = request.SortOrder;
        dbCategory.IsDisabled = request.IsDisabled;
        dbCategory.UpdatedAt = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        var dto = new CategoryDto(
            dbCategory.Id,
            dbCategory.ParentId,
            dbCategory.Name,
            dbCategory.Description,
            dbCategory.Level,
            dbCategory.SortOrder,
            dbCategory.IsDisabled
        );

        return _responseFactory.SuccessResult(HttpContext, dto, "Category updated successfully.");
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IResult> DeleteCategory([FromRoute] Guid id, CancellationToken cancellationToken = default)
    {
        var category = await _dbContext.Categories.FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted, cancellationToken);
        if (category == null)
        {
            return _responseFactory.FailureResult(
                HttpContext,
                title: "Category not found.",
                status: StatusCodes.Status404NotFound,
                detail: $"Category with ID '{id}' was not found in the system.",
                errorCode: "CATEGORY_NOT_FOUND"
            );
        }

        category.IsDeleted = true;
        category.UpdatedAt = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return _responseFactory.SuccessResult(HttpContext, new { deleted = true, id }, "Category deleted successfully.");
    }
}
