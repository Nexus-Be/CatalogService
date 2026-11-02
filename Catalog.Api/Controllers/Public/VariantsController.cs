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
}
