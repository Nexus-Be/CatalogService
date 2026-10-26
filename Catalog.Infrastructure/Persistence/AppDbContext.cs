namespace Catalog.Infrastructure.Persistence;

using MassTransit;
using Microsoft.EntityFrameworkCore;
using Catalog.Application.Interfaces;
using Catalog.Domain.Entities;

public class AppDbContext : DbContext, ICatalogDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();
    public DbSet<ProductVariantAttribute> ProductVariantAttributes => Set<ProductVariantAttribute>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();
    public DbSet<ProductRatingStats> ProductRatingStats => Set<ProductRatingStats>();
    public DbSet<PurchaseRecord> PurchaseRecords => Set<PurchaseRecord>();
    public DbSet<ProductReview> ProductReviews => Set<ProductReview>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        Seeds.CatalogSeedData.SeedAll(modelBuilder);

        // MassTransit Transactional Outbox entities
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
    }
}
