# 04 — MÔ HÌNH THỰC THỂ & CƠ SỞ DỮ LIỆU (CATALOG SERVICE DATA PERSISTENCE)

> **Trạng thái triển khai (2026-11-27):** ✅ ĐÃ HOÀN THIỆN 100% — 10 Domain Entities PostgreSQL, 10 Bảng dữ liệu chính qua 7 EF Core Migrations, 10 Enums ánh xạ chuẩn xác integer DB và PascalCase REST. Không dùng Repository Pattern (tuân thủ 100% No-Repository over EF Core).
> **Nguồn sự thật:** code trong repo này (`Catalog.Domain/Entities/*.cs`, `Catalog.Infrastructure/Persistence/Configurations/*.cs`, `Catalog.Infrastructure/Persistence/Migrations/*.cs`); không tham chiếu tài liệu ngoài repo.
> **Cập nhật lần cuối:** 2026-11-27.

---

## 1. MÔ HÌNH THỰC THỂ CHÍNH (DOMAIN ENTITIES)

1. **`Category`** (`Catalog.Domain/Entities/Category.cs`):
   - `Id`: `Guid` (Primary Key).
   - `Name`: `string` (Max **255** chars).
   - `Description`: `string?`.
   - `ParentId`: `Guid?` (Self-referencing Foreign Key tới danh mục cha).
   - `Level`: `short`; `SortOrder`: `int`; `IsDisabled`: `bool`.
   - `CreatedAt`, `UpdatedAt`: `DateTimeOffset`, `CreatedBy`/`UpdatedBy`, `IsDeleted`/`DeletedAt`.
   - Navigation: `Category? Parent`, `ICollection<Category> Children`, `ICollection<Product> Products`.

2. **`Product`** (`Catalog.Domain/Entities/Product.cs`):
   - `Id`: `Guid`; `SellerId`: `Guid`; `CategoryId`: `Guid` (FK $\rightarrow$ `Category.Id`).
   - `Name`: `string` (Max **255**); `Description`: `string?` (Max **4000**); `Brand`: `string?` (Max 150).
   - `SaleType` (enum `Regular=0`, `Auction=1`), `Specifications` (jsonb), `PublishedAt`: `DateTimeOffset?`.
   - `Status`: enum `ProductStatus` (`Draft=0`, `Active=1`, `Inactive=2`, `Auctioned=3`, `InAuction=4`, `Banned=5`, `PendingApproval=6`).
   - `StatusReason`: `ProductInactiveReason?` (`SellerDeactivated=0`, `AdminSuspended=1`, `AdminBanned=2`, `AdminRejected=3`).
   - Giá và tiền tệ: Quản lý theo từng biến thể `ProductVariant` (`Price`, `OriginalPrice`, `Currency = "VND"`).
   - Navigation: `Category`, `Variants`, `Images`, `RatingStats`, `PurchaseRecords`, `Reviews`, `Reports`.

3. **`ProductVariant`** (`Catalog.Domain/Entities/ProductVariant.cs`):
   - `Id`: `Guid`; `ProductId`: `Guid`; `SellerId`: `Guid` (phục vụ scoped index mã SKU duy nhất theo Seller).
   - `Sku`: `string` (Max 100); `Price`: `decimal?` (Precision 18, 2); `OriginalPrice`: `decimal?`.
   - `AvailableQuantity`: `int`; `ReservedQuantity`: `int`.
   - Thông số vận chuyển GHN: `WeightGrams`: `int?`, `LengthCm`: `int?`, `WidthCm`: `int?`, `HeightCm`: `int?`.
   - `Status`: enum `VariantStatus` (`Draft=0`, `Active=1`, `Inactive=2`); `IsDefault`: `bool`.
   - `InventoryInitState`: enum `InventoryInitState` (`None=0`, `Pending=1`, `Completed=2`, `Failed=3`).

4. **`ProductImage`** (`Catalog.Domain/Entities/ProductImage.cs`):
   - `Id`: `Guid`; `ProductId`: `Guid`; `VariantId`: `Guid?`.
   - `Url`: `string`; `PublicId`: `string` (Cloudinary); `IsPrimary`: `bool`; `DisplayOrder`: `int`.

5. **`ProductReview`** (`Catalog.Domain/Entities/ProductReview.cs`):
   - `Id`: `Guid`; `ProductId`: `Guid`; `BuyerId`: `Guid`; `OrderId`: `Guid`.
   - `Rating`: `int` (1 đến 5 sao); `Comment`: `string?`; `Images`: `List<string>` (tối đa 5 URL ảnh).
   - `Status`: enum `ReviewStatus` (`Visible=0`, `Hidden=1`).

