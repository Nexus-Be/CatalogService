namespace Catalog.Application.Interfaces;

using System;
using System.Threading;
using System.Threading.Tasks;

public record InitializeInventoryClientRequest(
    Guid SkuId,
    Guid? WarehouseId = null,
    int InitialQuantity = 0);

public record InventoryClientResponse(
    Guid Id,
    Guid SkuId,
    Guid WarehouseId,
    int TotalQuantity,
    int AvailableQuantity);

public interface IFulfillmentClient
{
    Task<InventoryClientResponse?> InitializeInventoryAsync(
        InitializeInventoryClientRequest request,
        CancellationToken cancellationToken = default);
}
