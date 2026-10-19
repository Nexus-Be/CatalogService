using Catalog.Domain.Base;

namespace Catalog.Domain.Entities;

public class ProductImage : BaseEntity<Guid>
{
    public required Guid ProductId { get; set; }
    public Guid? VariantId { get; set; }
    public required string Url { get; set; }
    public string? AltText { get; set; }
    public bool IsActive { get; set; }
    public short SortOrder { get; set; }

    // Navigation
    public Product? Product { get; set; }
    public ProductVariant? Variant { get; set; }
}
