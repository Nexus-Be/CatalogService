namespace Catalog.Application.Services.Product;

using System;

public sealed record ProductDto(
    Guid Id,
    Guid SellerId,
    Guid CategoryId,
    string Name,
    string? Description,
    string? Brand,
    string Status);
