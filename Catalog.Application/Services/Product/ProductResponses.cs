namespace Catalog.Application.Services.Product;

using System;

public sealed record ProductResponse(
    Guid Id,
    Guid SellerId,
    Guid CategoryId,
    string Name,
    string? Description,
    string? Brand,
    string Status);

public sealed record ReserveStockResponse(
    bool Success,
    Guid ProductId,
    int ReservedQuantity,
    string? Reason = null);
