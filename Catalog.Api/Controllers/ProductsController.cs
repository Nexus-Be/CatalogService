namespace Catalog.Api.Controllers;

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Catalog.Api.Extensions;
using Catalog.Api.Responses;
using Catalog.Application.Services.Product;
using Catalog.Domain.Enums;

public sealed record CreateProductRequest(
    Guid? SellerId, 
    Guid CategoryId, 
    string Name, 
    string? Description, 
    string? Brand,
    SaleType? SaleType = SaleType.Regular,
    string? Specifications = null);

public sealed record UpdateProductRequest(
    string Name, 
    string? Description, 
    string? Brand, 
    Guid? CategoryId = null,
    SaleType? SaleType = null,
    string? Status = null,
    string? Specifications = null);

public sealed class UploadProductImageRequest
{
    public IFormFile? File { get; set; }
    public Guid? VariantId { get; set; }
    public string? AltText { get; set; }
    public short SortOrder { get; set; } = 0;
    public Guid? SellerId { get; set; }
}

[ApiController]
[Route("api/v1/catalog/products")]
[Produces("application/json")]
public sealed class ProductsController : ControllerBase
{
    private readonly IProductService _productService;
    private readonly IApiResponseFactory _responseFactory;

    public ProductsController(IProductService productService, IApiResponseFactory responseFactory)
    {
        _productService = productService;
        _responseFactory = responseFactory;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedData<ProductListItemDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IResult> ListProducts(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? keyword = null, [FromQuery] Guid? categoryId = null,
        [FromQuery] Guid? sellerId = null, [FromQuery] SaleType? saleType = null,
        [FromQuery] string? sortBy = null, CancellationToken cancellationToken = default)
    {
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

        var result = await _productService.SearchAsync(
            new ProductListQuery(page, pageSize, keyword, categoryId, sellerId, saleType, sortBy),
            cancellationToken);

        return _responseFactory.PagedResult(HttpContext, result.Items, page, pageSize, result.TotalCount, StatusCodes.Status200OK);
    }

    [HttpGet("seller/{sellerId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<PagedData<ProductListItemDto>>), StatusCodes.Status200OK)]
    public async Task<IResult> ListProductsBySeller(
        [FromRoute] Guid sellerId, 
        [FromQuery] int page = 1, 
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
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

        var result = await _productService.SearchAsync(
            new ProductListQuery(page, pageSize, null, null, sellerId, null, null),
            cancellationToken);

        return _responseFactory.PagedResult(HttpContext, result.Items, page, pageSize, result.TotalCount, StatusCodes.Status200OK);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<ProductDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IResult> GetProduct([FromRoute] Guid id, CancellationToken cancellationToken = default)
    {
        var product = await _productService.GetDetailAsync(id, cancellationToken);
        if (product == null)
        {
            return _responseFactory.FailureResult(
                HttpContext,
                title: "Product not found.",
                status: StatusCodes.Status404NotFound,
                detail: $"Product with ID '{id}' was not found in the system.",
                errorCode: "PRODUCT_NOT_FOUND"
            );
        }

        return _responseFactory.SuccessResult(HttpContext, product, "Product retrieved successfully.");
    }

    [HttpGet("search")]
    [ProducesResponseType(typeof(ApiResponse<PagedData<ProductListItemDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IResult> SearchProducts(
        [FromQuery] int page = 1, 
        [FromQuery] int pageSize = 20,
        [FromQuery] string? keyword = null, 
        [FromQuery] Guid? categoryId = null,
        [FromQuery] Guid? sellerId = null, 
        [FromQuery] SaleType? saleType = null,
        [FromQuery] decimal? minPrice = null,
        [FromQuery] decimal? maxPrice = null,
        [FromQuery] string? brand = null,
        [FromQuery] decimal? minRating = null,
        [FromQuery] string? sortBy = null, 
        CancellationToken cancellationToken = default)
    {
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

        if (minPrice.HasValue && maxPrice.HasValue && minPrice.Value > maxPrice.Value)
        {
            return _responseFactory.FailureResult(
                HttpContext,
                title: "Invalid price range.",
                status: StatusCodes.Status400BadRequest,
                detail: "MinPrice cannot be greater than MaxPrice.",
                errorCode: "VAL_INVALID_PRICE_RANGE"
            );
        }

        var result = await _productService.SearchAsync(
            new ProductListQuery(page, pageSize, keyword, categoryId, sellerId, saleType, sortBy, minPrice, maxPrice, brand, minRating),
            cancellationToken);

        return _responseFactory.PagedResult(HttpContext, result.Items, page, pageSize, result.TotalCount, StatusCodes.Status200OK);
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<ProductDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IResult> CreateProduct([FromBody] CreateProductRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request?.Name))
        {
            return _responseFactory.FailureResult(
                HttpContext,
                title: "Validation error.",
                status: StatusCodes.Status400BadRequest,
                detail: "Product name is required.",
                errorCode: "VAL_INVALID_INPUT"
            );
        }

        var currentSellerId = HttpContext.GetCurrentSellerId(request.SellerId);
        if (!currentSellerId.HasValue || currentSellerId.Value == Guid.Empty)
        {
            return _responseFactory.FailureResult(
                HttpContext,
                title: "Validation error.",
                status: StatusCodes.Status400BadRequest,
                detail: "Seller identity is required via X-Seller-Id header or request body.",
                errorCode: "VAL_INVALID_INPUT"
            );
        }

        if (request.CategoryId == Guid.Empty)
        {
            return _responseFactory.FailureResult(
                HttpContext,
                title: "Validation error.",
                status: StatusCodes.Status400BadRequest,
                detail: "Category ID is required.",
                errorCode: "VAL_INVALID_INPUT"
            );
        }

        var command = new CreateProductCommand(
            currentSellerId.Value,
            request.CategoryId,
            request.Name,
            request.Description,
            request.Brand,
            request.SaleType ?? SaleType.Regular,
            request.Specifications);

        var result = await _productService.CreateAsync(command, cancellationToken);
        if (result.Status == ProductOperationStatus.InvalidCategory)
        {
            return _responseFactory.FailureResult(
                HttpContext,
                title: "Validation error.",
                status: StatusCodes.Status400BadRequest,
                detail: result.ErrorMessage ?? "Invalid category.",
                errorCode: "VAL_INVALID_INPUT"
            );
        }

        return _responseFactory.SuccessResult(
            HttpContext, 
            result.Data, 
            "Product created successfully.", 
            StatusCodes.Status201Created);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<ProductDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IResult> UpdateProduct(
        [FromRoute] Guid id, 
        [FromBody] UpdateProductRequest request, 
        CancellationToken cancellationToken = default)
    {
        var currentSellerId = HttpContext.GetCurrentSellerId();
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

        if (string.IsNullOrWhiteSpace(request?.Name))
        {
            return _responseFactory.FailureResult(
                HttpContext,
                title: "Validation error.",
                status: StatusCodes.Status400BadRequest,
                detail: "Product name cannot be empty.",
                errorCode: "VAL_INVALID_INPUT"
            );
        }

        var command = new UpdateProductCommand(
            id,
            currentSellerId.Value,
            request.Name,
            request.Description,
            request.Brand,
            request.CategoryId,
            request.SaleType,
            request.Status,
            request.Specifications);

        var result = await _productService.UpdateAsync(command, cancellationToken);
        return result.Status switch
        {
            ProductOperationStatus.NotFound => _responseFactory.FailureResult(
                HttpContext,
                title: "Product not found.",
                status: StatusCodes.Status404NotFound,
                detail: result.ErrorMessage ?? $"Product with ID '{id}' was not found in the system.",
                errorCode: "PRODUCT_NOT_FOUND"
            ),
            ProductOperationStatus.Forbidden => _responseFactory.FailureResult(
                HttpContext,
                title: "Forbidden.",
                status: StatusCodes.Status403Forbidden,
                detail: result.ErrorMessage ?? "You do not have permission to modify this product.",
                errorCode: "PERMISSION_DENIED"
            ),
            ProductOperationStatus.InvalidCategory => _responseFactory.FailureResult(
                HttpContext,
                title: "Validation error.",
                status: StatusCodes.Status400BadRequest,
                detail: result.ErrorMessage ?? "Invalid category.",
                errorCode: "VAL_INVALID_INPUT"
            ),
            _ => _responseFactory.SuccessResult(
                HttpContext, 
                result.Data, 
                "Product updated successfully.")
        };
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IResult> DeleteProduct(
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

        var result = await _productService.SoftDeleteAsync(id, currentSellerId.Value, cancellationToken);
        return result.Status switch
        {
            ProductOperationStatus.NotFound => _responseFactory.FailureResult(
                HttpContext,
                title: "Product not found.",
                status: StatusCodes.Status404NotFound,
                detail: result.ErrorMessage ?? $"Product with ID '{id}' was not found in the system.",
                errorCode: "PRODUCT_NOT_FOUND"
            ),
            ProductOperationStatus.Forbidden => _responseFactory.FailureResult(
                HttpContext,
                title: "Forbidden.",
                status: StatusCodes.Status403Forbidden,
                detail: result.ErrorMessage ?? "You do not have permission to delete this product.",
                errorCode: "PERMISSION_DENIED"
            ),
            _ => _responseFactory.SuccessResult(
                HttpContext, 
                new { deleted = true, id }, 
                "Product deleted successfully.")
        };
    }

    [HttpPost("{id:guid}/images")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<ProductImageDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IResult> UploadProductImage(
        [FromRoute] Guid id,
        [FromForm] UploadProductImageRequest request,
        [FromQuery] Guid? sellerId = null,
        CancellationToken cancellationToken = default)
    {
        var currentSellerId = HttpContext.GetCurrentSellerId(request?.SellerId ?? sellerId);
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

        if (request?.File == null || request.File.Length == 0)
        {
            return _responseFactory.FailureResult(
                HttpContext,
                title: "Validation error.",
                status: StatusCodes.Status400BadRequest,
                detail: "An image file is required.",
                errorCode: "VAL_INVALID_INPUT"
            );
        }

        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
        var ext = Path.GetExtension(request.File.FileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(ext))
        {
            return _responseFactory.FailureResult(
                HttpContext,
                title: "Validation error.",
                status: StatusCodes.Status400BadRequest,
                detail: "Only JPG, JPEG, PNG, and WEBP image formats are supported.",
                errorCode: "VAL_INVALID_INPUT"
            );
        }

        if (request.File.Length > 5 * 1024 * 1024)
        {
            return _responseFactory.FailureResult(
                HttpContext,
                title: "Limit exceeded.",
                status: StatusCodes.Status400BadRequest,
                detail: "Image size cannot exceed 5MB.",
                errorCode: "VAL_LIMIT_EXCEEDED"
            );
        }

        using var stream = request.File.OpenReadStream();
        var result = await _productService.AddProductImageAsync(
            id,
            currentSellerId.Value,
            stream,
            request.File.FileName,
            request.VariantId,
            request.AltText,
            request.SortOrder,
            cancellationToken);

        return result.Status switch
        {
            ProductOperationStatus.NotFound => _responseFactory.FailureResult(
                HttpContext,
                title: "Product not found.",
                status: StatusCodes.Status404NotFound,
                detail: result.ErrorMessage ?? $"Product with ID '{id}' was not found in the system.",
                errorCode: "PRODUCT_NOT_FOUND"
            ),
            ProductOperationStatus.Forbidden => _responseFactory.FailureResult(
                HttpContext,
                title: "Forbidden.",
                status: StatusCodes.Status403Forbidden,
                detail: result.ErrorMessage ?? "You do not have permission to upload images for this product.",
                errorCode: "PERMISSION_DENIED"
            ),
            ProductOperationStatus.InvalidCategory => _responseFactory.FailureResult(
                HttpContext,
                title: "Validation error.",
                status: StatusCodes.Status400BadRequest,
                detail: result.ErrorMessage ?? "Variant does not belong to this product.",
                errorCode: "VAL_INVALID_INPUT"
            ),
            _ => _responseFactory.SuccessResult(
                HttpContext,
                result.Data,
                "Product image uploaded successfully.",
                StatusCodes.Status201Created)
        };
    }

    [HttpDelete("{id:guid}/images/{imageId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IResult> DeleteProductImage(
        [FromRoute] Guid id,
        [FromRoute] Guid imageId,
        CancellationToken cancellationToken = default)
    {
        var currentSellerId = HttpContext.GetCurrentSellerId();
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

        var result = await _productService.DeleteProductImageAsync(id, imageId, currentSellerId.Value, cancellationToken);
        return result.Status switch
        {
            ProductOperationStatus.NotFound => _responseFactory.FailureResult(
                HttpContext,
                title: "Not found.",
                status: StatusCodes.Status404NotFound,
                detail: result.ErrorMessage ?? "Image not found.",
                errorCode: "PRODUCT_NOT_FOUND"
            ),
            ProductOperationStatus.Forbidden => _responseFactory.FailureResult(
                HttpContext,
                title: "Forbidden.",
                status: StatusCodes.Status403Forbidden,
                detail: result.ErrorMessage ?? "You do not have permission to delete images for this product.",
                errorCode: "PERMISSION_DENIED"
            ),
            _ => _responseFactory.SuccessResult(
                HttpContext,
                new { deleted = true, productId = id, imageId },
                "Product image deleted successfully.")
        };
    }

    /// <summary>
    /// CAT-07: Add Variant to Product
    /// </summary>
    [Tags("Products", "Variants")]
    [HttpPost("{id:guid}/variants")]
    [ProducesResponseType(typeof(ApiResponse<VariantDetailDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IResult> AddVariant(
        [FromRoute] Guid id,
        [FromBody] CreateVariantRequest? request,
        [FromQuery] Guid? sellerId = null,
        CancellationToken cancellationToken = default)
    {
        var currentSellerId = HttpContext.GetCurrentSellerId(request?.SellerId ?? sellerId);
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

        if (request == null || string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.SkuCode))
        {
            return _responseFactory.FailureResult(
                HttpContext,
                title: "Validation error.",
                status: StatusCodes.Status400BadRequest,
                detail: "Variant name and SKU code are required.",
                errorCode: "VAL_INVALID_INPUT"
            );
        }

        var result = await _productService.AddVariantAsync(id, currentSellerId.Value, request, cancellationToken);
        return result.Status switch
        {
            VariantOperationStatus.NotFound => _responseFactory.FailureResult(
                HttpContext,
                title: "Product not found.",
                status: StatusCodes.Status404NotFound,
                detail: result.ErrorMessage ?? "Product was not found.",
                errorCode: "PRODUCT_NOT_FOUND"
            ),
            VariantOperationStatus.Forbidden => _responseFactory.FailureResult(
                HttpContext,
                title: "Forbidden.",
                status: StatusCodes.Status403Forbidden,
                detail: result.ErrorMessage ?? "You do not have permission to add variants to this product.",
                errorCode: "PERMISSION_DENIED"
            ),
            VariantOperationStatus.DuplicateSku => _responseFactory.FailureResult(
                HttpContext,
                title: "Conflict.",
                status: StatusCodes.Status409Conflict,
                detail: result.ErrorMessage ?? "SKU code already exists.",
                errorCode: "SKU_CODE_DUPLICATED"
            ),
            VariantOperationStatus.InvalidOperation => _responseFactory.FailureResult(
                HttpContext,
                title: "Validation error.",
                status: StatusCodes.Status400BadRequest,
                detail: result.ErrorMessage ?? "Invalid variant data.",
                errorCode: "VAL_INVALID_INPUT"
            ),
            _ => _responseFactory.SuccessResult(
                HttpContext,
                result.Data,
                "Variant created successfully.",
                StatusCodes.Status201Created)
        };
    }

    /// <summary>
    /// CAT-10: Set Default Variant
    /// </summary>
    [HttpPut("{id:guid}/default-variant")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IResult> SetDefaultVariant(
        [FromRoute] Guid id,
        [FromBody] SetDefaultVariantRequest? request,
        CancellationToken cancellationToken = default)
    {
        var currentSellerId = HttpContext.GetCurrentSellerId();
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

        if (request == null || request.VariantId == Guid.Empty)
        {
            return _responseFactory.FailureResult(
                HttpContext,
                title: "Validation error.",
                status: StatusCodes.Status400BadRequest,
                detail: "VariantId is required.",
                errorCode: "VAL_INVALID_INPUT"
            );
        }

        var result = await _productService.SetDefaultVariantAsync(id, currentSellerId.Value, request.VariantId, cancellationToken);
        return result.Status switch
        {
            VariantOperationStatus.NotFound => _responseFactory.FailureResult(
                HttpContext,
                title: "Not found.",
                status: StatusCodes.Status404NotFound,
                detail: result.ErrorMessage ?? "Product or variant not found.",
                errorCode: "PRODUCT_NOT_FOUND"
            ),
            VariantOperationStatus.Forbidden => _responseFactory.FailureResult(
                HttpContext,
                title: "Forbidden.",
                status: StatusCodes.Status403Forbidden,
                detail: result.ErrorMessage ?? "You do not have permission to modify this product.",
                errorCode: "PERMISSION_DENIED"
            ),
            VariantOperationStatus.InvalidOperation => _responseFactory.FailureResult(
                HttpContext,
                title: "Validation error.",
                status: StatusCodes.Status400BadRequest,
                detail: result.ErrorMessage ?? "Cannot set this variant as default.",
                errorCode: "VAL_INVALID_INPUT"
            ),
            _ => _responseFactory.SuccessResult(
                HttpContext,
                new { productId = id, defaultVariantId = result.Data },
                "Default variant updated successfully.")
        };
    }
}
