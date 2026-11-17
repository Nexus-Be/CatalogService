namespace Catalog.Domain.Enums;

/// <summary>
/// Whether the SKU has its inventory row in FulfillmentService. Only Regular products need one; an auction SKU stays NotRequired.
/// </summary>
public enum InventoryInitState
{
    NotRequired = 0,
    Pending = 1,
    Done = 2,
    NeedsAttention = 3
}
