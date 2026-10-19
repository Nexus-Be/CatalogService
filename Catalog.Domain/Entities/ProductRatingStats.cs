using Catalog.Domain.Base;

namespace Catalog.Domain.Entities;

public class ProductRatingStats : BaseEntity<Guid>
{
    public required Guid ProductId { get; set; }
    public int StarSum { get; set; }
    public int ReviewCount { get; set; }
    public decimal? AvgRating { get; set; }

    // Navigation
    public Product? Product { get; set; }
}
