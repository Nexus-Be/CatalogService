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
[Route("api/v1/seller/products")]
[Produces("application/json")]
public class SellerProductsController : ControllerBase
{
    private readonly IProductService _productService;

    public SellerProductsController(IProductService productService)
    {
        _productService = productService;
    }

    /// <summary>Lists and searches all products belonging to the authenticated seller (CAT-44).</summary>
    [HttpGet]
    [RequireRole("seller", "admin")]
    [ProducesResponseType(typeof(PagedData<SellerProductListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> SearchSellerProducts(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? keyword = null,
        [FromQuery] ProductStatus? status = null,
        [FromQuery] Guid? categoryId = null,
        [FromQuery] SaleType? saleType = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDirection = "asc",
        CancellationToken ct = default)
    {
        if (!this.TryGetUserId(out var currentUserId, out var errorResult))
        {
            return errorResult;
        }

        var query = new SellerProductSearchQuery(
            page,
            pageSize,
            keyword,
            status,
            categoryId,
            saleType,
            sortBy,
            sortDirection);

        var result = await _productService.SearchSellerProductsAsync(currentUserId, query, ct);
        return Ok(result);
    }

    /// <summary>Gets full seller product details including variants with images and order status (CAT-45).</summary>
    [HttpGet("{id:guid}")]
    [RequireRole("seller", "admin")]
    [ProducesResponseType(typeof(SellerProductDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSellerProductDetail(
        [FromRoute] Guid id,
        CancellationToken ct)
    {
        if (!this.TryGetUserId(out var currentUserId, out var errorResult))
        {
            return errorResult;
        }

        var product = await _productService.GetSellerProductDetailAsync(id, currentUserId, ct);
        return Ok(product);
    }
}
