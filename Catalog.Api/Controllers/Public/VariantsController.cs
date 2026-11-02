namespace Catalog.Api.Controllers.Public;

using System;
using System.Threading;
using System.Threading.Tasks;
using Catalog.Api.Extensions;
using Catalog.Api.Filters;
using Catalog.Application.Services.Product;
using Catalog.Domain.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/v1/variants")]
[Produces("application/json")]
public class VariantsController : ControllerBase
{
    private readonly IProductService _productService;

    public VariantsController(IProductService productService)
    {
        _productService = productService;
    }

    /// <summary>Gets variant details by ID (CAT-42).</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(VariantDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetVariantById(
        [FromRoute] Guid id,
        CancellationToken ct)
    {
        var variant = await _productService.GetVariantByIdAsync(id, ct);
        return Ok(variant);
    }

    /// <summary>Updates a product variant's information (CAT-08).</summary>
    [HttpPut("{id:guid}")]
    [HttpPatch("{id:guid}")]
    [RequireRole("seller", "admin")]
    [ProducesResponseType(typeof(VariantDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateVariant(
        [FromRoute] Guid id,
        [FromBody] UpdateVariantRequest request,
        CancellationToken ct)
    {
        if (!this.TryGetUserId(out var currentUserId, out var errorResult))
        {
            return errorResult;
        }

        var variant = await _productService.UpdateVariantAsync(id, currentUserId, request, ct);
        return Ok(variant);
    }

    /// <summary>Changes a variant's status (ACTIVE or INACTIVE with reason) (CAT-09).</summary>
    [HttpPatch("{id:guid}/status")]
    [RequireRole("seller", "admin")]
    [ProducesResponseType(typeof(VariantDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ChangeVariantStatus(
        [FromRoute] Guid id,
        [FromBody] ChangeVariantStatusRequest request,
        CancellationToken ct)
    {
        if (!this.TryGetUserId(out var currentUserId, out var errorResult))
        {
            return errorResult;
        }

        var variant = await _productService.ChangeVariantStatusAsync(id, currentUserId, request, ct);
        return Ok(variant);
    }

    /// <summary>Soft deletes a product variant if it has no orders and is not the last variant (CAT-11).</summary>
    [HttpDelete("{id:guid}")]
    [RequireRole("seller", "admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteVariant(
        [FromRoute] Guid id,
        CancellationToken ct)
    {
        if (!this.TryGetUserId(out var currentUserId, out var errorResult))
        {
            return errorResult;
        }

        await _productService.SoftDeleteVariantAsync(id, currentUserId, ct);
        return NoContent();
    }

    private static readonly string[] AllowedImageExtensions = { ".jpg", ".jpeg", ".png", ".webp" };
    private const long MaxImageFileSizeBytes = 5 * 1024 * 1024; // 5 MB

    /// <summary>Gets all images of a variant with fallback to product primary image.</summary>
    [HttpGet("{id:guid}/images")]
    [ProducesResponseType(typeof(IReadOnlyList<ProductImageDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetVariantImages([FromRoute] Guid id, CancellationToken ct)
    {
        var images = await _productService.GetVariantImagesAsync(id, ct);
        return Ok(images);
    }

    /// <summary>Uploads an image specifically for a variant via Cloudinary. Max 3 images per variant, max 5MB.</summary>
    [HttpPost("{id:guid}/images")]
    [RequireRole("seller")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ProductImageDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UploadVariantImage(
        [FromRoute] Guid id,
        IFormFile file,
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

        var ext = System.IO.Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!System.Linq.Enumerable.Contains(AllowedImageExtensions, ext))
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "Invalid image format. Supported formats: .jpg, .jpeg, .png, .webp.");
        }

        using var stream = file.OpenReadStream();
        var uploadedImage = await _productService.AddVariantImageAsync(
            id,
            currentUserId,
            stream,
            file.FileName,
            altText,
            sortOrder,
            ct);

        return StatusCode(StatusCodes.Status201Created, uploadedImage);
    }

    /// <summary>Deletes an image from a variant.</summary>
    [HttpDelete("{id:guid}/images/{imageId:guid}")]
    [RequireRole("seller")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteVariantImage(
        [FromRoute] Guid id,
        [FromRoute] Guid imageId,
        CancellationToken ct)
    {
        if (!this.TryGetUserId(out var currentUserId, out var errorResult))
        {
            return errorResult;
        }

        await _productService.DeleteImageByIdAsync(imageId, currentUserId, ct);
        return NoContent();
    }
}