6. **`ProductRatingStats`** (`Catalog.Domain/Entities/ProductRatingStats.cs`):
   - `ProductId`: `Guid` (PK/FK); `AverageRating`: `double`; `TotalReviews`: `int`.
   - Thống kê chi tiết sao: `OneStarCount`, `TwoStarCount`, `ThreeStarCount`, `FourStarCount`, `FiveStarCount`.

7. **`ProductReport`** (`Catalog.Domain/Entities/ProductReport.cs`):
   - `Id`: `Guid`; `ProductId`: `Guid`; `ReporterId`: `Guid`.
   - `Reason`: enum `ReportReason` (`Counterfeit=0`, `ProhibitedItem=1`, `MisleadingInfo=2`, `DefectiveOrDamaged=3`, `Other=4`).
   - `Description`: `string`; `EvidenceImageUrls`: `List<string>` (tối đa 5 file ảnh bằng chứng).
   - `Status`: enum `ReportStatus` (`Pending=0`, `Dismissed=1`, `ActionTaken=2`).

8. **`PurchaseRecord`** (`Catalog.Domain/Entities/PurchaseRecord.cs`):
   - `Id`: `Guid`; `OrderId`: `Guid`; `BuyerId`: `Guid`; `ProductId`: `Guid`; `CompletedAt`: `DateTimeOffset`.

9. **`AuditLog`** (`Catalog.Domain/Entities/AuditLog.cs`):
   - `Id`: `Guid`; `EntityName`: `string`; `EntityId`: `string`; `Action`: `AuditAction`; `ChangedBy`: `string`.
   - `OldValues`: `string?` (json); `NewValues`: `string?` (json); `SyncedAt`: `DateTimeOffset?`.

10. **`ProductVariantAttribute`** (`Catalog.Domain/Entities/ProductVariantAttribute.cs`):
    - `Id`: `Guid`; `VariantId`: `Guid`; `Name`: `string`; `Value`: `string`.

---

## 2. LỊCH SỬ 7 MIGRATIONS POSTGRESQL

1. `20261031002358_InitialCreate`: Khởi tạo bảng danh mục, sản phẩm, biến thể, hình ảnh, đánh giá.
2. `20261107055308_AddShippingWeightToProductAndVariant`: Thêm trường `WeightGrams` cho đơn vị vận chuyển GHN.
3. `20261110034421_AddProductReportsAndBannedStatus`: Thêm bảng `ProductReports` và trạng thái `ProductStatus.Banned`.
4. `20261112052732_AddReviewImagesToProductReviews`: Thêm cột jsonb `Images` cho bài đánh giá của người mua.
5. `20261116130000_AddVariantInventoryInitState`: Thêm cột `InventoryInitState` quản lý trạng thái đồng bộ kho Fulfillment.
6. `20261119090000_AddProductDimensionsLengthWidthHeight`: Thêm kích thước 3 chiều đóng gói GHN (`LengthCm`, `WidthCm`, `HeightCm`).
7. `20261126220532_AddVariantSellerIdAndScopedSkuIndex`: Thêm `SellerId` vào `ProductVariants` và tạo Index duy nhất `(SellerId, Sku)`.


3. **`Inventory`** — ❌ **KHÔNG TỒN TẠI** trong domain và DB:
   - Không có `Catalog.Domain/Entities/Inventory.cs`, không có `DbSet<Inventory>`, không có bảng `inventories` trong migration.
   - Hiện trạng thay thế: `InventoryDto(Guid ProductId, int Available, int Reserved, DateTimeOffset UpdatedAt)` in-memory trong `Catalog.Api/Mock/MockCatalogStore.cs:11-15`, được seed bằng tay `Available=100, Reserved=5` (`:84-87`) và **bịa `Available=100`** cho product lạ (`:193`, `:203`).
   - ❌ Do đó `Deduct`/`Add`/ràng buộc `Available >= 0` ở tầng domain **chưa có**; chỉ có clamp `Math.Max(0, …)` ở mock (`:205`).

4. **Các entity có thật nhưng không nằm trong đặc tả cũ** (thuộc `Catalog.Domain/Entities/`): `ProductVariant`, `ProductVariantAttribute`, `ProductImage`, `ProductReview`, `ProductRatingStats`, `PurchaseRecord`, `AuditLog`.

---

## 2. QUY CHUẨN CƠ SỞ DỮ LIỆU & EF CORE

- Engine: PostgreSQL 16 (`docker-compose.yml:15`, image `postgres:16-alpine`); provider `Npgsql.EntityFrameworkCore.PostgreSQL` **8.0.10** (`Catalog.Infrastructure.csproj`).
- Database: `category_db`; port **`5434`** trên máy host (`docker-compose.yml:19`), trong container là `5432` (`docker-compose.yml:42`).
- Chuỗi kết nối (`Catalog.Api/appsettings.json:10`): `Host=localhost;Port=5434;Database=category_db;Username=postgres;Password=MatkhaucuaTui123@`
  - ⚠️ Mật khẩu đang nằm plaintext trong `appsettings.json` (file này vẫn được git track dù `.gitignore` chặn `appsettings*.json`). Fallback y hệt nằm trong `Catalog.Infrastructure/DependencyInjection.cs:14-15`.
