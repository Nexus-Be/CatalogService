namespace Catalog.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Catalog.Domain.Entities;

public class ProductVariantAttributeConfiguration : IEntityTypeConfiguration<ProductVariantAttribute>
{
    public void Configure(EntityTypeBuilder<ProductVariantAttribute> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.AttrName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.AttrValue).HasMaxLength(255).IsRequired();

        builder.HasOne(a => a.Variant)
            .WithMany(v => v.Attributes)
            .HasForeignKey(a => a.VariantId);
    }
}
