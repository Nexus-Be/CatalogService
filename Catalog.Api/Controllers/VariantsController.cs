namespace Catalog.Api.Controllers;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Catalog.Api.Extensions;
using Catalog.Api.Responses;
using Catalog.Application.Services.Product;

[ApiController]
[Route("api/v1/catalog/variants")]
[Route("api/v1/variants")]
[Produces("application/json")]
public sealed class VariantsController : ControllerBase
{
    private readonly IProductService _productService;
    private readonly IApiResponseFactory _responseFactory;

    public VariantsController(IProductService productService, IApiResponseFactory responseFactory)
    {
        _productService = productService;
        _responseFactory = responseFactory;
    }

    /// <summary>
    /// CAT-08: Update Variant
    /// </summary>
    [HttpPut("{id:guid}")]
    [HttpPatch("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<VariantDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IResult> UpdateVariant(
        [FromRoute] Guid id,
        [FromBody] UpdateVariantRequest? request,
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

        if (request == null || string.IsNullOrWhiteSpace(request.Name))
        {
            return _responseFactory.FailureResult(
                HttpContext,
                title: "Validation error.",
                status: StatusCodes.Status400BadRequest,
                detail: "Variant name is required.",
                errorCode: "VAL_INVALID_INPUT"
            );
        }

        var result = await _productService.UpdateVariantAsync(id, currentSellerId.Value, request, cancellationToken);
        return result.Status switch
        {
            VariantOperationStatus.NotFound => _responseFactory.FailureResult(
                HttpContext,
                title: "Variant not found.",
                status: StatusCodes.Status404NotFound,
                detail: result.ErrorMessage ?? "Variant was not found.",
                errorCode: "VARIANT_NOT_FOUND"
            ),
            VariantOperationStatus.Forbidden => _responseFactory.FailureResult(
                HttpContext,
                title: "Forbidden.",
                status: StatusCodes.Status403Forbidden,
                detail: result.ErrorMessage ?? "You do not have permission to modify this variant.",
                errorCode: "PERMISSION_DENIED"
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
                "Variant updated successfully.")
        };
    }

    /// <summary>
    /// CAT-09: Change Variant Status
    /// </summary>
    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType(typeof(ApiResponse<VariantDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IResult> ChangeVariantStatus(
        [FromRoute] Guid id,
        [FromBody] ChangeVariantStatusRequest? request,
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

        if (request == null || string.IsNullOrWhiteSpace(request.Status))
        {
            return _responseFactory.FailureResult(
                HttpContext,
                title: "Validation error.",
                status: StatusCodes.Status400BadRequest,
                detail: "Status is required (ACTIVE or INACTIVE).",
                errorCode: "VAL_INVALID_INPUT"
            );
        }

        var result = await _productService.ChangeVariantStatusAsync(id, currentSellerId.Value, request, cancellationToken);
        return result.Status switch
        {
            VariantOperationStatus.NotFound => _responseFactory.FailureResult(
                HttpContext,
                title: "Variant not found.",
                status: StatusCodes.Status404NotFound,
                detail: result.ErrorMessage ?? "Variant was not found.",
                errorCode: "VARIANT_NOT_FOUND"
            ),
            VariantOperationStatus.Forbidden => _responseFactory.FailureResult(
                HttpContext,
                title: "Forbidden.",
                status: StatusCodes.Status403Forbidden,
                detail: result.ErrorMessage ?? "You do not have permission to modify this variant.",
                errorCode: "PERMISSION_DENIED"
            ),
            VariantOperationStatus.InvalidOperation => _responseFactory.FailureResult(
                HttpContext,
                title: "Validation error.",
                status: StatusCodes.Status400BadRequest,
                detail: result.ErrorMessage ?? "Cannot change variant status.",
                errorCode: "VAL_INVALID_INPUT"
            ),
            _ => _responseFactory.SuccessResult(
                HttpContext,
                result.Data,
                "Variant status updated successfully.")
        };
    }

    /// <summary>
    /// CAT-11: Soft Delete Variant
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IResult> DeleteVariant(
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

        var result = await _productService.SoftDeleteVariantAsync(id, currentSellerId.Value, cancellationToken);
        return result.Status switch
        {
            VariantOperationStatus.NotFound => _responseFactory.FailureResult(
                HttpContext,
                title: "Variant not found.",
                status: StatusCodes.Status404NotFound,
                detail: result.ErrorMessage ?? "Variant was not found.",
                errorCode: "VARIANT_NOT_FOUND"
            ),
            VariantOperationStatus.Forbidden => _responseFactory.FailureResult(
                HttpContext,
                title: "Forbidden.",
                status: StatusCodes.Status403Forbidden,
                detail: result.ErrorMessage ?? "You do not have permission to delete this variant.",
                errorCode: "PERMISSION_DENIED"
            ),
            VariantOperationStatus.HasOrder => _responseFactory.FailureResult(
                HttpContext,
                title: "Conflict.",
                status: StatusCodes.Status409Conflict,
                detail: result.ErrorMessage ?? "Cannot delete variant with existing orders.",
                errorCode: "VARIANT_HAS_ORDER"
            ),
            VariantOperationStatus.LastVariant => _responseFactory.FailureResult(
                HttpContext,
                title: "Conflict.",
                status: StatusCodes.Status409Conflict,
                detail: result.ErrorMessage ?? "Cannot delete the only remaining variant of a product.",
                errorCode: "LAST_VARIANT"
            ),
            _ => _responseFactory.SuccessResult(
                HttpContext,
                new { deleted = true, id },
                "Variant deleted successfully.")
        };
    }
}

