namespace Catalog.Application.Services.Review;

using System;
using System.Threading;
using System.Threading.Tasks;
using Catalog.Application.Common.Models;

public interface IProductReviewService
{
    Task<ProductRatingSummaryDto> GetRatingSummaryAsync(
        Guid productId,
        CancellationToken cancellationToken = default);

    Task<PagedData<ProductReviewDto>> GetProductReviewsAsync(
        Guid productId,
        ReviewListQuery query,
        CancellationToken cancellationToken = default);

    Task<ProductReviewDto> CreateReviewAsync(
        Guid productId,
        CreateProductReviewRequest request,
        Guid currentUserId,
        IReadOnlyList<FileUploadModel>? reviewImages = null,
        CancellationToken cancellationToken = default);

    Task<ProductReviewDto> UpdateReviewAsync(
        Guid reviewId,
        UpdateProductReviewRequest request,
        Guid currentUserId,
        IReadOnlyList<FileUploadModel>? reviewImages = null,
        CancellationToken cancellationToken = default);

    Task DeleteReviewAsync(
        Guid reviewId,
        Guid currentUserId,
        bool isAdmin,
        CancellationToken cancellationToken = default);

    Task<ProductReviewDto> ModerateReviewAsync(
        Guid reviewId,
        ModerateReviewRequest request,
        Guid adminId,
        CancellationToken cancellationToken = default);

    Task<PurchaseRecordDto> SyncPurchaseRecordAsync(
        SyncPurchaseRecordRequest request,
        CancellationToken cancellationToken = default);
}

