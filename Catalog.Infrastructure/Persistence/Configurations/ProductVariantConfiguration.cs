namespace Catalog.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Catalog.Domain.Entities;

public class ProductVariantConfiguration : IEntityTypeConfiguration<ProductVariant>
{
    public void Configure(EntityTypeBuilder<ProductVariant> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SellerId).IsRequired();
        builder.Property(x => x.SkuCode).HasMaxLength(100).IsRequired();
        builder.HasIndex(x => x.SkuCode);
        builder.HasIndex(x => new { x.SellerId, x.SkuCode })
            .HasDatabaseName("IX_ProductVariants_SellerId_SkuCode")
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false");
        builder.Property(x => x.Name).HasMaxLength(255).IsRequired();
        builder.Property(x => x.Price).HasPrecision(18, 2);
        builder.Property(x => x.Currency).HasMaxLength(10);
        builder.Property(x => x.WeightGrams).IsRequired(false);
        builder.Property(x => x.LengthCm).IsRequired(false);
        builder.Property(x => x.WidthCm).IsRequired(false);
        builder.Property(x => x.HeightCm).IsRequired(false);
        builder.Property(x => x.InventoryInitLastError).HasMaxLength(500);
        builder.HasIndex(x => new { x.InventoryInitState, x.InventoryInitNextRetryAt });

        builder.HasOne(v => v.Product)
            .WithMany(p => p.Variants)
            .HasForeignKey(v => v.ProductId);

        builder.HasMany(v => v.Attributes)
            .WithOne(a => a.Variant)
            .HasForeignKey(a => a.VariantId);

        builder.HasMany(v => v.Images)
            .WithOne(i => i.Variant)
            .HasForeignKey(i => i.VariantId);
    }
}