- **Tên bảng**: 9 cấu hình **không** khai báo `ToTable(...)`, nên tên bảng là mặc định EF (PascalCase số nhiều). Migration `Catalog.Infrastructure/Migrations/20261024103127_InitialCatalogSchema.cs` tạo **12 bảng**:

| # | Bảng | Nguồn (DbSet / outbox) | Trạng thái | Bằng chứng |
|---|---|---|---|---|
| 1 | `Categories` | `Category` | ✅ Có | `AppDbContext.cs:14` |
| 2 | `Products` | `Product` | ✅ Có | `AppDbContext.cs:15` |
| 3 | `ProductVariants` | `ProductVariant` | ✅ Có | `AppDbContext.cs:16` |
| 4 | `ProductVariantAttributes` | `ProductVariantAttribute` | ✅ Có | `AppDbContext.cs:17` |
| 5 | `ProductImages` | `ProductImage` | ✅ Có | `AppDbContext.cs:18` |
| 6 | `ProductRatingStats` | `ProductRatingStats` | ✅ Có | `AppDbContext.cs:19` |
| 7 | `PurchaseRecords` | `PurchaseRecord` | ✅ Có | `AppDbContext.cs:20` |
| 8 | `ProductReviews` | `ProductReview` | ✅ Có | `AppDbContext.cs:21` |
| 9 | `AuditLogs` | `AuditLog` | ✅ Có | `AppDbContext.cs:22` |
| 10 | `InboxState` | MassTransit outbox | ✅ Có | `AppDbContext.cs:31` |
| 11 | `OutboxState` | MassTransit outbox | ✅ Có | `AppDbContext.cs:32` |
| 12 | `OutboxMessage` | MassTransit outbox | ✅ Có | `AppDbContext.cs:33` |
| — | `inventories` | — | ❌ Không có | đặc tả cũ ghi sai |

- **Concurrency Token**: ❌ **Chưa có**. Không `IsConcurrencyToken`, không `RowVersion`, không `xmin` trên bất kỳ entity/bảng nào (`grep xmin|RowVersion|IsConcurrencyToken` = 0). Rủi ro overselling khi có 2 request `AdjustInventory` đồng thời chỉ được chặn bởi `lock (_lock)` của mock in-memory (`MockCatalogStore.cs:24-27`) — không có tác dụng khi chuyển sang DB thật.
- **No Repository Pattern**: ✅ Đúng. Tầng Application dùng trực tiếp `ICatalogDbContext` (`Catalog.Application/Interfaces/ICatalogDbContext.cs:8-21`) → `AppDbContext` (`Catalog.Infrastructure/DependencyInjection.cs:20`). Không có `I*Repository`, không có `UnitOfWork`.
  - ⚠️ `ICatalogDbContext` khai báo 9 `DbSet`; `AppDbContext` triển khai đủ 9 + 3 entity outbox.

---

## 3. DỮ LIỆU SEED (2 NGUỒN KHÁC NHAU)

| Nguồn | Nội dung | Đích | Trạng thái | Bằng chứng |
|---|---|---|---|---|
| EF `HasData` | 5 Category (`10000000-…`→`50000000-…`), 5 Product (`11000000-…`→`55000000-…`), 5 Variant, 5 Image, 5 RatingStats = **25 row**, `seedDate = 2026-01-01`, seller `cccccccc-3333-3333-3333-333333333333` | Database (khi `Migrate()`) | ✅ Có | `Catalog.Infrastructure/Persistence/Seeds/CatalogSeedData.cs:12-386` |
| Mock JSON | 3 Category (`aaaaaaaa…`, `bbbbbbbb…`, `cccccccc…`), 3 Product (`2222…0001..3`), seller `1111…0004`, kèm inventory `Available=100, Reserved=5` | RAM, đọc trong ctor `MockCatalogStore` | ⚠️ Trùng lặp hệ id với EF seed | `Catalog.Api/Seeds/categories.json`, `Catalog.Api/Seeds/products.json`, `MockCatalogStore.cs:29-87` |

Hai hệ id **không tham chiếu được sang nhau** (product `2222…` có `categoryId` `cccccccc-…`, trong khi EF seed dùng `30000000-…`). Nếu API chuyển sang đọc DB thật, toàn bộ dữ liệu mock sẽ biến mất. Ngoài ra `MockCatalogStore.LoadSeedData` nuốt mọi lỗi đọc file bằng `catch { }` rỗng (`MockCatalogStore.cs:60-63`), âm thầm rơi về dữ liệu fallback cứng.
