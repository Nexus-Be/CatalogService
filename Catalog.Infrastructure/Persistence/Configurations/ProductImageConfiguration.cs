namespace Catalog.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Catalog.Domain.Entities;

public class ProductImageConfiguration : IEntityTypeConfiguration<ProductImage>
{
    public void Configure(EntityTypeBuilder<ProductImage> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Url).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.AltText).HasMaxLength(500);

        builder.HasOne(i => i.Product)
            .WithMany(p => p.Images)
            .HasForeignKey(i => i.ProductId);

        builder.HasOne(i => i.Variant)
            .WithMany(v => v.Images)
            .HasForeignKey(i => i.VariantId);
    }
}
