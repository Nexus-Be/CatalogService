using Catalog.Domain.Base;
using Catalog.Domain.Enums;

namespace Catalog.Domain.Entities;

public class ProductReview : BaseEntity<Guid>
{
    public required Guid PurchaseRecordId { get; set; }
    public required Guid ProductId { get; set; }
    public required Guid BuyerId { get; set; }
    public required short Rating { get; set; }
    public string? Comment { get; set; }
    public required ReviewStatus Status { get; set; } = ReviewStatus.Published;

    // Navigation
    public PurchaseRecord? PurchaseRecord { get; set; }
    public Product? Product { get; set; }
}
