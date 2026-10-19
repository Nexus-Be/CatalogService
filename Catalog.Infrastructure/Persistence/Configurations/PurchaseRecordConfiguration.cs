namespace Catalog.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Catalog.Domain.Entities;

public class PurchaseRecordConfiguration : IEntityTypeConfiguration<PurchaseRecord>
{
    public void Configure(EntityTypeBuilder<PurchaseRecord> builder)
    {
        builder.HasKey(x => x.Id);

        builder.HasOne(pr => pr.Product)
            .WithMany(p => p.PurchaseRecords)
            .HasForeignKey(pr => pr.ProductId);

        builder.HasMany(pr => pr.ProductReviews)
            .WithOne(r => r.PurchaseRecord)
            .HasForeignKey(r => r.PurchaseRecordId);
    }
}
