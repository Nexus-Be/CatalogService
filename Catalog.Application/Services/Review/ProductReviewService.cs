namespace Catalog.Application.Services.Review;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Catalog.Domain.Exceptions;
using Catalog.Application.Common.Extensions;
using Catalog.Application.Common.Models;
using Catalog.Application.Interfaces;
using Catalog.Domain.Entities;
using Catalog.Domain.Enums;

public sealed class ProductReviewService : IProductReviewService
{
    private readonly ICatalogDbContext _dbContext;
    private readonly ILogger<ProductReviewService> _logger;

    public ProductReviewService(
        ICatalogDbContext dbContext,
        ILogger<ProductReviewService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<ProductRatingSummaryDto> GetRatingSummaryAsync(
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        var productExists = await _dbContext.Products
            .AnyAsync(p => p.Id == productId && !p.IsDeleted, cancellationToken);

        if (!productExists)
        {
            throw new EntityNotFoundException("PRODUCT_NOT_FOUND", "Product", productId);
        }

        var stats = await _dbContext.ProductRatingStats
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.ProductId == productId && !s.IsDeleted, cancellationToken);

        var starCounts = await _dbContext.ProductReviews
            .AsNoTracking()
            .Where(r => r.ProductId == productId && r.Status == ReviewStatus.Published && !r.IsDeleted)
            .GroupBy(r => r.Rating)
            .Select(g => new { Rating = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        int star5 = starCounts.FirstOrDefault(x => x.Rating == 5)?.Count ?? 0;
        int star4 = starCounts.FirstOrDefault(x => x.Rating == 4)?.Count ?? 0;
        int star3 = starCounts.FirstOrDefault(x => x.Rating == 3)?.Count ?? 0;
        int star2 = starCounts.FirstOrDefault(x => x.Rating == 2)?.Count ?? 0;
        int star1 = starCounts.FirstOrDefault(x => x.Rating == 1)?.Count ?? 0;

        return new ProductRatingSummaryDto(
            productId,
            stats?.AvgRating,
            stats?.ReviewCount ?? 0,
            stats?.StarSum ?? 0,
            new StarDistributionDto(star5, star4, star3, star2, star1));
    }

    public async Task<PagedData<ProductReviewDto>> GetProductReviewsAsync(
        Guid productId,
        ReviewListQuery query,
        CancellationToken cancellationToken = default)
    {
        var productExists = await _dbContext.Products
            .AnyAsync(p => p.Id == productId && !p.IsDeleted, cancellationToken);

        if (!productExists)
        {
            throw new EntityNotFoundException("PRODUCT_NOT_FOUND", "Product", productId);
        }

        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize < 1 ? 10 : (query.PageSize > 50 ? 50 : query.PageSize);

        if (query.Rating.HasValue && (query.Rating.Value < 1 || query.Rating.Value > 5))
        {
            throw new BusinessRuleValidationException("VAL_INVALID_RATING", "Rating filter must be between 1 and 5.");
        }

        var dbQuery = _dbContext.ProductReviews
            .AsNoTracking()
            .Where(r => r.ProductId == productId && r.Status == ReviewStatus.Published && !r.IsDeleted);

        if (query.Rating.HasValue)
        {
            dbQuery = dbQuery.Where(r => r.Rating == query.Rating.Value);
        }

        dbQuery = (query.SortBy?.ToLowerInvariant()) switch
        {
            "highest_rating" => dbQuery.OrderByDescending(r => r.Rating).ThenByDescending(r => r.CreatedAt),
            "lowest_rating" => dbQuery.OrderBy(r => r.Rating).ThenByDescending(r => r.CreatedAt),
            _ => dbQuery.OrderByDescending(r => r.CreatedAt)
        };

        var totalItems = await dbQuery.CountAsync(cancellationToken);
        var items = await dbQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new ProductReviewDto(
                r.Id,
                r.ProductId,
                r.BuyerId,
                r.Rating,
                r.Comment,
                r.Status.ToString(),
                r.CreatedAt,
                r.UpdatedAt))
            .ToListAsync(cancellationToken);

        return new PagedData<ProductReviewDto>(items, PaginationMetadata.Create(page, pageSize, totalItems));
    }

    public async Task<ProductReviewDto> CreateReviewAsync(
        Guid productId,
        CreateProductReviewRequest request,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (request.Rating < 1 || request.Rating > 5)
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "Rating must be between 1 and 5 stars.");
        }

