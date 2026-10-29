namespace Catalog.Api.Controllers;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Catalog.Api.Responses;
using Catalog.Application.Services.Product;

[ApiController]
[Route("api/v1/internal/variants")]
[Produces("application/json")]
public sealed class InternalVariantsController : ControllerBase
{
    private static readonly HashSet<string> ValidPurposes = new(StringComparer.OrdinalIgnoreCase)
    {
        "CART",
        "CHECKOUT",
        "AUCTION"
    };

    private readonly IProductService _productService;
    private readonly IApiResponseFactory _responseFactory;

    public InternalVariantsController(IProductService productService, IApiResponseFactory responseFactory)
    {
        _productService = productService;
        _responseFactory = responseFactory;
    }

    /// <summary>
    /// CAT-24: Batch get variant/SKU details for Cart, Order, and Fulfillment.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<BatchVariantResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IResult> GetVariantsBatch(
        [FromBody] BatchGetVariantsRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (request?.VariantIds == null || request.VariantIds.Count == 0)
        {
            return _responseFactory.FailureResult(
                HttpContext,
                title: "Validation error.",
                status: StatusCodes.Status400BadRequest,
                detail: "At least one variant ID must be provided.",
                errorCode: "VAL_INVALID_INPUT"
            );
        }

        if (request.VariantIds.Count > 100)
        {
            return _responseFactory.FailureResult(
                HttpContext,
                title: "Limit exceeded.",
                status: StatusCodes.Status400BadRequest,
                detail: "A maximum of 100 variants can be queried at once.",
                errorCode: "VAL_LIMIT_EXCEEDED"
            );
        }

        var result = await _productService.GetVariantsBatchAsync(request, cancellationToken);
        return _responseFactory.SuccessResult(HttpContext, result, "Variants retrieved successfully.");
    }

    /// <summary>
    /// CAT-39: Check purchasable status of variants for Cart, Checkout, or Auction.
    /// </summary>
    [HttpPost("check-status")]
    [ProducesResponseType(typeof(ApiResponse<CheckVariantStatusResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IResult> CheckVariantStatus(
        [FromBody] CheckVariantStatusRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (request?.VariantIds == null || request.VariantIds.Count == 0)
        {
            return _responseFactory.FailureResult(
                HttpContext,
                title: "Validation error.",
                status: StatusCodes.Status400BadRequest,
                detail: "At least one variant ID must be provided.",
                errorCode: "VAL_INVALID_INPUT"
            );
        }

        if (request.VariantIds.Count > 100)
        {
            return _responseFactory.FailureResult(
                HttpContext,
                title: "Limit exceeded.",
                status: StatusCodes.Status400BadRequest,
                detail: "A maximum of 100 variants can be checked at once.",
                errorCode: "VAL_LIMIT_EXCEEDED"
            );
        }

        if (string.IsNullOrWhiteSpace(request.Purpose) || !ValidPurposes.Contains(request.Purpose.Trim()))
        {
            return _responseFactory.FailureResult(
                HttpContext,
                title: "Validation error.",
                status: StatusCodes.Status400BadRequest,
                detail: "Purpose must be one of: CART, CHECKOUT, AUCTION.",
                errorCode: "VAL_INVALID_INPUT"
            );
        }

        var result = await _productService.CheckVariantStatusBatchAsync(request, cancellationToken);
        return _responseFactory.SuccessResult(HttpContext, result, "Variant statuses checked successfully.");
    }
}
