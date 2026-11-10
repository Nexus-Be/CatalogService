namespace Catalog.Api.Controllers.Public;

using System;
using System.Threading;
using System.Threading.Tasks;
using Catalog.Api.Extensions;
using Catalog.Api.Filters;
using Catalog.Application.Services.Report;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/v1/products/{id:guid}/reports")]
[Produces("application/json")]
public class ProductReportsController : ControllerBase
{
    private readonly IProductReportService _reportService;

    public ProductReportsController(IProductReportService reportService)
    {
        _reportService = reportService;
    }

    /// <summary>Submits a violation report for a product (CAT-57).</summary>
    [HttpPost]
    [RequireRole("buyer", "seller")]
    [ProducesResponseType(typeof(ProductReportDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SubmitReport(
        [FromRoute] Guid id,
        [FromBody] CreateProductReportRequest request,
        CancellationToken ct)
    {
        if (!this.TryGetUserId(out var reporterId, out var errorResult))
        {
            return errorResult;
        }

        if (request == null)
        {
            return this.BadRequestError("VAL_INVALID_INPUT", "Request body is required.");
        }

        var result = await _reportService.SubmitProductReportAsync(id, request, reporterId, ct);
        return StatusCode(StatusCodes.Status201Created, result);
    }
}
