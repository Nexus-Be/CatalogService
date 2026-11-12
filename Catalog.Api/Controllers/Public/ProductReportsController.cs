namespace Catalog.Api.Controllers.Public;

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Catalog.Api.Extensions;
using Catalog.Api.Filters;
using Catalog.Application.Common.Models;
using Catalog.Application.Services.Report;
using Catalog.Domain.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

public sealed class SubmitProductReportFormRequest
{
    [Required(ErrorMessage = "Report reason is required.")]
    public string Reason { get; set; } = string.Empty;

    [Required(ErrorMessage = "Description is required.")]
    [StringLength(2000, MinimumLength = 10, ErrorMessage = "Description must be between 10 and 2000 characters.")]
    public string Description { get; set; } = string.Empty;

    public List<IFormFile>? EvidenceFiles { get; set; }
}

[ApiController]
[Route("api/v1/products/{id:guid}/reports")]
[Produces("application/json")]
public class ProductReportsController : ControllerBase
{
    private static readonly string[] AllowedImageExtensions = { ".jpg", ".jpeg", ".png", ".webp" };
    private const long MaxImageFileSizeBytes = 5 * 1024 * 1024; // 5 MB

    private readonly IProductReportService _reportService;

    public ProductReportsController(IProductReportService reportService)
    {
        _reportService = reportService;
    }

    /// <summary>Submits a violation report for a product with evidence image uploads (CAT-57).</summary>
    [HttpPost]
    [RequireRole("buyer", "seller")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ProductReportDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SubmitReport(
        [FromRoute] Guid id,
        [FromForm] SubmitProductReportFormRequest request,
        CancellationToken ct)
    {
        if (!this.TryGetUserId(out var reporterId, out var errorResult))
        {
            return errorResult;
        }

        if (request == null)
        {
            return this.BadRequestError("VAL_INVALID_INPUT", "Request form data is required.");
        }

        if (request.EvidenceFiles == null || request.EvidenceFiles.Count == 0 || request.EvidenceFiles.All(f => f == null || f.Length == 0))
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "At least one evidence image is required to submit a report.");
        }

        var streams = new List<FileUploadModel>();
        try
        {
            if (request.EvidenceFiles != null && request.EvidenceFiles.Count > 0)
            {
                if (request.EvidenceFiles.Count > 5)
                {
                    throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "Maximum 5 evidence images are allowed.");
                }

                foreach (var file in request.EvidenceFiles)
                {
                    if (file == null || file.Length == 0) continue;

                    if (file.Length > MaxImageFileSizeBytes)
                    {
                        throw new BusinessRuleValidationException("VAL_LIMIT_EXCEEDED", $"Image file '{file.FileName}' exceeds the 5MB limit.");
                    }

                    var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
                    if (!AllowedImageExtensions.Contains(ext))
                    {
                        throw new BusinessRuleValidationException("VAL_INVALID_INPUT", $"Invalid image format for '{file.FileName}'. Supported formats: .jpg, .jpeg, .png, .webp.");
                    }

                    streams.Add(new FileUploadModel(file.OpenReadStream(), file.FileName));
                }
            }

            var appRequest = new CreateProductReportRequest(request.Reason, request.Description);
            var result = await _reportService.SubmitProductReportAsync(id, appRequest, reporterId, streams, ct);
            return StatusCode(StatusCodes.Status201Created, result);
        }
        finally
        {
            foreach (var item in streams)
            {
                item.Stream?.Dispose();
            }
        }
    }
}