        var product = await _dbContext.Products
            .FirstOrDefaultAsync(p => p.Id == productId && !p.IsDeleted, cancellationToken)
            ?? throw new EntityNotFoundException("PRODUCT_NOT_FOUND", "Product", productId);

        if (product.Status == ProductStatus.Banned)
        {
            throw new BusinessRuleValidationException("PRODUCT_BANNED", "Cannot review a banned product.", 400);
        }

        var purchase = await _dbContext.PurchaseRecords
            .FirstOrDefaultAsync(p => p.BuyerId == currentUserId
                                   && p.ProductId == productId
                                   && p.OrderId == request.OrderId
                                   && !p.IsDeleted, cancellationToken);

        if (purchase == null)
        {
            throw new BusinessRuleValidationException(
                "PURCHASE_NOT_FOUND",
                "You can only review products from your completed purchases.",
                403);
        }

        var alreadyReviewed = await _dbContext.ProductReviews
            .AnyAsync(r => r.PurchaseRecordId == purchase.Id && !r.IsDeleted, cancellationToken);

        if (alreadyReviewed)
        {
            throw new BusinessRuleValidationException(
                "ALREADY_REVIEWED",
                "This purchase has already been reviewed.",
                409);
        }

        var now = DateTimeOffset.UtcNow;
        var review = new ProductReview
        {
            Id = Guid.NewGuid(),
            PurchaseRecordId = purchase.Id,
            ProductId = productId,
            BuyerId = currentUserId,
            Rating = request.Rating,
            Comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim(),
            Status = ReviewStatus.Published,
            CreatedAt = now,
            UpdatedAt = now,
            IsDeleted = false
        };

        _dbContext.ProductReviews.Add(review);

        var stats = await _dbContext.ProductRatingStats
            .FirstOrDefaultAsync(s => s.ProductId == productId && !s.IsDeleted, cancellationToken);

        if (stats == null)
        {
            stats = new ProductRatingStats
            {
                Id = Guid.NewGuid(),
                ProductId = productId,
                StarSum = request.Rating,
                ReviewCount = 1,
                AvgRating = request.Rating,
                CreatedAt = now,
                UpdatedAt = now,
                IsDeleted = false
            };
            _dbContext.ProductRatingStats.Add(stats);
        }
        else
        {
            stats.StarSum += request.Rating;
            stats.ReviewCount += 1;
            stats.AvgRating = Math.Round((decimal)stats.StarSum / stats.ReviewCount, 2);
            stats.UpdatedAt = now;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "[ProductReviewService] Created review '{ReviewId}' for product '{ProductId}' by buyer '{BuyerId}'. Rating: {Rating}, New Avg: {AvgRating}",
            review.Id, productId, currentUserId, request.Rating, stats.AvgRating);

