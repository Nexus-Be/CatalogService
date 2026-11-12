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

public sealed class UpdateProductReviewFormRequest
{
    [Range(1, 5, ErrorMessage = "Rating must be between 1 and 5 stars.")]
    public short Rating { get; set; }

    [StringLength(2000, ErrorMessage = "Comment cannot exceed 2000 characters.")]
    public string? Comment { get; set; }

    public List<IFormFile>? ReviewImages { get; set; }
}

[ApiController]
[Route("api/v1/reviews")]
[Produces("application/json")]
public class ReviewsController : ControllerBase
{
    private static readonly string[] AllowedImageExtensions = { ".jpg", ".jpeg", ".png", ".webp" };
    private const long MaxImageFileSizeBytes = 5 * 1024 * 1024; // 5 MB

    private readonly IProductReviewService _reviewService;

    public ReviewsController(IProductReviewService reviewService)
    {
        _reviewService = reviewService;
    }

    /// <summary>Updates a product review with optional new images (CAT-63).</summary>
    [HttpPut("{id:guid}")]
    [RequireRole("buyer", "admin")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ProductReviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateReview(
        [FromRoute] Guid id,
        [FromForm] UpdateProductReviewFormRequest request,
        CancellationToken ct)
    {
        if (!this.TryGetUserId(out var currentUserId, out var errorResult))
        {
            return errorResult;
        }

        if (request == null)
        {
            return this.BadRequestError("VAL_INVALID_INPUT", "Request form data is required.");
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

            var serviceRequest = new UpdateProductReviewRequest(request.Rating, request.Comment);
            var result = await _reviewService.UpdateReviewAsync(id, serviceRequest, currentUserId, request.ReviewImages != null ? streams : null, ct);
            return Ok(result);
        }
        finally
        {
            foreach (var item in streams)
            {
                item.Stream?.Dispose();
            }
        }
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

