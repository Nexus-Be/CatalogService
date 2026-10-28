namespace Catalog.Application.Services.Product;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

public interface IProductService
{
    Task<ProductDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProductDto>> ListAsync(int page = 1, int pageSize = 20, CancellationToken cancellationToken = default);
    Task<PagedList<ProductListItemDto>> SearchAsync(ProductListQuery query, CancellationToken cancellationToken = default);
    Task<ProductDetailDto?> GetDetailAsync(Guid id, CancellationToken cancellationToken = default);
    Task<BatchVariantResponse> GetVariantsBatchAsync(BatchGetVariantsRequest request, CancellationToken cancellationToken = default);
    Task<CheckVariantStatusResponse> CheckVariantStatusBatchAsync(CheckVariantStatusRequest request, CancellationToken cancellationToken = default);
    Task<ProductOperationResult<ProductDto>> CreateAsync(CreateProductCommand command, CancellationToken cancellationToken = default);
    Task<ProductOperationResult<ProductDto>> UpdateAsync(UpdateProductCommand command, CancellationToken cancellationToken = default);
    Task<ProductOperationResult<ProductDto>> ChangeProductStatusAsync(
        Guid productId,
        Guid currentSellerId,
        ChangeProductStatusRequest request,
        CancellationToken cancellationToken = default);
    Task<ProductOperationResult<bool>> SoftDeleteAsync(Guid id, Guid currentSellerId, CancellationToken cancellationToken = default);
    Task<ProductOperationResult<ProductImageDto>> AddProductImageAsync(
        Guid productId,
        Guid currentSellerId,
        Stream fileStream,
        string fileName,
        Guid? variantId = null,
        string? altText = null,
        short sortOrder = 0,
        CancellationToken cancellationToken = default);
    Task<ProductOperationResult<bool>> DeleteProductImageAsync(
        Guid productId,
        Guid imageId,
        Guid currentSellerId,
        CancellationToken cancellationToken = default);

    // Variant Management (CAT-07, CAT-08, CAT-09, CAT-10, CAT-11)
    Task<VariantOperationResult<VariantDetailDto>> AddVariantAsync(
        Guid productId,
        Guid currentSellerId,
        CreateVariantRequest request,
        CancellationToken cancellationToken = default);

    Task<VariantOperationResult<VariantDetailDto>> UpdateVariantAsync(
        Guid variantId,
        Guid currentSellerId,
        UpdateVariantRequest request,
        CancellationToken cancellationToken = default);

    Task<VariantOperationResult<VariantDetailDto>> ChangeVariantStatusAsync(
        Guid variantId,
        Guid currentSellerId,
        ChangeVariantStatusRequest request,
        CancellationToken cancellationToken = default);

    Task<VariantOperationResult<Guid>> SetDefaultVariantAsync(
        Guid productId,
        Guid currentSellerId,
        Guid variantId,
        CancellationToken cancellationToken = default);

    Task<VariantOperationResult<bool>> SoftDeleteVariantAsync(
        Guid variantId,
        Guid currentSellerId,
        CancellationToken cancellationToken = default);

    // Seller Specific Functions (CAT-44, CAT-45)
    Task<PagedList<SellerProductListItemDto>> SearchSellerProductsAsync(
        Guid sellerId,
        SellerProductSearchQuery query,
        CancellationToken cancellationToken = default);

    Task<ProductOperationResult<SellerProductDetailDto>> GetSellerProductDetailAsync(
        Guid productId,
        Guid sellerId,
        CancellationToken cancellationToken = default);
}

