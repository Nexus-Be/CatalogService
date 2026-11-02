namespace Catalog.Api.Controllers.Public;

using System;
using System.Threading;
using System.Threading.Tasks;
using Catalog.Api.Extensions;
using Catalog.Api.Filters;
using Catalog.Application.Services.Product;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/v1/images")]
[Produces("application/json")]
public class ImagesController : ControllerBase
{
    private readonly IProductService _productService;

    public ImagesController(IProductService productService)
    {
        _productService = productService;
    }

    /// <summary>Updates product image primary flag, sort order, or alt text (CAT-14).</summary>
    [HttpPatch("{id:guid}")]
    [RequireRole("seller", "admin")]
    [ProducesResponseType(typeof(ProductImageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateImage(
        [FromRoute] Guid id,
        [FromBody] UpdateProductImageRequest request,
        CancellationToken ct)
    {
        if (!this.TryGetUserId(out var currentUserId, out var errorResult))
        {
            return errorResult;
        }

        var image = await _productService.UpdateImageAsync(id, request, currentUserId, ct);
        return Ok(image);
    }

    /// <summary>Deletes a product image by ID, auto-transferring primary status if necessary (CAT-13).</summary>
    [HttpDelete("{id:guid}")]
    [RequireRole("seller", "admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteImage(
        [FromRoute] Guid id,
        CancellationToken ct)
    {
        if (!this.TryGetUserId(out var currentUserId, out var errorResult))
        {
            return errorResult;
        }

        await _productService.DeleteImageByIdAsync(id, currentUserId, ct);
        return NoContent();
    }
}
