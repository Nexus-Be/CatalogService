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
    string? Specifications = null,
    List<CreateVariantRequest>? Variants = null);

public sealed record CreateAuctionProductRequest(
    Guid CategoryId,
    string Name,
    string? Description = null,
    string? Brand = null,
    string? SkuCode = null,
    string? Specifications = null,
    Guid? SellerId = null);

public sealed record UpdateProductRequest(
    string Name,
    string? Description = null,
    string? Brand = null,
    Guid? CategoryId = null,
    SaleType? SaleType = null,
    string? Status = null,
    string? Specifications = null);

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
    decimal? MinPrice = null,
    decimal? MaxPrice = null,
    string? Brand = null,
    decimal? MinRating = null);

public sealed record CreateVariantAttributeRequest(string AttrName, string AttrValue);

public sealed record CreateVariantRequest(
    string SkuCode,
    string Name,
    decimal Price,
    string? Currency = "VND",
    List<CreateVariantAttributeRequest>? Attributes = null);

public sealed record UpdateVariantRequest(
    string Name,
    decimal Price,
    string? Currency = "VND",
    List<CreateVariantAttributeRequest>? Attributes = null);

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
    string? SortBy = null);

public sealed record BatchGetVariantsRequest(List<Guid> VariantIds);

public sealed record CheckVariantStatusRequest(
    List<Guid> VariantIds,
    string Purpose);

public sealed record ReserveStockRequest(
    Guid ProductId,
    int Quantity,
    Guid OrderId);
