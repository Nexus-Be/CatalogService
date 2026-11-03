namespace Catalog.Application.Interfaces;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public record WarehouseClientDto(
    Guid Id,
    string Code,
    string Name,
    string? Address,
    bool IsActive,
    Guid? SellerId);

public record DeclareInventoryClientRequest(
    Guid SkuId,
    Guid WarehouseId,
    int Quantity = 0,
    string? Note = null);

public record InitializeInventoryClientRequest(
    Guid SkuId,
    Guid? WarehouseId = null,
    int InitialQuantity = 0);

public record InventoryClientResponse(
    Guid Id,
    Guid SkuId,
    Guid WarehouseId,
    int TotalQuantity,
    int ReservedQuantity,
    int AvailableQuantity);

public interface IFulfillmentClient
{
    /// <summary>
    /// Gets list of warehouses owned by a seller via GET /api/v1/warehouses/seller/{sellerId} (WH-03).
    /// </summary>
    Task<IReadOnlyList<WarehouseClientDto>> GetWarehousesBySellerIdAsync(
        Guid sellerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all active warehouses in the system via GET /api/v1/warehouses (WH-01).
    /// </summary>
    Task<IReadOnlyList<WarehouseClientDto>> GetActiveWarehousesAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Declares initial inventory for a SKU at a warehouse via POST /api/v1/internal/inventories/declare (FUL-01).
    /// </summary>
    Task<InventoryClientResponse?> DeclareInventoryAsync(
        DeclareInventoryClientRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves the seller's active warehouse (or fallback platform warehouse) and declares initial inventory for the SKU.
    /// </summary>
    Task<InventoryClientResponse?> InitializeInventoryForSkuAsync(
        Guid skuId,
        Guid sellerId,
        int initialQuantity = 0,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Legacy fallback method.
    /// </summary>
    Task<InventoryClientResponse?> InitializeInventoryAsync(
        InitializeInventoryClientRequest request,
        CancellationToken cancellationToken = default);
}
