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
using Catalog.Application.Services.Review;
using Catalog.Domain.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

public sealed class CreateProductReviewFormRequest
{
    [Required(ErrorMessage = "Order ID is required.")]
    public Guid OrderId { get; set; }

    [Range(1, 5, ErrorMessage = "Rating must be between 1 and 5 stars.")]
    public short Rating { get; set; }

    [StringLength(2000, ErrorMessage = "Comment cannot exceed 2000 characters.")]
    public string? Comment { get; set; }

    public List<IFormFile>? ReviewImages { get; set; }
}

[ApiController]
[Produces("application/json")]
public class ProductReviewsController : ControllerBase
{
    private static readonly string[] AllowedImageExtensions = { ".jpg", ".jpeg", ".png", ".webp" };
    private const long MaxImageFileSizeBytes = 5 * 1024 * 1024; // 5 MB

    private readonly IProductReviewService _reviewService;

    public ProductReviewsController(IProductReviewService reviewService)
    {
        _reviewService = reviewService;
    }

    /// <summary>Gets rating summary and star distribution of a product (CAT-60).</summary>
    [HttpGet("api/v1/products/{id:guid}/rating-summary")]
    [ProducesResponseType(typeof(ProductRatingSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRatingSummary(
        [FromRoute] Guid id,
        CancellationToken ct)
    {
        var result = await _reviewService.GetRatingSummaryAsync(id, ct);
        return Ok(result);
    }

    /// <summary>Lists public approved reviews of a product with pagination and filtering (CAT-61).</summary>
    [HttpGet("api/v1/products/{id:guid}/reviews")]
    [ProducesResponseType(typeof(PagedData<ProductReviewDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProductReviews(
        [FromRoute] Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] short? rating = null,
        [FromQuery] string? sortBy = "newest",
        CancellationToken ct = default)
    {
        if (page < 1 || pageSize < 1 || pageSize > 50)
        {
            return this.BadRequestError("VAL_INVALID_PAGINATION", "Page must be at least 1, and page size must be between 1 and 50.");
        }

        if (rating.HasValue && (rating.Value < 1 || rating.Value > 5))
        {
            return this.BadRequestError("VAL_INVALID_RATING", "Rating filter must be between 1 and 5.");
        }

        var query = new ReviewListQuery(page, pageSize, rating, sortBy);
        var result = await _reviewService.GetProductReviewsAsync(id, query, ct);
        return Ok(result);
    }

    /// <summary>Creates a product review with optional images after completing a purchase (CAT-62).</summary>
    [HttpPost("api/v1/products/{id:guid}/reviews")]
    [RequireRole("buyer", "admin")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ProductReviewDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateReview(
        [FromRoute] Guid id,
        [FromForm] CreateProductReviewFormRequest request,
        CancellationToken ct = default)
    {
        if (!this.TryGetUserId(out var currentUserId, out var errorResult))
        {
            return errorResult;
        }

        if (request == null)
        {
            return this.BadRequestError("VAL_INVALID_INPUT", "Request form data is required.");
        }

        if (request.OrderId == Guid.Empty)
        {
            return this.BadRequestError("VAL_INVALID_INPUT", "Order ID is required.");
        }

        var streams = new List<FileUploadModel>();
        try
        {
            if (request.ReviewImages != null && request.ReviewImages.Count > 0)
            {
                if (request.ReviewImages.Count > 5)
                {
                    throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "Maximum 5 review images are allowed.");
                }

                foreach (var file in request.ReviewImages)
                {
                    if (file == null || file.Length == 0)
                    {
                        continue;
                    }

                    if (file.Length > MaxImageFileSizeBytes)
                    {
                        throw new BusinessRuleValidationException("VAL_INVALID_INPUT", $"File '{file.FileName}' exceeds 5 MB limit.");
                    }

                    var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
                    if (!AllowedImageExtensions.Contains(ext))
                    {
                        throw new BusinessRuleValidationException("VAL_INVALID_INPUT", $"File '{file.FileName}' has unsupported extension '{ext}'. Only .jpg, .jpeg, .png, .webp are allowed.");
                    }

                    streams.Add(new FileUploadModel(file.OpenReadStream(), file.FileName));
                }
            }

            var serviceRequest = new CreateProductReviewRequest(request.OrderId, request.Rating, request.Comment);
            var result = await _reviewService.CreateReviewAsync(id, serviceRequest, currentUserId, streams.Count > 0 ? streams : null, ct);
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

