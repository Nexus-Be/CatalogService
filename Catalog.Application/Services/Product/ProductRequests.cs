namespace Catalog.Application.Services.Product;

using System;

public sealed record CreateProductRequest(
    Guid SellerId,
    Guid CategoryId,
    string Name,
    string? Description,
    string? Brand);

public sealed record UpdateProductRequest(
    string Name,
    string? Description,
    string? Brand,
    string Status);

public sealed record ReserveStockRequest(
    Guid ProductId,
    int Quantity,
    Guid OrderId);
