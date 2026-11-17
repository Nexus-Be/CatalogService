using Catalog.Domain.Base;
using Catalog.Domain.Enums;

namespace Catalog.Domain.Entities;

public class ProductVariant : AuditableEntity<Guid>
{
    public required Guid ProductId { get; set; }
    public required string SkuCode { get; set; }
    public required string Name { get; set; }
    public decimal? Price { get; set; }
    public string Currency { get; set; } = "VND";
    public bool IsActive { get; set; }
    public bool HasOrder { get; set; }
    public required VariantStatus Status { get; set; } = VariantStatus.Active;
    public VariantInactiveReason? StatusReason { get; set; }
    public int? WeightGrams { get; set; }

    /// <summary>Inventory row in FulfillmentService. A failed call is retried in the background until it is Done or needs attention.</summary>
    public InventoryInitState InventoryInitState { get; set; } = InventoryInitState.NotRequired;
    public int InventoryInitAttemptCount { get; set; }
    public DateTimeOffset? InventoryInitNextRetryAt { get; set; }
    public string? InventoryInitLastError { get; set; }

    // Navigation
    public Product? Product { get; set; }
    public ICollection<ProductVariantAttribute> Attributes { get; set; } = new List<ProductVariantAttribute>();
    public ICollection<ProductImage> Images { get; set; } = new List<ProductImage>();
}
