namespace Catalog.Application.Services.Product;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Catalog.Application.Common.Models;

public interface IProductService
{
    // Product Queries & CRUD
    Task<ProductDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedData<ProductDto>> ListAsync(int page = 1, int pageSize = 20, CancellationToken cancellationToken = default);
    Task<PagedData<ProductListItemDto>> SearchAsync(ProductListQuery query, CancellationToken cancellationToken = default);
    Task<ProductDetailDto> GetDetailAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ProductDto> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken = default);
    Task<ProductDto> CreateAuctionProductAsync(CreateAuctionProductRequest request, Guid currentUserId, CancellationToken cancellationToken = default);
    Task<ProductDto> UpdateAsync(Guid id, UpdateProductRequest request, Guid currentUserId, CancellationToken cancellationToken = default);
    Task<ProductDto> ChangeStatusAsync(Guid id, ChangeProductStatusRequest request, Guid currentUserId, CancellationToken cancellationToken = default);
    Task<bool> SoftDeleteAsync(Guid id, Guid currentUserId, CancellationToken cancellationToken = default);

    // Image Management (CAT-12, CAT-13, CAT-14, CAT-40)
    Task<ProductImageDto> AddProductImageAsync(
        Guid productId,
        Guid currentUserId,
        Stream fileStream,
        string fileName,
        Guid? variantId = null,
        string? altText = null,
        short sortOrder = 0,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteProductImageAsync(
        Guid productId,
        Guid imageId,
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteImageByIdAsync(
        Guid imageId,
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<ProductImageDto> UpdateImageAsync(
        Guid imageId,
        UpdateProductImageRequest request,
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProductImageDto>> GetProductImagesAsync(
        Guid productId,
        CancellationToken cancellationToken = default);

    // Variant Management (CAT-07 to CAT-11, CAT-42)
    Task<VariantDetailDto> GetVariantByIdAsync(
        Guid variantId,
        CancellationToken cancellationToken = default);

    Task<VariantDetailDto> AddVariantAsync(
        Guid productId,
        Guid currentUserId,
        CreateVariantRequest request,
        CancellationToken cancellationToken = default);

    Task<VariantDetailDto> UpdateVariantAsync(
        Guid variantId,
        Guid currentUserId,
        UpdateVariantRequest request,
        CancellationToken cancellationToken = default);

    Task<VariantDetailDto> ChangeVariantStatusAsync(
        Guid variantId,
        Guid currentUserId,
        ChangeVariantStatusRequest request,
        CancellationToken cancellationToken = default);

    Task<Guid> SetDefaultVariantAsync(
        Guid productId,
        Guid currentUserId,
        Guid variantId,
        CancellationToken cancellationToken = default);

    Task<bool> SoftDeleteVariantAsync(
        Guid variantId,
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    // Seller APIs (CAT-44, CAT-45)
    Task<PagedData<SellerProductListItemDto>> SearchSellerProductsAsync(
        Guid sellerId,
        SellerProductSearchQuery query,
        CancellationToken cancellationToken = default);

    Task<SellerProductDetailDto> GetSellerProductDetailAsync(
        Guid productId,
        Guid sellerId,
        CancellationToken cancellationToken = default);

    // Internal Batch APIs (CAT-24, CAT-39)
    Task<BatchVariantResponse> GetVariantsBatchAsync(
        BatchGetVariantsRequest request,
        CancellationToken cancellationToken = default);

    Task<CheckVariantStatusResponse> CheckVariantStatusBatchAsync(
        CheckVariantStatusRequest request,
        CancellationToken cancellationToken = default);
}
