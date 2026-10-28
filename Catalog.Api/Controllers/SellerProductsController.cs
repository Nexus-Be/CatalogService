namespace Catalog.Api.Controllers;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Catalog.Api.Extensions;
using Catalog.Api.Responses;
using Catalog.Application.Services.Product;
using Catalog.Domain.Enums;

[ApiController]
[Route("api/v1/catalog/seller/products")]
[Route("api/v1/seller/products")]
[Produces("application/json")]
public sealed class SellerProductsController : ControllerBase
{
    private readonly IProductService _productService;
    private readonly IApiResponseFactory _responseFactory;

    public SellerProductsController(IProductService productService, IApiResponseFactory responseFactory)
    {
        _productService = productService;
        _responseFactory = responseFactory;
    }

    /// <summary>
    /// CAT-44: Seller Search & Browse Shop Products
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedData<SellerProductListItemDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IResult> SearchSellerProducts(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? keyword = null,
        [FromQuery] ProductStatus? status = null,
        [FromQuery] Guid? categoryId = null,
        [FromQuery] SaleType? saleType = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] Guid? sellerId = null,
        CancellationToken cancellationToken = default)
    {
        var currentSellerId = HttpContext.GetCurrentSellerId(sellerId);
        if (!currentSellerId.HasValue || currentSellerId.Value == Guid.Empty)
        {
            return _responseFactory.FailureResult(
                HttpContext,
                title: "Unauthorized.",
                status: StatusCodes.Status401Unauthorized,
                detail: "Seller identity is required via X-Seller-Id header.",
                errorCode: "UNAUTHORIZED"
            );
        }

        if (page < 1 || pageSize < 1 || pageSize > 100)
        {
            return _responseFactory.FailureResult(
                HttpContext,
                title: "Invalid pagination parameters.",
                status: StatusCodes.Status400BadRequest,
                detail: "Page must be greater than or equal to 1, and pageSize must be between 1 and 100.",
                errorCode: "VAL_INVALID_PAGINATION"
            );
        }

        var query = new SellerProductSearchQuery(page, pageSize, keyword, status, categoryId, saleType, sortBy);
        var result = await _productService.SearchSellerProductsAsync(currentSellerId.Value, query, cancellationToken);

        return _responseFactory.PagedResult(HttpContext, result.Items, page, pageSize, result.TotalCount, StatusCodes.Status200OK);
    }

    /// <summary>
    /// CAT-45: Seller Product Detail
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<SellerProductDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IResult> GetSellerProductDetail(
        [FromRoute] Guid id,
        [FromQuery] Guid? sellerId = null,
        CancellationToken cancellationToken = default)
    {
        var currentSellerId = HttpContext.GetCurrentSellerId(sellerId);
        if (!currentSellerId.HasValue || currentSellerId.Value == Guid.Empty)
        {
            return _responseFactory.FailureResult(
                HttpContext,
                title: "Unauthorized.",
                status: StatusCodes.Status401Unauthorized,
                detail: "Seller identity is required via X-Seller-Id header.",
                errorCode: "UNAUTHORIZED"
            );
        }

        var result = await _productService.GetSellerProductDetailAsync(id, currentSellerId.Value, cancellationToken);
        if (result.Status == ProductOperationStatus.NotFound)
        {
            return _responseFactory.FailureResult(
                HttpContext,
                title: "Product not found.",
                status: StatusCodes.Status404NotFound,
                detail: result.ErrorMessage ?? "Product not found in the system.",
                errorCode: "PRODUCT_NOT_FOUND"
            );
        }

        if (result.Status == ProductOperationStatus.Forbidden)
        {
            return _responseFactory.FailureResult(
                HttpContext,
                title: "Forbidden.",
                status: StatusCodes.Status403Forbidden,
                detail: result.ErrorMessage ?? "You do not have permission to view or manage this product.",
                errorCode: "FORBIDDEN"
            );
        }

        return _responseFactory.SuccessResult(
            HttpContext,
            result.Data,
            "Seller product details retrieved successfully.");
    }
}

