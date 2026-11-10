namespace Catalog.Application.Services.Review;

using System;
using System.ComponentModel.DataAnnotations;

public sealed record CreateProductReviewRequest(
    [Required(ErrorMessage = "Order ID is required.")]
    Guid OrderId,

    [Range(1, 5, ErrorMessage = "Rating must be between 1 and 5 stars.")]
    short Rating,

    [MaxLength(2000, ErrorMessage = "Comment cannot exceed 2000 characters.")]
    string? Comment = null
);

public sealed record UpdateProductReviewRequest(
    [Range(1, 5, ErrorMessage = "Rating must be between 1 and 5 stars.")]
    short Rating,

    [MaxLength(2000, ErrorMessage = "Comment cannot exceed 2000 characters.")]
    string? Comment = null
);

public sealed record ModerateReviewRequest(
    [Required(ErrorMessage = "Action is required.")]
    string Action,

    [MaxLength(500, ErrorMessage = "Moderation reason cannot exceed 500 characters.")]
    string? Reason = null
);

public sealed record SyncPurchaseRecordRequest(
    [Required(ErrorMessage = "Buyer ID is required.")]
    Guid BuyerId,

    [Required(ErrorMessage = "Order ID is required.")]
    Guid OrderId,

    [Required(ErrorMessage = "Product ID is required.")]
    Guid ProductId,

    DateTimeOffset? CompletedAt = null
);

public sealed record ReviewListQuery(
    int Page = 1,
    int PageSize = 10,
    short? Rating = null,
    string? SortBy = "newest"
);

