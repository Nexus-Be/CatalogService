namespace Catalog.Application.Services.Product;

using System;
using System.Collections.Generic;

public sealed record ProductDto(
    Guid Id,
    Guid SellerId,
    Guid CategoryId,
    string Name,
    string? Description,
    string? Brand,
    string Status,
    int? WeightGrams = null)
{
    public ProductDto(Guid id, Guid sellerId, Guid categoryId, string name, string? description, string? brand, string status)
        : this(id, sellerId, categoryId, name, description, brand, status, null) { }
}

public sealed record ProductListItemDto(
    Guid ProductId,
    Guid SellerId,
    Guid CategoryId,
    string Name,
    string? Brand,
    string SaleType,
    string Status,
    decimal? MinPrice,
    decimal? MaxPrice,
    string? PrimaryImageUrl,
    decimal? AvgRating);

public sealed record ProductVariantAttributeDto(string AttrName, string AttrValue);

public sealed record ProductDetailVariantDto(
    Guid VariantId,
    string SkuCode,
    string Name,
    decimal? Price,
    string Status,
    bool IsActive,
    IReadOnlyList<ProductVariantAttributeDto> Attributes,
    string? PrimaryImageUrl = null,
    IReadOnlyList<ProductImageDto>? Images = null,
    int? WeightGrams = null)
{
    public ProductDetailVariantDto(
        Guid variantId,
        string skuCode,
        string name,
        decimal? price,
        string status,
        bool isActive,
        IReadOnlyList<ProductVariantAttributeDto> attributes,
        string? primaryImageUrl,
        IReadOnlyList<ProductImageDto>? images)
        : this(variantId, skuCode, name, price, status, isActive, attributes, primaryImageUrl, images, null) { }
}

public sealed record ProductDetailImageDto(
    Guid ImageId,
    string Url,
    string? AltText,
    bool IsActive,
    int SortOrder,
    Guid? VariantId);

public sealed record ProductDetailCategoryDto(
    Guid CategoryId,
    Guid? ParentId,
    string Name,
    int Level);

public sealed record ProductDetailRatingDto(
    decimal? AvgRating,
    int ReviewCount);

public sealed record ProductDetailDto(
    Guid ProductId,
    Guid SellerId,
    string Name,
    string? Description,
    string? Brand,
    string SaleType,
    string Status,
    IReadOnlyList<ProductSpecificationDto>? Specifications,
    DateTimeOffset? PublishedAt,
    ProductDetailCategoryDto? Category,
    IReadOnlyList<ProductDetailVariantDto> Variants,
    IReadOnlyList<ProductDetailImageDto> Images,
    ProductDetailRatingDto Rating,
    int? WeightGrams = null)
{
    public ProductDetailDto(
        Guid productId,
        Guid sellerId,
        string name,
        string? description,
        string? brand,
        string saleType,
        string status,
        IReadOnlyList<ProductSpecificationDto>? specifications,
        DateTimeOffset? publishedAt,
        ProductDetailCategoryDto? category,
        IReadOnlyList<ProductDetailVariantDto> variants,
        IReadOnlyList<ProductDetailImageDto> images,
        ProductDetailRatingDto rating)
        : this(productId, sellerId, name, description, brand, saleType, status, specifications, publishedAt, category, variants, images, rating, null) { }
}

public sealed record ProductImageDto(
    Guid Id,
    Guid ProductId,
    Guid? VariantId,
    string Url,
    string? AltText,
    bool IsActive,
    short SortOrder);

public sealed record VariantDetailDto(
    Guid Id,
    Guid ProductId,
    Guid SellerId,
    string ProductName,
    string SkuCode,
    string Name,
    decimal? Price,
    string Currency,
    bool IsActive,
    bool HasOrder,
    string Status,
    string? StatusReason,
    bool IsPurchasable,
    IReadOnlyList<ProductVariantAttributeDto> Attributes,
    string? PrimaryImageUrl = null,
    IReadOnlyList<ProductImageDto>? Images = null,
    int? WeightGrams = null);

public sealed record SellerProductListItemDto(
    Guid ProductId,
    string Name,
    string? Brand,
    Guid CategoryId,
    string? CategoryName,
    string SaleType,
    string Status,
    string? StatusReason,
    decimal? MinPrice,
    decimal? MaxPrice,
    string? PrimaryImageUrl,
    int VariantCount,
    bool HasOrder,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PublishedAt);

public sealed record SellerProductVariantDto(
    Guid VariantId,
    string SkuCode,
    string Name,
    decimal? Price,
    string Currency,
    string Status,
    string? StatusReason,
    bool IsActive,
    bool HasOrder,
    IReadOnlyList<ProductVariantAttributeDto> Attributes,
    IReadOnlyList<ProductImageDto> Images,
    int? WeightGrams = null);

public sealed record SellerProductDetailDto(
    Guid ProductId,
    Guid SellerId,
    Guid CategoryId,
    string? CategoryName,
    string Name,
    string? Description,
    string? Brand,
    IReadOnlyList<ProductSpecificationDto>? Specifications,
    string SaleType,
    string Status,
    string? StatusReason,
    bool HasOrder,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    DateTimeOffset? PublishedAt,
    IReadOnlyList<ProductImageDto> Images,
    IReadOnlyList<SellerProductVariantDto> Variants,
    int? WeightGrams = null,
    string? RejectionReason = null);

public sealed record BatchVariantItemDto(
    Guid VariantId,
    Guid ProductId,
    string SkuCode,
    string VariantName,
    string ProductName,
    decimal? Price,
    string Currency,
    string Status,
    string ProductStatus,
    Guid SellerId,
    string SaleType,
    string? PrimaryImageUrl,
    IReadOnlyList<ProductVariantAttributeDto> Attributes,
    bool IsPurchasable,
    int? WeightGrams = null);

public sealed record BatchVariantResponse(
    IReadOnlyList<BatchVariantItemDto> Items,
    IReadOnlyList<Guid> NotFoundIds);

public sealed record VariantStatusItemDto(
    Guid VariantId,
    Guid ProductId,
    string ProductStatus,
    string? ProductStatusReason,
    string VariantStatus,
    string? VariantStatusReason,
    string SaleType,
    bool IsPurchasable,
    string Reason,
    decimal? Price,
    string Currency);

public sealed record CheckVariantStatusResponse(
    IReadOnlyList<VariantStatusItemDto> Items,
    IReadOnlyList<Guid> NotFoundIds);

public sealed record ValidateVariantSwitchResponse(
    bool IsValid,
    Guid ProductId,
    Guid SellerId,
    Guid NewSkuId,
    string NewSkuName,
    decimal NewUnitPrice,
    bool IsPurchasable,
    int? WeightGrams = null);

public sealed record AdminProductListItemDto(
    Guid ProductId,
    Guid SellerId,
    Guid CategoryId,
    string CategoryName,
    string Name,
    string? Brand,
    string SaleType,
    string Status,
    string? StatusReason,
    decimal? MinPrice,
    decimal? MaxPrice,
    string? PrimaryImageUrl,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PublishedAt);
