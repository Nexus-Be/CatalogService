namespace Catalog.Application.Services.Review;

using System;

public sealed record ProductReviewDto(
    Guid Id,
    Guid ProductId,
    Guid BuyerId,
    short Rating,
    string? Comment,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt = null
);

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

