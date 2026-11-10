namespace Catalog.Api.Controllers.Public;

using System;
using System.Threading;
using System.Threading.Tasks;
using Catalog.Api.Extensions;
using Catalog.Api.Filters;
using Catalog.Application.Common.Models;
using Catalog.Application.Services.Product;
using Catalog.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/v1/admin/products")]
[Produces("application/json")]
public class AdminProductsController : ControllerBase
{
    private readonly IProductService _productService;

    public AdminProductsController(IProductService productService)
    {
        _productService = productService;
    }

    /// <summary>Lists and searches all products across the entire platform for admin and staff oversight (CAT-50).</summary>
    [HttpGet]
    [RequireRole("admin", "staff")]
    [ProducesResponseType(typeof(PagedData<AdminProductListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> SearchAdminProducts(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? keyword = null,
        [FromQuery] ProductStatus? status = null,
        [FromQuery] ProductInactiveReason? statusReason = null,
        [FromQuery] Guid? categoryId = null,
        [FromQuery] Guid? sellerId = null,
        [FromQuery] SaleType? saleType = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDirection = "desc",
        CancellationToken ct = default)
    {
        if (page < 1 || pageSize < 1 || pageSize > 100)
        {
            return this.BadRequestError("VAL_INVALID_PAGINATION", "Page must be greater than or equal to 1, and pageSize must be between 1 and 100.");
        }

        var query = new AdminProductSearchQuery(
            page,
            pageSize,
            keyword,
            status,
            statusReason,
            categoryId,
            sellerId,
            saleType,
            sortBy,
            sortDirection);

        var result = await _productService.SearchAdminProductsAsync(query, ct);
        return Ok(result);
    }

    /// <summary>Moderates product: approve, suspend, reject, unlock, or ban (CAT-51).</summary>
    [HttpPatch("{id:guid}/moderate")]
    [RequireRole("admin", "staff")]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ModerateProduct(
        [FromRoute] Guid id,
        [FromBody] ModerateProductRequest request,
        CancellationToken ct)
    {
        if (!this.TryGetUserId(out var adminId, out var errorResult))
        {
            return errorResult;
        }

        if (string.IsNullOrWhiteSpace(request?.Action))
        {
            return this.BadRequestError("VAL_INVALID_INPUT", "Action is required. Allowed values: APPROVE, SUSPEND, REJECT, UNLOCK, BAN.");
        }

        var updated = await _productService.ModerateProductAsync(id, request, adminId, ct);
        return Ok(updated);
    }
}

