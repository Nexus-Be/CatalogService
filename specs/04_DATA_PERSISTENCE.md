# 04 — MÔ HÌNH THỰC THỂ & CƠ SỞ DỮ LIỆU (CATALOG SERVICE DATA PERSISTENCE)

> **Trạng thái triển khai (2026-10-24):** ⚠️ Một phần — **9/10 entity đặc tả có thật** + 6 entity phát sinh; **12 bảng** được tạo bởi 1 migration; **❌ không có entity/bảng `Inventory`** (kho ảo chỉ là mock in-memory); **0 ràng buộc concurrency/overselling**; **0 repository** (đúng quy tắc).
> **Nguồn sự thật:** code trong repo này (bằng chứng `file:dòng`); không tham chiếu tài liệu ngoài repo.
> **Cập nhật lần cuối:** 2026-10-24.

---

## 1. MÔ HÌNH THỰC THỂ CHÍNH (DOMAIN ENTITIES)

1. **`Category`** (`Catalog.Domain/Entities/Category.cs:5-18`):
   - `Id`: `Guid` (Primary Key).
   - `Name`: `string` (Max **255** chars — `Catalog.Infrastructure/Persistence/Configurations/CategoryConfiguration.cs:12`; ❌ đặc tả cũ ghi 100 là **sai**).
   - `Description`: `string?`.
   - `ParentId`: `Guid?` (Self-referencing Foreign Key tới danh mục cha).
   - `Level`: `short`; `SortOrder`: `int`; `IsDisabled`: `bool`.
   - `CreatedAt`, `UpdatedAt`: `DateTimeOffset` (từ `BaseEntity<Guid>`), `CreatedBy`/`UpdatedBy` (từ `AuditableEntity<Guid>`), `IsDeleted`/`DeletedAt`.
   - Navigation thật: `Category? Parent`, `ICollection<Category> Children` (**không phải** `SubCategories`), `ICollection<Product> Products`.

2. **`Product`** (`Catalog.Domain/Entities/Product.cs:6-26`):
   - `Id`: `Guid`; `SellerId`: `Guid`; `CategoryId`: `Guid` (FK $\rightarrow$ `Category.Id`).
   - `Name`: `string` (Max **255** — `ProductConfiguration.cs:12`; ❌ đặc tả cũ ghi 250 là **sai**).
   - `Description`: `string?` (Max **4000** — `ProductConfiguration.cs:13`).
   - `Brand`: `string?` (Max 150).
   - `SaleType` (enum `Regular`/`Auction`), `Specifications` (jsonb — `ProductConfiguration.cs:18`), `PublishedAt`: `DateTimeOffset?`.
   - `Status`: enum `ProductStatus` lưu dạng **string** (`HasConversion<string>`), giá trị thật chỉ có **`Draft`, `Active`, `Inactive`** — ❌ **không có `"Archived"`**.
   - `StatusReason`: `ProductInactiveReason?` (enum, lưu string).
   - ❌ **Không có** `Price`, `Currency`, `Slug` trên `Product`: giá và tiền tệ nằm ở `ProductVariant.Price` (`decimal?`, Precision 18/2) và `ProductVariant.Currency` (mặc định `"VND"`) — `ProductVariant.cs:11-12`.
   - ❌ **Không có** navigation `Inventory? Inventory` (không tồn tại entity `Inventory`).
   - Navigation thật: `Category`, `Variants`, `Images`, `RatingStats`, `PurchaseRecords`, `Reviews`.

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
