namespace Catalog.Api.Controllers.Public;

using System;
using System.Threading;
using System.Threading.Tasks;
using Catalog.Api.Extensions;
using Catalog.Api.Filters;
using Catalog.Application.Common.Models;
using Catalog.Application.Services.Report;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/v1/admin/reports/products")]
[Produces("application/json")]
public class AdminProductReportsController : ControllerBase
{
    private readonly IProductReportService _reportService;

    public AdminProductReportsController(IProductReportService reportService)
    {
        _reportService = reportService;
    }

    /// <summary>Lists and searches violation reports for products in moderation queue (CAT-58).</summary>
    [HttpGet]
    [RequireRole("admin", "staff")]
    [ProducesResponseType(typeof(PagedData<ProductReportListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAdminReports(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        [FromQuery] string? reason = null,
        [FromQuery] Guid? productId = null,
        [FromQuery] Guid? sellerId = null,
        [FromQuery] string? sortBy = "createdAt",
        [FromQuery] string? sortDirection = "desc",
        CancellationToken ct = default)
    {
        if (page < 1 || pageSize < 1 || pageSize > 100)
        {
            return this.BadRequestError("VAL_INVALID_PAGINATION", "Page must be greater than or equal to 1, and pageSize must be between 1 and 100.");
        }

        var query = new AdminReportSearchQuery(
            page,
            pageSize,
            status,
            reason,
            productId,
            sellerId,
            sortBy,
            sortDirection);

        var result = await _reportService.GetAdminReportsAsync(query, ct);
        return Ok(result);
    }

    /// <summary>Moderates a product report: ban product, dismiss, or investigate (CAT-59).</summary>
    [HttpPut("{reportId:guid}/action")]
    [RequireRole("admin", "staff")]
    [ProducesResponseType(typeof(ProductReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ModerateReport(
        [FromRoute] Guid reportId,
        [FromBody] ModerateProductReportRequest request,
        CancellationToken ct = default)
    {
        if (!this.TryGetUserId(out var adminId, out var errorResult))
        {
            return errorResult;
        }

        if (string.IsNullOrWhiteSpace(request?.Action))
        {
            return this.BadRequestError("VAL_INVALID_INPUT", "Action is required. Allowed values: BAN_PRODUCT, DISMISS, INVESTIGATE.");
        }

        if (string.IsNullOrWhiteSpace(request.ResolutionNote))
        {
            return this.BadRequestError("VAL_INVALID_INPUT", "Resolution note is required.");
        }

        var result = await _reportService.ModerateReportAsync(reportId, request, adminId, ct);
        return Ok(result);
    }
}
