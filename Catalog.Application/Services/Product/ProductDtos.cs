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

public sealed record ProductListQuery(
    int Page,
    int PageSize,
    string? Keyword,
    Guid? CategoryId,
    Guid? SellerId,
    Catalog.Domain.Enums.SaleType? SaleType,
    string? SortBy,
    // search by price range, brand, rating
    decimal? MinPrice = null,
    decimal? MaxPrice = null,
    string? Brand = null,
    decimal? MinRating = null);

public sealed record ProductListItemDto(
    Guid ProductId,
    string Name,
    string? Brand,
    string SaleType,
    string Status,
    decimal? MinPrice,
    decimal? MaxPrice,
    string? PrimaryImageUrl,
    decimal? AvgRating);

public sealed record PagedList<T>(IReadOnlyList<T> Items, int TotalCount);

public sealed record ProductVariantAttributeDto(string AttrName, string AttrValue);

public sealed record ProductDetailVariantDto(
    Guid VariantId,
    string SkuCode,
    string Name,
    decimal? Price,
    string Status,
    bool IsActive,
    IReadOnlyList<ProductVariantAttributeDto> Attributes);

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
    string? Specifications,
    DateTimeOffset? PublishedAt,
    ProductDetailCategoryDto? Category,
    IReadOnlyList<ProductDetailVariantDto> Variants,
    IReadOnlyList<ProductDetailImageDto> Images,
    ProductDetailRatingDto Rating);

// CAT-24: Batch Get Variants
public sealed record BatchGetVariantsRequest(List<Guid> VariantIds);

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
    IReadOnlyList<ProductVariantAttributeDto> Attributes);

public sealed record BatchVariantResponse(
    IReadOnlyList<BatchVariantItemDto> Items,
    IReadOnlyList<Guid> NotFoundIds);

// CAT-39: Check Variant & Product Status
public sealed record CheckVariantStatusRequest(
    List<Guid> VariantIds,
    string Purpose); // "CART", "CHECKOUT", or "AUCTION"

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

// Product Management (Create, Update, Soft Delete)
public enum ProductOperationStatus
{
    Success,
    NotFound,
    Forbidden,
    InvalidCategory
}

public sealed record ProductOperationResult<T>(
    ProductOperationStatus Status,
    T? Data = default,
    string? ErrorMessage = null);

public sealed record CreateProductCommand(
    Guid SellerId,
    Guid CategoryId,
    string Name,
    string? Description,
    string? Brand,
    Catalog.Domain.Enums.SaleType SaleType = Catalog.Domain.Enums.SaleType.Regular,
    string? Specifications = null);

public sealed record UpdateProductCommand(
    Guid ProductId,
    Guid CurrentSellerId,
    string Name,
    string? Description,
    string? Brand,
    Guid? CategoryId = null,
    Catalog.Domain.Enums.SaleType? SaleType = null,
    string? Status = null,
    string? Specifications = null);

public sealed record ProductImageDto(
    Guid Id,
    Guid ProductId,
    Guid? VariantId,
    string Url,
    string? AltText,
    bool IsActive,
    short SortOrder);

// Variant Management (CAT-07, CAT-08, CAT-09, CAT-10, CAT-11)
public enum VariantOperationStatus
{
    Success,
    NotFound,
    Forbidden,
    DuplicateSku,
    InvalidOperation,
    HasOrder,
    LastVariant
}

public sealed record VariantOperationResult<T>(
    VariantOperationStatus Status,
    T? Data = default,
    string? ErrorMessage = null);

public sealed record CreateVariantAttributeRequest(string AttrName, string AttrValue);

public sealed record CreateVariantRequest(
    string SkuCode,
    string Name,
    decimal Price,
    string? Currency = "VND",
    List<CreateVariantAttributeRequest>? Attributes = null,
    Guid? SellerId = null);

public sealed record UpdateVariantRequest(
    string Name,
    decimal Price,
    string? Currency = "VND",
    List<CreateVariantAttributeRequest>? Attributes = null,
    Guid? SellerId = null);

public sealed record ChangeVariantStatusRequest(
    string Status,
    string? StatusReason = null,
    Guid? SellerId = null);

public sealed record SetDefaultVariantRequest(
    Guid VariantId);

public sealed record VariantDetailDto(
    Guid Id,
    Guid ProductId,
    string SkuCode,
    string Name,
    decimal? Price,
    string Currency,
    bool IsActive,
    bool HasOrder,
    string Status,
    string? StatusReason,
    IReadOnlyList<ProductVariantAttributeDto> Attributes);




