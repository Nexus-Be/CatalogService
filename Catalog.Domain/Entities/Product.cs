using Catalog.Domain.Base;
using Catalog.Domain.Enums;

namespace Catalog.Domain.Entities;

public class Product : AuditableEntity<Guid>
{
    public required Guid SellerId { get; set; }
    public required Guid CategoryId { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public string? Brand { get; set; }
    public required SaleType SaleType { get; set; } = SaleType.Regular;
    public required ProductStatus Status { get; set; } = ProductStatus.Draft;
    public ProductInactiveReason? StatusReason { get; set; }
    public string? Specifications { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }

    // Navigation
    public Category? Category { get; set; }
    public ICollection<ProductVariant> Variants { get; set; } = new List<ProductVariant>();
    public ICollection<ProductImage> Images { get; set; } = new List<ProductImage>();
    public ProductRatingStats? RatingStats { get; set; }
    public ICollection<PurchaseRecord> PurchaseRecords { get; set; } = new List<PurchaseRecord>();
    public ICollection<ProductReview> Reviews { get; set; } = new List<ProductReview>();
}
