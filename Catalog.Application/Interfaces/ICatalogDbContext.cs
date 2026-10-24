namespace Catalog.Application.Interfaces;

using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Catalog.Domain.Entities;

public interface ICatalogDbContext
{
    DbSet<Category> Categories { get; }
    DbSet<Product> Products { get; }
    DbSet<ProductVariant> ProductVariants { get; }
    DbSet<ProductVariantAttribute> ProductVariantAttributes { get; }
    DbSet<ProductImage> ProductImages { get; }
    DbSet<ProductRatingStats> ProductRatingStats { get; }
    DbSet<PurchaseRecord> PurchaseRecords { get; }
    DbSet<ProductReview> ProductReviews { get; }
    DbSet<AuditLog> AuditLogs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
