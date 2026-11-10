namespace Catalog.Api.Controllers.Public;

using System;
using System.Threading;
using System.Threading.Tasks;
using Catalog.Api.Extensions;
using Catalog.Api.Filters;
using Catalog.Application.Services.Review;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/v1/reviews")]
[Produces("application/json")]
public class ReviewsController : ControllerBase
{
    private readonly IProductReviewService _reviewService;

    public ReviewsController(IProductReviewService reviewService)
    {
        _reviewService = reviewService;
    }

    /// <summary>Updates a product review (CAT-63).</summary>
    [HttpPut("{id:guid}")]
    [RequireRole("buyer", "admin")]
    [ProducesResponseType(typeof(ProductReviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateReview(
        [FromRoute] Guid id,
        [FromBody] UpdateProductReviewRequest request,
        CancellationToken ct)
    {
        if (!this.TryGetUserId(out var currentUserId, out var errorResult))
        {
            return errorResult;
        }

        if (request == null)
        {
            return this.BadRequestError("VAL_INVALID_INPUT", "Request body is required.");
        }

        var result = await _reviewService.UpdateReviewAsync(id, request, currentUserId, ct);
        return Ok(result);
    }

    /// <summary>Deletes a product review (CAT-64).</summary>
    [HttpDelete("{id:guid}")]
    [RequireRole("buyer", "admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteReview(
        [FromRoute] Guid id,
        CancellationToken ct)
    {
        if (!this.TryGetUserId(out var currentUserId, out var errorResult))
        {
            return errorResult;
        }

        var userRoles = this.GetUserRoles();
        var isAdmin = userRoles.Contains("admin", StringComparer.OrdinalIgnoreCase);

        await _reviewService.DeleteReviewAsync(id, currentUserId, isAdmin, ct);
        return NoContent();
    }
}

