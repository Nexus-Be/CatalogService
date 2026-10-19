using Catalog.Domain.Base;

namespace Catalog.Domain.Entities;

public class ProductVariantAttribute : BaseEntity<Guid>
{
    public required Guid VariantId { get; set; }
    public required string AttrName { get; set; }
    public required string AttrValue { get; set; }

    // Navigation
    public ProductVariant? Variant { get; set; }
}
