namespace Catalog.Application.Services.Product;

using System;
using System.Collections.Generic;
using Catalog.Domain.Enums;

public sealed record CreateProductRequest(
    Guid SellerId,
    Guid CategoryId,
    string Name,
    string? Description = null,
    string? Brand = null,
    SaleType SaleType = SaleType.Regular,
    List<ProductSpecificationDto>? Specifications = null,
    List<CreateVariantRequest>? Variants = null,
    int? WeightGrams = null);

public sealed record CreateAuctionProductRequest(
    Guid CategoryId,
    string Name,
    string? Description = null,
    string? Brand = null,
    string? SkuCode = null,
    List<ProductSpecificationDto>? Specifications = null,
    Guid? SellerId = null,
    int? WeightGrams = null);

public sealed record UpdateProductRequest(
    string Name,
    string? Description = null,
    string? Brand = null,
    Guid? CategoryId = null,
    SaleType? SaleType = null,
    string? Status = null,
    List<ProductSpecificationDto>? Specifications = null,
    int? WeightGrams = null);

public sealed record ChangeProductStatusRequest(
    string Status,
    string? StatusReason = null);

public sealed record ProductListQuery(
    int Page = 1,
    int PageSize = 20,
    string? Keyword = null,
    Guid? CategoryId = null,
    Guid? SellerId = null,
    SaleType? SaleType = null,
    string? SortBy = null,
    string? SortDirection = "asc",
    decimal? MinPrice = null,
    decimal? MaxPrice = null,
    string? Brand = null,
    decimal? MinRating = null);

public sealed record CreateVariantAttributeRequest(string AttrName, string AttrValue);

public sealed record CreateVariantRequest(
    string SkuCode,
    string Name,
    decimal? Price = null,
    string? Currency = "VND",
    List<CreateVariantAttributeRequest>? Attributes = null,
    int? WeightGrams = null);

public sealed record UpdateVariantRequest(
    string Name,
    decimal Price,
    string? Currency = "VND",
    List<CreateVariantAttributeRequest>? Attributes = null,
    int? WeightGrams = null);

public sealed record ChangeVariantStatusRequest(
    string Status,
    string? StatusReason = null);

public sealed record SetDefaultVariantRequest(
    Guid VariantId);

public sealed record UpdateProductImageRequest(
    bool? IsActive = null,
    short? SortOrder = null,
    string? AltText = null);

public sealed record SellerProductSearchQuery(
    int Page = 1,
    int PageSize = 20,
    string? Keyword = null,
    ProductStatus? Status = null,
    Guid? CategoryId = null,
    SaleType? SaleType = null,
    string? SortBy = null,
    string? SortDirection = "asc");

public sealed record BatchGetVariantsRequest(List<Guid> VariantIds);

public sealed record CheckVariantStatusRequest(
    List<Guid> VariantIds,
    string Purpose);

public sealed record ValidateVariantSwitchRequest(
    Guid CurrentSkuId,
    Guid NewSkuId);

public sealed record ReserveStockRequest(
    Guid ProductId,
    int Quantity,
    Guid OrderId);

public sealed record ModerateProductRequest(
    string Action,
    string? Reason = null,
    string? Note = null);

public sealed record AdminProductSearchQuery(
    int Page = 1,
    int PageSize = 20,
    string? Keyword = null,
    ProductStatus? Status = null,
    ProductInactiveReason? StatusReason = null,
    Guid? CategoryId = null,
    Guid? SellerId = null,
    SaleType? SaleType = null,
    string? SortBy = null,
    string? SortDirection = "desc");
