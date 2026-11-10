namespace Catalog.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Catalog.Domain.Entities;

public class ProductReportConfiguration : IEntityTypeConfiguration<ProductReport>
{
    public void Configure(EntityTypeBuilder<ProductReport> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Description).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.ResolutionNote).HasMaxLength(2000);
        builder.Property(x => x.EvidenceImages).HasColumnType("jsonb");
        builder.Property(x => x.Reason).HasConversion<string>();
        builder.Property(x => x.Status).HasConversion<string>();

        builder.HasOne(r => r.Product)
            .WithMany(p => p.Reports)
            .HasForeignKey(r => r.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => new { r.ProductId, r.Status });
        builder.HasIndex(r => new { r.Status, r.CreatedAt });
    }
}
