namespace Catalog.Application.Services.Product;

using System;
using System.Collections.Generic;

public sealed record AuctionProductListItemDto(
    Guid ProductId,
    Guid SellerId,
    Guid CategoryId,
    string Name,
    string? Brand,
    string SkuCode,
    string Status,
    string? PrimaryImageUrl,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PublishedAt);

public sealed record AuctionProductDetailDto(
    Guid ProductId,
    Guid SellerId,
    Guid CategoryId,
    string CategoryName,
    string Name,
    string? Description,
    string? Brand,
    string Status,
    string SkuCode,
    Guid VariantId,
    IReadOnlyList<ProductSpecificationDto>? Specifications,
    IReadOnlyList<ProductImageDto> Images,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PublishedAt,
    int? WeightGrams = null);

public sealed record AuctionListQuery(
    int Page = 1,
    int PageSize = 20,
    string? Keyword = null,
    Guid? CategoryId = null,
    Guid? SellerId = null,
    string? Status = null,
    string? SortBy = null);