        return new ProductReviewDto(
            review.Id,
            review.ProductId,
            review.BuyerId,
            review.Rating,
            review.Comment,
            review.Status.ToString(),
            review.CreatedAt,
            null);
    }

    public async Task<ProductReviewDto> UpdateReviewAsync(
        Guid reviewId,
        UpdateProductReviewRequest request,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (request.Rating < 1 || request.Rating > 5)
        {
            throw new BusinessRuleValidationException("VAL_INVALID_INPUT", "Rating must be between 1 and 5 stars.");
        }

        var review = await _dbContext.ProductReviews
            .FirstOrDefaultAsync(r => r.Id == reviewId && !r.IsDeleted, cancellationToken)
            ?? throw new EntityNotFoundException("REVIEW_NOT_FOUND", "ProductReview", reviewId);

        if (review.BuyerId != currentUserId)
        {
            throw new BusinessRuleValidationException("FORBIDDEN", "You can only edit your own reviews.", 403);
        }

        if (review.Status == ReviewStatus.Hidden)
        {
            throw new BusinessRuleValidationException("REVIEW_HIDDEN", "Cannot edit a review that has been hidden by moderators.", 400);
        }

        var now = DateTimeOffset.UtcNow;
        var oldRating = review.Rating;
        review.Rating = request.Rating;
        review.Comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim();
        review.UpdatedAt = now;

        if (oldRating != request.Rating)
        {
            var stats = await _dbContext.ProductRatingStats
                .FirstOrDefaultAsync(s => s.ProductId == review.ProductId && !s.IsDeleted, cancellationToken);

            if (stats != null && stats.ReviewCount > 0)
            {
                stats.StarSum = stats.StarSum - oldRating + request.Rating;
                stats.AvgRating = Math.Round((decimal)stats.StarSum / stats.ReviewCount, 2);
                stats.UpdatedAt = now;
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "[ProductReviewService] Updated review '{ReviewId}' by buyer '{BuyerId}'. Rating changed: {OldRating} -> {NewRating}",
            review.Id, currentUserId, oldRating, request.Rating);

        return new ProductReviewDto(
            review.Id,
            review.ProductId,
            review.BuyerId,
            review.Rating,
            review.Comment,
            review.Status.ToString(),
            review.CreatedAt,
            review.UpdatedAt);
    }

    public async Task DeleteReviewAsync(
        Guid reviewId,
        Guid currentUserId,
        bool isAdmin,
        CancellationToken cancellationToken = default)
    {
        var review = await _dbContext.ProductReviews
            .FirstOrDefaultAsync(r => r.Id == reviewId && !r.IsDeleted, cancellationToken)
            ?? throw new EntityNotFoundException("REVIEW_NOT_FOUND", "ProductReview", reviewId);

        if (!isAdmin && review.BuyerId != currentUserId)
        {
            throw new BusinessRuleValidationException("FORBIDDEN", "You can only delete your own reviews.", 403);
        }

        var now = DateTimeOffset.UtcNow;
        review.IsDeleted = true;
        review.DeletedAt = now;

        if (review.Status == ReviewStatus.Published)
        {
            var stats = await _dbContext.ProductRatingStats
                .FirstOrDefaultAsync(s => s.ProductId == review.ProductId && !s.IsDeleted, cancellationToken);

            if (stats != null && stats.ReviewCount > 0)
            {
                stats.StarSum -= review.Rating;
                stats.ReviewCount -= 1;
                stats.AvgRating = stats.ReviewCount > 0
                    ? Math.Round((decimal)stats.StarSum / stats.ReviewCount, 2)
                    : null;
                stats.UpdatedAt = now;
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "[ProductReviewService] Soft deleted review '{ReviewId}' by user '{UserId}' (IsAdmin: {IsAdmin})",
            review.Id, currentUserId, isAdmin);
    }

    public async Task<ProductReviewDto> ModerateReviewAsync(
        Guid reviewId,
        ModerateReviewRequest request,
        Guid adminId,
        CancellationToken cancellationToken = default)
    {
        var review = await _dbContext.ProductReviews
            .FirstOrDefaultAsync(r => r.Id == reviewId && !r.IsDeleted, cancellationToken)
            ?? throw new EntityNotFoundException("REVIEW_NOT_FOUND", "ProductReview", reviewId);

        var action = request.Action?.Trim().ToUpperInvariant();
        var now = DateTimeOffset.UtcNow;

        var stats = await _dbContext.ProductRatingStats
            .FirstOrDefaultAsync(s => s.ProductId == review.ProductId && !s.IsDeleted, cancellationToken);

        var oldStatus = review.Status;

        if (action == "HIDE")
        {
            if (review.Status != ReviewStatus.Hidden)
            {
                review.Status = ReviewStatus.Hidden;
                review.UpdatedAt = now;

                if (stats != null && stats.ReviewCount > 0)
                {
                    stats.StarSum -= review.Rating;
                    stats.ReviewCount -= 1;
                    stats.AvgRating = stats.ReviewCount > 0
                        ? Math.Round((decimal)stats.StarSum / stats.ReviewCount, 2)
                        : null;
                    stats.UpdatedAt = now;
                }
            }
        }
        else if (action == "PUBLISH")
        {
            if (review.Status != ReviewStatus.Published)
            {
                review.Status = ReviewStatus.Published;
                review.UpdatedAt = now;

                if (stats == null)
                {
                    stats = new ProductRatingStats
                    {
                        Id = Guid.NewGuid(),
                        ProductId = review.ProductId,
                        StarSum = review.Rating,
                        ReviewCount = 1,
                        AvgRating = review.Rating,
                        CreatedAt = now,
                        UpdatedAt = now,
                        IsDeleted = false
                    };
                    _dbContext.ProductRatingStats.Add(stats);
                }
                else
                {
                    stats.StarSum += review.Rating;
                    stats.ReviewCount += 1;
                    stats.AvgRating = Math.Round((decimal)stats.StarSum / stats.ReviewCount, 2);
                    stats.UpdatedAt = now;
                }
            }
        }
        else
        {
            throw new BusinessRuleValidationException("VAL_INVALID_ACTION", "Action must be either 'HIDE' or 'PUBLISH'.", 400);
        }

        _dbContext.AddAuditLog(
            AuditAction.ReviewModerate,
            "ProductReview",
            review.Id.ToString(),
            oldValues: new { Status = oldStatus.ToString() },
            newValues: new { Status = review.Status.ToString(), Reason = request.Reason },
            userId: adminId.ToString());

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "[ProductReviewService] Moderated review '{ReviewId}' to status '{Status}' by admin '{AdminId}'",
            review.Id, review.Status, adminId);

        return new ProductReviewDto(
            review.Id,
            review.ProductId,
            review.BuyerId,
            review.Rating,
            review.Comment,
            review.Status.ToString(),
            review.CreatedAt,
            review.UpdatedAt);
    }

    public async Task<PurchaseRecordDto> SyncPurchaseRecordAsync(
        SyncPurchaseRecordRequest request,
        CancellationToken cancellationToken = default)
    {
        var productExists = await _dbContext.Products
            .AnyAsync(p => p.Id == request.ProductId && !p.IsDeleted, cancellationToken);

        if (!productExists)
        {
            throw new EntityNotFoundException("PRODUCT_NOT_FOUND", "Product", request.ProductId);
        }

        var existing = await _dbContext.PurchaseRecords
            .FirstOrDefaultAsync(p => p.BuyerId == request.BuyerId
                                   && p.OrderId == request.OrderId
                                   && p.ProductId == request.ProductId
                                   && !p.IsDeleted, cancellationToken);

        if (existing != null)
        {
            return new PurchaseRecordDto(
                existing.Id,
                existing.BuyerId,
                existing.OrderId,
                existing.ProductId,
                existing.CompletedAt,
                existing.CreatedAt);
        }

        var now = DateTimeOffset.UtcNow;
        var purchase = new PurchaseRecord
        {
            Id = Guid.NewGuid(),
            BuyerId = request.BuyerId,
            OrderId = request.OrderId,
            ProductId = request.ProductId,
            CompletedAt = request.CompletedAt ?? now,
            CreatedAt = now,
            UpdatedAt = now,
            IsDeleted = false
        };

        _dbContext.PurchaseRecords.Add(purchase);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "[ProductReviewService] Synced purchase record '{PurchaseId}' for buyer '{BuyerId}', order '{OrderId}', product '{ProductId}'",
            purchase.Id, purchase.BuyerId, purchase.OrderId, purchase.ProductId);

        return new PurchaseRecordDto(
            purchase.Id,
            purchase.BuyerId,
            purchase.OrderId,
            purchase.ProductId,
            purchase.CompletedAt,
            purchase.CreatedAt);
    }
}
