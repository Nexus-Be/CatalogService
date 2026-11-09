namespace Catalog.Application.Services.Product;

using System;

public sealed record AuctionEligibilityResponse(
    Guid ProductId,
    bool IsEligible,
    string? Reason,
    string Status,
    string? SkuCode,
    int? WeightGrams
);

