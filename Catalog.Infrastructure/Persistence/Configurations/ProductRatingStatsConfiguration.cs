namespace Catalog.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Catalog.Domain.Entities;

public class ProductRatingStatsConfiguration : IEntityTypeConfiguration<ProductRatingStats>
{
    public void Configure(EntityTypeBuilder<ProductRatingStats> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.AvgRating).HasPrecision(3, 2);

        builder.HasOne(r => r.Product)
            .WithOne(p => p.RatingStats)
            .HasForeignKey<ProductRatingStats>(r => r.ProductId);
    }
}
