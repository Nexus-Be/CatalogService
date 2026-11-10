namespace Catalog.Api.Controllers.Public;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Catalog.Api.Extensions;
using Catalog.Api.Filters;
using Catalog.Application.Common.Models;
using Catalog.Application.Services.Product;
using Catalog.Domain.Enums;
using Catalog.Domain.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/v1/products")]
[Produces("application/json")]
public class ProductsController : ControllerBase
{
    private static readonly string[] AllowedImageExtensions = { ".jpg", ".jpeg", ".png", ".webp" };
    private const long MaxImageFileSizeBytes = 5 * 1024 * 1024; // 5 MB

    private readonly IProductService _productService;

    public ProductsController(IProductService productService)
    {
        _productService = productService;
    }

    /// <summary>Searches and lists active products with filtering and pagination (CAT-06).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedData<ProductListItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedData<ProductListItemDto>>> Search(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? keyword = null,
        [FromQuery] Guid? categoryId = null,
        [FromQuery] Guid? sellerId = null,
        [FromQuery] SaleType? saleType = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDirection = "asc",
        [FromQuery] decimal? minPrice = null,
        [FromQuery] decimal? maxPrice = null,
        [FromQuery] string? brand = null,
        [FromQuery] decimal? minRating = null,
        CancellationToken ct = default)
    {
        var query = new ProductListQuery(
            page,
            pageSize,
            keyword,
            categoryId,
            sellerId,
            saleType,
            sortBy,
            sortDirection,
            minPrice,
            maxPrice,
            brand,
            minRating);

        var result = await _productService.SearchAsync(query, ct);
        return Ok(result);
    }

    /// <summary>Searches active products with filtering and pagination (CAT-20 / CAT-21 alias endpoint).</summary>
    [HttpGet("search")]
    [ProducesResponseType(typeof(PagedData<ProductListItemDto>), StatusCodes.Status200OK)]
    public Task<ActionResult<PagedData<ProductListItemDto>>> SearchAlias(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? keyword = null,
        [FromQuery] Guid? categoryId = null,
        [FromQuery] Guid? sellerId = null,
        [FromQuery] SaleType? saleType = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDirection = "asc",
        [FromQuery] decimal? minPrice = null,
        [FromQuery] decimal? maxPrice = null,
        [FromQuery] string? brand = null,
        [FromQuery] decimal? minRating = null,
        CancellationToken ct = default)
        => Search(page, pageSize, keyword, categoryId, sellerId, saleType, sortBy, sortDirection, minPrice, maxPrice, brand, minRating, ct);

    /// <summary>Gets full product details by ID (variants, images, rating).</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ProductDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductDetailDto>> GetById([FromRoute] Guid id, CancellationToken ct)
    {
        var product = await _productService.GetDetailAsync(id, ct);
        return Ok(product);
    }

    /// <summary>Creates a new regular product in Draft status (CAT-01). Can include initial variants.</summary>
    [HttpPost]
    [RequireRole("seller", "admin")]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create([FromBody] CreateProductRequest request, CancellationToken ct)
    {
        if (!this.TryGetUserId(out var currentUserId, out var errorResult))
        {
            return errorResult;
        }

        if (request.SellerId != Guid.Empty && request.SellerId != currentUserId)
        {
            return this.ForbiddenError("You cannot create products on behalf of another seller.");
        }

        var reqWithSeller = request with { SellerId = currentUserId };
        var product = await _productService.CreateAsync(reqWithSeller, ct);
        return StatusCode(StatusCodes.Status201Created, product);
    }

    /// <summary>Lists public active auction products with filtering and pagination (CAT-46).</summary>
    [HttpGet("auctions")]
    [ProducesResponseType(typeof(PagedData<AuctionProductListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ListAuctions(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? keyword = null,
        [FromQuery] Guid? categoryId = null,
        [FromQuery] Guid? sellerId = null,
        [FromQuery] string? status = null,
        [FromQuery] string? sortBy = null,
        CancellationToken ct = default)
    {
        if (page < 1 || pageSize < 1 || pageSize > 100)
        {
            return this.BadRequestError("VAL_INVALID_PAGINATION", "Page must be greater than or equal to 1, and pageSize must be between 1 and 100.");
        }

        var result = await _productService.SearchAuctionsAsync(
            new AuctionListQuery(page, pageSize, keyword, categoryId, sellerId, status, sortBy),
            ct);

        return Ok(result);
    }

    /// <summary>Gets public auction product details with single unpriced variant (CAT-47).</summary>
    [HttpGet("auctions/{id:guid}")]
    [ProducesResponseType(typeof(AuctionProductDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAuctionDetail([FromRoute] Guid id, CancellationToken ct)
    {
        var product = await _productService.GetAuctionDetailAsync(id, ct);
        return Ok(product);
    }

    /// <summary>Creates a new auction product in Draft status (CAT-43). Does not require variant price.</summary>
    [HttpPost("auction")]
    [RequireRole("seller", "admin")]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateAuction([FromBody] CreateAuctionProductRequest request, CancellationToken ct)
    {
        if (!this.TryGetUserId(out var currentUserId, out var errorResult))
        {
            return errorResult;
        }

        if (request.SellerId.HasValue && request.SellerId.Value != Guid.Empty && request.SellerId.Value != currentUserId)
        {
            return this.ForbiddenError("You cannot create products on behalf of another seller.");
        }

        var product = await _productService.CreateAuctionProductAsync(request, currentUserId, ct);
        return StatusCode(StatusCodes.Status201Created, product);
    }

    /// <summary>Updates auction product status (CAT-49). Locks permanently when transition to Auctioned.</summary>
    [HttpPatch("auctions/{id:guid}/status")]
    [RequireRole("seller", "admin")]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ChangeAuctionStatus(
        [FromRoute] Guid id,
        [FromBody] ChangeProductStatusRequest request,
        CancellationToken ct)
    {
        if (!this.TryGetUserId(out var currentUserId, out var errorResult))
        {
            return errorResult;
        }

        if (string.IsNullOrWhiteSpace(request?.Status))
        {
            return this.BadRequestError("VAL_INVALID_INPUT", "Status is required.");
        }

        var updated = await _productService.ChangeAuctionStatusAsync(id, request, currentUserId, ct);
        return Ok(updated);
    }

    /// <summary>Updates product general details (CAT-02).</summary>
    [HttpPut("{id:guid}")]
    [RequireRole("seller", "admin")]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update([FromRoute] Guid id, [FromBody] UpdateProductRequest request, CancellationToken ct)
    {
        if (!this.TryGetUserId(out var currentUserId, out var errorResult))
        {
            return errorResult;
        }

        var updated = await _productService.UpdateAsync(id, request, currentUserId, ct);
        return Ok(updated);
    }

    /// <summary>Updates product general details via partial patch (CAT-02).</summary>
    [HttpPatch("{id:guid}")]
    [RequireRole("seller", "admin")]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> Patch([FromRoute] Guid id, [FromBody] UpdateProductRequest request, CancellationToken ct)
        => Update(id, request, ct);

    /// <summary>Changes product status (DRAFT, ACTIVE, INACTIVE) (CAT-03). Requires product owner or admin.</summary>
    [HttpPatch("{id:guid}/status")]
    [RequireRole("seller", "admin")]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ChangeStatus([FromRoute] Guid id, [FromBody] ChangeProductStatusRequest request, CancellationToken ct)
    {
        if (!this.TryGetUserId(out var currentUserId, out var errorResult))
        {
            return errorResult;
        }

        var updated = await _productService.ChangeStatusAsync(id, request, currentUserId, ct);
        return Ok(updated);
    }

    /// <summary>Soft deletes a product and all its variants (CAT-04). Requires product owner or admin.</summary>
    [HttpDelete("{id:guid}")]
    [RequireRole("seller", "admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete([FromRoute] Guid id, CancellationToken ct)
    {
        if (!this.TryGetUserId(out var currentUserId, out var errorResult))
        {
            return errorResult;
        }

        await _productService.SoftDeleteAsync(id, currentUserId, ct);
        return NoContent();
    }

    // -------------------------------------------------------------
    // Image Management (CAT-12, CAT-13, CAT-40)
    // -------------------------------------------------------------

    /// <summary>Gets all images of a product (CAT-40).</summary>
    [HttpGet("{id:guid}/images")]
    [ProducesResponseType(typeof(IReadOnlyList<ProductImageDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetImages([FromRoute] Guid id, CancellationToken ct)
    {
        var images = await _productService.GetProductImagesAsync(id, ct);
        return Ok(images);
    }

    /// <summary>Uploads an image for a product via Cloudinary (CAT-12). Max 5MB, format .jpg/.jpeg/.png/.webp.</summary>
    [HttpPost("{id:guid}/images")]
    [RequireRole("seller", "admin")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ProductImageDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UploadImage(
        [FromRoute] Guid id,
        IFormFile file,
        [FromForm] Guid? variantId = null,
        [FromForm] string? altText = null,
        [FromForm] short sortOrder = 0,
        CancellationToken ct = default)
    {
        if (!this.TryGetUserId(out var currentUserId, out var errorResult))
        {
            return errorResult;
        }

        if (file == null || file.Length == 0)
        {
            throw new BusinessRuleValidationException("VAL_INVALID_FILE", "Please select an image file to upload.");
        }

        if (file.Length > MaxImageFileSizeBytes)
        {
            throw new BusinessRuleValidationException("VAL_LIMIT_EXCEEDED", "Image file size exceeds the 5MB limit.");
        }

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedImageExtensions.Contains(ext))
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "Invalid image format. Supported formats: .jpg, .jpeg, .png, .webp.");
        }

        using var stream = file.OpenReadStream();
        var uploadedImage = await _productService.AddProductImageAsync(
            id,
            currentUserId,
            stream,
            file.FileName,
            variantId,
            altText,
            sortOrder,
            ct);

        return StatusCode(StatusCodes.Status201Created, uploadedImage);
    }

    /// <summary>Deletes an image from a product (CAT-13 sub-route).</summary>
    [HttpDelete("{id:guid}/images/{imageId:guid}")]
    [RequireRole("seller", "admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteImage(
        [FromRoute] Guid id,
        [FromRoute] Guid imageId,
        CancellationToken ct)
    {
        if (!this.TryGetUserId(out var currentUserId, out var errorResult))
        {
            return errorResult;
        }

        await _productService.DeleteProductImageAsync(id, imageId, currentUserId, ct);
        return NoContent();
    }

    // -------------------------------------------------------------
    // Variant Management (CAT-07, CAT-10)
    // -------------------------------------------------------------

    /// <summary>Adds a new variant/SKU to a product (CAT-07). Max 10 variants allowed.</summary>
    [HttpPost("{id:guid}/variants")]
    [RequireRole("seller", "admin")]
    [ProducesResponseType(typeof(VariantDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddVariant(
        [FromRoute] Guid id,
        [FromBody] CreateVariantRequest request,
        CancellationToken ct)
    {
        if (!this.TryGetUserId(out var currentUserId, out var errorResult))
        {
            return errorResult;
        }

        var variant = await _productService.AddVariantAsync(id, currentUserId, request, ct);
        return StatusCode(StatusCodes.Status201Created, variant);
    }

    /// <summary>Sets a variant as the default variant for the product (CAT-10).</summary>
    [HttpPut("{id:guid}/default-variant")]
    [RequireRole("seller", "admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetDefaultVariant(
        [FromRoute] Guid id,
        [FromBody] SetDefaultVariantRequest request,
        CancellationToken ct)
    {
        if (!this.TryGetUserId(out var currentUserId, out var errorResult))
        {
            return errorResult;
        }

        var defaultId = await _productService.SetDefaultVariantAsync(id, currentUserId, request.VariantId, ct);
        return Ok(new { defaultVariantId = defaultId });
    }
}
