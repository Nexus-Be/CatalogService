namespace Catalog.Infrastructure.Persistence.Seeds;

using Microsoft.EntityFrameworkCore;
using Catalog.Domain.Entities;

public static class CatalogSeedData
{
    public static void SeedCategories(ModelBuilder modelBuilder)
    {
        var electronicsId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var fashionId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var seedDate = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        modelBuilder.Entity<Category>().HasData(
            new Category
            {
                Id = electronicsId,
                Name = "Thiết Bị Điện Tử",
                Description = "Điện thoại, máy tính, phụ kiện công nghệ",
                Level = 1,
                SortOrder = 1,
                IsDisabled = false,
                CreatedAt = seedDate,
                UpdatedAt = seedDate,
                IsDeleted = false
            },
            new Category
            {
                Id = fashionId,
                Name = "Thời Trang & Phụ Kiện",
                Description = "Quần áo, giày dép, túi xách",
                Level = 1,
                SortOrder = 2,
                IsDisabled = false,
                CreatedAt = seedDate,
                UpdatedAt = seedDate,
                IsDeleted = false
            }
        );
    }
}
