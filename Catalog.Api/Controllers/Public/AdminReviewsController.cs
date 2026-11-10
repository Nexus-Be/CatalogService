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
[Route("api/v1/admin/reviews")]
[Produces("application/json")]
public class AdminReviewsController : ControllerBase
{
    private readonly IProductReviewService _reviewService;

    public AdminReviewsController(IProductReviewService reviewService)
    {
        _reviewService = reviewService;
    }

    /// <summary>Moderates a product review (HIDE or PUBLISH) (CAT-65).</summary>
    [HttpPatch("{id:guid}/moderate")]
    [RequireRole("admin", "staff")]
    [ProducesResponseType(typeof(ProductReviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ModerateReview(
        [FromRoute] Guid id,
        [FromBody] ModerateReviewRequest request,
        CancellationToken ct)
    {
        if (!this.TryGetUserId(out var adminId, out var errorResult))
        {
            return errorResult;
        }

        if (request == null)
        {
            return this.BadRequestError("VAL_INVALID_INPUT", "Request body is required.");
        }

        var result = await _reviewService.ModerateReviewAsync(id, request, adminId, ct);
        return Ok(result);
    }
}

