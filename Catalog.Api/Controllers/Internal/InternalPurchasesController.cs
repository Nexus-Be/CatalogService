namespace Catalog.Api.Controllers.Internal;

using System;
using System.Threading;
using System.Threading.Tasks;
using Catalog.Api.Extensions;
using Catalog.Application.Services.Review;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/v1/internal/purchases")]
[Produces("application/json")]
public class InternalPurchasesController : ControllerBase
{
    private readonly IProductReviewService _reviewService;

    public InternalPurchasesController(IProductReviewService reviewService)
    {
        _reviewService = reviewService;
    }

    /// <summary>Syncs a completed purchase record from OrderService (CAT-66).</summary>
    [HttpPost]
    [ProducesResponseType(typeof(PurchaseRecordDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SyncPurchase(
        [FromBody] SyncPurchaseRecordRequest request,
        CancellationToken ct)
    {
        if (request == null)
        {
            return this.BadRequestError("VAL_INVALID_INPUT", "Request body is required.");
        }

        if (request.BuyerId == Guid.Empty || request.OrderId == Guid.Empty || request.ProductId == Guid.Empty)
        {
            return this.BadRequestError("VAL_INVALID_INPUT", "All identification fields (BuyerId, OrderId, ProductId) are required.");
        }

        var result = await _reviewService.SyncPurchaseRecordAsync(request, ct);
        return StatusCode(StatusCodes.Status201Created, result);
    }
}

