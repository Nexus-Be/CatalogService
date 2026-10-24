namespace Catalog.Infrastructure.Persistence.Seeds;

using Microsoft.EntityFrameworkCore;
using Catalog.Domain.Entities;
using Catalog.Domain.Enums;

public static class CatalogSeedData
{
    public static void SeedAll(ModelBuilder modelBuilder)
    {
        var seedDate = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var sellerUserId = Guid.Parse("cccccccc-3333-3333-3333-333333333333");

        // 1. Categories (5 Categories)
        var electronicsCatId = Guid.Parse("10000000-0000-0000-0000-000000000001");
        var fashionCatId = Guid.Parse("20000000-0000-0000-0000-000000000002");
        var homeCatId = Guid.Parse("30000000-0000-0000-0000-000000000003");
        var sportsCatId = Guid.Parse("40000000-0000-0000-0000-000000000004");
        var booksCatId = Guid.Parse("50000000-0000-0000-0000-000000000005");

        modelBuilder.Entity<Category>().HasData(
            new Category
            {
                Id = electronicsCatId,
                Name = "Thiết Bị Điện Tử & Công Nghệ",
                Description = "Điện thoại, máy tính, tai nghe và phụ kiện thông minh",
                Level = 1,
                SortOrder = 1,
                IsDisabled = false,
                CreatedAt = seedDate,
                UpdatedAt = seedDate,
                IsDeleted = false
            },
            new Category
            {
                Id = fashionCatId,
                Name = "Thời Trang & Phụ Kiện",
                Description = "Quần áo, áo khoác, phụ kiện thời trang unisex",
                Level = 1,
                SortOrder = 2,
                IsDisabled = false,
                CreatedAt = seedDate,
                UpdatedAt = seedDate,
                IsDeleted = false
            },
            new Category
            {
                Id = homeCatId,
                Name = "Nhà Cửa & Đời Sống",
                Description = "Thiết bị gia dụng tiện ích và đồ dùng gia đình",
                Level = 1,
                SortOrder = 3,
                IsDisabled = false,
                CreatedAt = seedDate,
                UpdatedAt = seedDate,
                IsDeleted = false
            },
            new Category
            {
                Id = sportsCatId,
                Name = "Thể Thao & Dã Ngoại",
                Description = "Dụng cụ thể thao, xe đạp và đồ bảo hộ dã ngoại",
                Level = 1,
                SortOrder = 4,
                IsDisabled = false,
                CreatedAt = seedDate,
                UpdatedAt = seedDate,
                IsDeleted = false
            },
            new Category
            {
                Id = booksCatId,
                Name = "Sách & Tri Thức",
                Description = "Sách công nghệ thông tin, kiến trúc phần mềm và văn hóa doanh nghiệp",
                Level = 1,
                SortOrder = 5,
                IsDisabled = false,
                CreatedAt = seedDate,
                UpdatedAt = seedDate,
                IsDeleted = false
            }
        );

        // 2. Products (5 Products in 5 different Categories)
        var product1Id = Guid.Parse("11000000-0000-0000-0000-000000000001");
        var product2Id = Guid.Parse("22000000-0000-0000-0000-000000000002");
        var product3Id = Guid.Parse("33000000-0000-0000-0000-000000000003");
        var product4Id = Guid.Parse("44000000-0000-0000-0000-000000000004");
        var product5Id = Guid.Parse("55000000-0000-0000-0000-000000000005");

        modelBuilder.Entity<Product>().HasData(
            new Product
            {
                Id = product1Id,
                SellerId = sellerUserId,
                CategoryId = electronicsCatId,
                Name = "Smartphone Nexus X Pro 256GB",
                Description = "Flagship phone with OLED 120Hz display, 50MP triple camera system and 5000mAh battery.",
                Brand = "Nexus",
                SaleType = SaleType.Regular,
                Status = ProductStatus.Active,
                Specifications = "{\"ram\":\"12GB\",\"storage\":\"256GB\",\"chipset\":\"Snapdragon 8 Gen 3\"}",
                PublishedAt = seedDate,
                CreatedAt = seedDate,
                UpdatedAt = seedDate,
                IsDeleted = false
            },
            new Product
            {
                Id = product2Id,
                SellerId = sellerUserId,
                CategoryId = fashionCatId,
                Name = "Áo Hoodie Nexus Unisex Cotton",
                Description = "Áo hoodie chất liệu cotton 100% thoáng mát, form rộng unisex phong cách trẻ trung năng động.",
                Brand = "Nexus Wear",
                SaleType = SaleType.Regular,
                Status = ProductStatus.Active,
                Specifications = "{\"material\":\"Cotton 100%\",\"color\":\"Black\",\"size\":\"L\"}",
                PublishedAt = seedDate,
                CreatedAt = seedDate,
                UpdatedAt = seedDate,
                IsDeleted = false
            },
            new Product
            {
                Id = product3Id,
                SellerId = sellerUserId,
                CategoryId = homeCatId,
                Name = "Ấm Siêu Tốc Thông Minh Nexus 1.7L",
                Description = "Ấm đun nước siêu tốc inox 304 giữ nhiệt và điều khiển nhiệt độ thông minh qua app.",
                Brand = "Nexus Home",
                SaleType = SaleType.Regular,
                Status = ProductStatus.Active,
                Specifications = "{\"capacity\":\"1.7L\",\"power\":\"1800W\",\"material\":\"Inox 304\"}",
                PublishedAt = seedDate,
                CreatedAt = seedDate,
                UpdatedAt = seedDate,
                IsDeleted = false
            },
            new Product
            {
                Id = product4Id,
                SellerId = sellerUserId,
                CategoryId = sportsCatId,
                Name = "Mũ Bảo Hiểm Xe Đạp Nexus Aero",
                Description = "Mũ bảo hiểm khí động học trọng lượng siêu nhẹ chuẩn an toàn quốc tế CE EN 1078.",
                Brand = "Nexus Sport",
                SaleType = SaleType.Auction,
                Status = ProductStatus.Active,
                Specifications = "{\"weight\":\"220g\",\"size\":\"M (54-58cm)\",\"color\":\"Race Red\"}",
                PublishedAt = seedDate,
                CreatedAt = seedDate,
                UpdatedAt = seedDate,
                IsDeleted = false
            },
            new Product
            {
                Id = product5Id,
                SellerId = sellerUserId,
                CategoryId = booksCatId,
                Name = "Clean Architecture trong Microservices",
                Description = "Cẩm nang chuyên sâu về kiến trúc phần mềm hướng miền và thiết kế hệ thống phân tán chịu tải cao.",
                Brand = "Nexus Press",
                SaleType = SaleType.Regular,
                Status = ProductStatus.Active,
                Specifications = "{\"pages\":420,\"publisher\":\"Nexus Press\",\"language\":\"Vietnamese\"}",
                PublishedAt = seedDate,
                CreatedAt = seedDate,
                UpdatedAt = seedDate,
                IsDeleted = false
            }
        );

        // 3. Product Variants (1 Variant per Product with SKU and Pricing)
        var variant1Id = Guid.Parse("11100000-0000-0000-0000-000000000001");
        var variant2Id = Guid.Parse("22100000-0000-0000-0000-000000000002");
        var variant3Id = Guid.Parse("33100000-0000-0000-0000-000000000003");
        var variant4Id = Guid.Parse("44100000-0000-0000-0000-000000000004");
        var variant5Id = Guid.Parse("55100000-0000-0000-0000-000000000005");

        modelBuilder.Entity<ProductVariant>().HasData(
            new ProductVariant
            {
                Id = variant1Id,
                ProductId = product1Id,
                SkuCode = "NX-PHONE-256",
                Name = "Smartphone Nexus X Pro 256GB Midnight Black",
                Price = 15000000m,
                Currency = "VND",
                IsActive = true,
                HasOrder = true,
                Status = VariantStatus.Active,
                CreatedAt = seedDate,
                UpdatedAt = seedDate,
                IsDeleted = false
            },
            new ProductVariant
            {
                Id = variant2Id,
                ProductId = product2Id,
                SkuCode = "NX-HOODIE-L",
                Name = "Áo Hoodie Nexus Unisex Cotton - Đen / Size L",
                Price = 450000m,
                Currency = "VND",
                IsActive = true,
                HasOrder = true,
                Status = VariantStatus.Active,
                CreatedAt = seedDate,
                UpdatedAt = seedDate,
                IsDeleted = false
            },
            new ProductVariant
            {
                Id = variant3Id,
                ProductId = product3Id,
                SkuCode = "NX-KETTLE-17",
                Name = "Ấm Siêu Tốc Thông Minh Nexus 1.7L Trắng Tinh Khôi",
                Price = 850000m,
                Currency = "VND",
                IsActive = true,
                HasOrder = true,
                Status = VariantStatus.Active,
                CreatedAt = seedDate,
                UpdatedAt = seedDate,
                IsDeleted = false
            },
            new ProductVariant
            {
                Id = variant4Id,
                ProductId = product4Id,
                SkuCode = "NX-HELMET-M",
                Name = "Mũ Bảo Hiểm Xe Đạp Nexus Aero - Đỏ / Size M",
                Price = 620000m,
                Currency = "VND",
                IsActive = true,
                HasOrder = false,
                Status = VariantStatus.Active,
                CreatedAt = seedDate,
                UpdatedAt = seedDate,
                IsDeleted = false
            },
            new ProductVariant
            {
                Id = variant5Id,
                ProductId = product5Id,
                SkuCode = "NX-BOOK-ARCH",
                Name = "Clean Architecture trong Microservices (Bìa Cứng)",
                Price = 350000m,
                Currency = "VND",
                IsActive = true,
                HasOrder = false,
                Status = VariantStatus.Active,
                CreatedAt = seedDate,
                UpdatedAt = seedDate,
                IsDeleted = false
            }
        );

        // 4. Product Images
        modelBuilder.Entity<ProductImage>().HasData(
            new ProductImage
            {
                Id = Guid.Parse("11200000-0000-0000-0000-000000000001"),
                ProductId = product1Id,
                VariantId = variant1Id,
                Url = "https://images.nexus.local/products/phone-black.png",
                AltText = "Smartphone Nexus X Pro 256GB Black",
                IsActive = true,
                SortOrder = 1,
                CreatedAt = seedDate,
                UpdatedAt = seedDate,
                IsDeleted = false
            },
            new ProductImage
            {
                Id = Guid.Parse("22200000-0000-0000-0000-000000000002"),
                ProductId = product2Id,
                VariantId = variant2Id,
                Url = "https://images.nexus.local/products/hoodie-black.png",
                AltText = "Áo Hoodie Nexus Unisex Cotton Đen",
                IsActive = true,
                SortOrder = 1,
                CreatedAt = seedDate,
                UpdatedAt = seedDate,
                IsDeleted = false
            },
            new ProductImage
            {
                Id = Guid.Parse("33200000-0000-0000-0000-000000000003"),
                ProductId = product3Id,
                VariantId = variant3Id,
                Url = "https://images.nexus.local/products/kettle-white.png",
                AltText = "Ấm Siêu Tốc Thông Minh Nexus 1.7L Trắng",
                IsActive = true,
                SortOrder = 1,
                CreatedAt = seedDate,
                UpdatedAt = seedDate,
                IsDeleted = false
            },
            new ProductImage
            {
                Id = Guid.Parse("44200000-0000-0000-0000-000000000004"),
                ProductId = product4Id,
                VariantId = variant4Id,
                Url = "https://images.nexus.local/products/helmet-red.png",
                AltText = "Mũ Bảo Hiểm Xe Đạp Nexus Aero Đỏ",
                IsActive = true,
                SortOrder = 1,
                CreatedAt = seedDate,
                UpdatedAt = seedDate,
                IsDeleted = false
            },
            new ProductImage
            {
                Id = Guid.Parse("55200000-0000-0000-0000-000000000005"),
                ProductId = product5Id,
                VariantId = variant5Id,
                Url = "https://images.nexus.local/products/clean-arch-book.png",
                AltText = "Clean Architecture trong Microservices Bìa Cứng",
                IsActive = true,
                SortOrder = 1,
                CreatedAt = seedDate,
                UpdatedAt = seedDate,
                IsDeleted = false
            }
        );

        // 5. Product Rating Stats
        modelBuilder.Entity<ProductRatingStats>().HasData(
            new ProductRatingStats
            {
                Id = Guid.Parse("11300000-0000-0000-0000-000000000001"),
                ProductId = product1Id,
                StarSum = 50,
                ReviewCount = 10,
                AvgRating = 5.0m,
                CreatedAt = seedDate,
                UpdatedAt = seedDate,
                IsDeleted = false
            },
            new ProductRatingStats
            {
                Id = Guid.Parse("22300000-0000-0000-0000-000000000002"),
                ProductId = product2Id,
                StarSum = 24,
                ReviewCount = 5,
                AvgRating = 4.8m,
                CreatedAt = seedDate,
                UpdatedAt = seedDate,
                IsDeleted = false
            },
            new ProductRatingStats
            {
                Id = Guid.Parse("33300000-0000-0000-0000-000000000003"),
                ProductId = product3Id,
                StarSum = 40,
                ReviewCount = 8,
                AvgRating = 5.0m,
                CreatedAt = seedDate,
                UpdatedAt = seedDate,
                IsDeleted = false
            },
            new ProductRatingStats
            {
                Id = Guid.Parse("44300000-0000-0000-0000-000000000004"),
                ProductId = product4Id,
                StarSum = 15,
                ReviewCount = 3,
                AvgRating = 5.0m,
                CreatedAt = seedDate,
                UpdatedAt = seedDate,
                IsDeleted = false
            },
            new ProductRatingStats
            {
                Id = Guid.Parse("55300000-0000-0000-0000-000000000005"),
                ProductId = product5Id,
                StarSum = 50,
                ReviewCount = 10,
                AvgRating = 5.0m,
                CreatedAt = seedDate,
                UpdatedAt = seedDate,
                IsDeleted = false
            }
        );
    }
}
