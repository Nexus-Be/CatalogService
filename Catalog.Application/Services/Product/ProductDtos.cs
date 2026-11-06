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
    string Status);

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
    IReadOnlyList<ProductImageDto>? Images = null);

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
    ProductDetailRatingDto Rating);

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
    IReadOnlyList<ProductImageDto>? Images = null);

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
    IReadOnlyList<ProductImageDto> Images);

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
    IReadOnlyList<SellerProductVariantDto> Variants);

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
    bool IsPurchasable);

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
    int WeightGrams = 200);
