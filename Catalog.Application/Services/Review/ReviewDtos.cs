namespace Catalog.Application.Services.Review;

using System;
using System.Collections.Generic;

public sealed record ProductReviewDto(
    Guid Id,
    Guid ProductId,
    Guid BuyerId,
    short Rating,
    string? Comment,
    List<string>? ReviewImages,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt = null
);

/// <summary>
/// Whether the caller may review one product of one sub-order, and whether they already did.
/// Reasons: PURCHASE_NOT_FOUND, PRODUCT_BANNED, ALREADY_REVIEWED, REVIEW_WINDOW_EXPIRED.
/// </summary>
public sealed record ProductReviewEligibilityDto(
    bool CanReview,
    bool AlreadyReviewed,
    string? Reason,
    Guid? ReviewId,
    DateTimeOffset? ExpiresAt);

/// <summary>A review of the caller, with the sub-order it belongs to, so a product page can offer to edit or delete the right one.</summary>
public sealed record MyProductReviewDto(
    Guid ReviewId,
    Guid ProductId,
    Guid OrderId,
    short Rating,
    string? Comment,
    List<string>? ReviewImages,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record StarDistributionDto(
    int Star5,
    int Star4,
    int Star3,
    int Star2,
    int Star1
);

public sealed record ProductRatingSummaryDto(
    Guid ProductId,
    decimal? AvgRating,
    int ReviewCount,
    int StarSum,
    StarDistributionDto Distribution
);

public sealed record PurchaseRecordDto(
    Guid Id,
    Guid BuyerId,
    Guid OrderId,
    Guid ProductId,
    DateTimeOffset CompletedAt,
    DateTimeOffset CreatedAt
);

