namespace Catalog.Api.Controllers.Public;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Catalog.Application.Common.Models;
using Catalog.Application.Services.Category;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/v1/categories")]
public class CategoriesController : ControllerBase
{
    private readonly ICategoryService _categoryService;

    public CategoriesController(ICategoryService categoryService)
    {
        _categoryService = categoryService;
    }

    /// <summary>Lists categories with pagination.</summary>
    [HttpGet]
    public async Task<ActionResult<PagedData<CategoryDto>>> ListAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var categories = await _categoryService.ListAllAsync(page, pageSize, ct);
        return Ok(categories);
    }

    /// <summary>Gets category details by ID.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CategoryDto>> GetById([FromRoute] Guid id, CancellationToken ct)
    {
        var category = await _categoryService.GetByIdAsync(id, ct);
        return Ok(category);
    }
}
