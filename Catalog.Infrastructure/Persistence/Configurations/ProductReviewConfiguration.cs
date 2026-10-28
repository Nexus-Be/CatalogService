namespace Catalog.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Catalog.Domain.Entities;

public class ProductReviewConfiguration : IEntityTypeConfiguration<ProductReview>
{
    public void Configure(EntityTypeBuilder<ProductReview> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Comment).HasMaxLength(2000);

        builder.HasOne(r => r.PurchaseRecord)
            .WithMany(pr => pr.ProductReviews)
            .HasForeignKey(r => r.PurchaseRecordId);

        builder.HasOne(r => r.Product)
            .WithMany(p => p.Reviews)
            .HasForeignKey(r => r.ProductId);
    }
}
