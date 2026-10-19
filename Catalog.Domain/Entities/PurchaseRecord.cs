using Catalog.Domain.Base;

namespace Catalog.Domain.Entities;

public class PurchaseRecord : BaseEntity<Guid>
{
    public required Guid BuyerId { get; set; }
    public required Guid OrderId { get; set; }
    public required Guid ProductId { get; set; }
    public required DateTimeOffset CompletedAt { get; set; }

    // Navigation
    public Product? Product { get; set; }
    public ICollection<ProductReview> ProductReviews { get; set; } = new List<ProductReview>();
}
