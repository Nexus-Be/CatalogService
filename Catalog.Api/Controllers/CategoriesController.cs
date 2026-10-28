namespace Catalog.Api.Controllers;

using System;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Catalog.Api.Mock;
using Catalog.Api.Responses;
using Catalog.Application.Services.Category;

public sealed record CreateCategoryRequest(string Name, string? Description, Guid? ParentId, short Level, int SortOrder);
public sealed record UpdateCategoryRequest(string Name, string? Description, short Level, int SortOrder, bool IsDisabled);

[ApiController]
[Route("api/v1/catalog/categories")]
[Produces("application/json")]
public sealed class CategoriesController : ControllerBase
{
    private readonly MockCatalogStore _store;
    private readonly IApiResponseFactory _responseFactory;
    private readonly ICategoryService _categoryService;

    public CategoriesController(MockCatalogStore store, ICategoryService categoryService, IApiResponseFactory responseFactory)
    {
        _store = store;
        _categoryService = categoryService;
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
    public IResult CreateCategory([FromBody] CreateCategoryRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return _responseFactory.FailureResult(
                HttpContext,
                title: "Validation error.",
                status: StatusCodes.Status400BadRequest,
                detail: "Category name is required.",
                errorCode: "VAL_INVALID_INPUT"
            );
        }

        var created = _store.CreateCategory(request.Name, request.Description, request.ParentId, request.Level, request.SortOrder);
        return _responseFactory.SuccessResult(HttpContext, created, "Category created successfully.", StatusCodes.Status201Created);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<CategoryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public IResult UpdateCategory([FromRoute] Guid id, [FromBody] UpdateCategoryRequest request)
    {
        var updated = _store.UpdateCategory(id, request.Name, request.Description, request.Level, request.SortOrder, request.IsDisabled);
        if (updated == null)
        {
            return _responseFactory.FailureResult(
                HttpContext,
                title: "Category not found.",
                status: StatusCodes.Status404NotFound,
                detail: $"Category with ID '{id}' was not found in the system.",
                errorCode: "CATEGORY_NOT_FOUND"
            );
        }

        return _responseFactory.SuccessResult(HttpContext, updated, "Category updated successfully.");
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public IResult DeleteCategory([FromRoute] Guid id)
    {
        var deleted = _store.DeleteCategory(id);
        if (!deleted)
        {
            return _responseFactory.FailureResult(
                HttpContext,
                title: "Category not found.",
                status: StatusCodes.Status404NotFound,
                detail: $"Category with ID '{id}' was not found in the system.",
                errorCode: "CATEGORY_NOT_FOUND"
            );
        }

        return _responseFactory.SuccessResult(HttpContext, new { deleted = true, id }, "Category deleted successfully.");
    }
}
