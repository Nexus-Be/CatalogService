namespace Catalog.Api.Controllers.Public;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Catalog.Api.Extensions;
using Catalog.Api.Filters;
using Catalog.Application.Common.Models;
using Catalog.Application.Services.Category;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/v1/categories")]
[Produces("application/json")]
public class CategoriesController : ControllerBase
{
    private readonly ICategoryService _categoryService;

    public CategoriesController(ICategoryService categoryService)
    {
        _categoryService = categoryService;
    }

    /// <summary>Lists categories. Supports tree hierarchy or flat list with search.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<CategoryItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] Guid? parentId = null,
        [FromQuery] bool tree = false,
        [FromQuery] string? keyword = null,
        CancellationToken ct = default)
    {
        var categories = await _categoryService.GetCategoriesAsync(
            new CategoryListQuery(parentId, tree, keyword),
            ct);
        return Ok(categories);
    }

    /// <summary>Gets category details by ID.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CategoryDto>> GetById([FromRoute] Guid id, CancellationToken ct)
    {
        var category = await _categoryService.GetByIdAsync(id, ct);
        return Ok(category);
    }

    /// <summary>Creates a new category. Requires admin or staff role (CAT-15).</summary>
    [HttpPost]
    [RequireRole("admin", "staff")]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create([FromBody] CreateCategoryRequest request, CancellationToken ct)
    {
        var currentUserId = this.GetUserId();
        var category = await _categoryService.CreateAsync(request, currentUserId, ct);
        return StatusCode(StatusCodes.Status201Created, category);
    }

    /// <summary>Updates an existing category. Requires admin or staff role (CAT-16).</summary>
    [HttpPut("{id:guid}")]
    [HttpPatch("{id:guid}")]
    [RequireRole("admin", "staff")]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update([FromRoute] Guid id, [FromBody] UpdateCategoryRequest request, CancellationToken ct)
    {
        var currentUserId = this.GetUserId();
        var category = await _categoryService.UpdateAsync(id, request, currentUserId, ct);
        return Ok(category);
    }

    /// <summary>Soft deletes a category. Requires admin or staff role (CAT-17).</summary>
    [HttpDelete("{id:guid}")]
    [RequireRole("admin", "staff")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete([FromRoute] Guid id, CancellationToken ct)
    {
        var currentUserId = this.GetUserId();
        await _categoryService.DeleteAsync(id, currentUserId, ct);
        return NoContent();
    }
}
