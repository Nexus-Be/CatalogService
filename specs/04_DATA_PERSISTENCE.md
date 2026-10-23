# 04 — MÔ HÌNH THỰC THỂ & CƠ SỞ DỮ LIỆU (CATALOG SERVICE DATA PERSISTENCE)

---

## 1. MÔ HÌNH THỰC THỂ CHÍNH (DOMAIN ENTITIES)

1. **`Category`**:
   - `Id`: `Guid` (Primary Key).
   - `Name`: `string` (Max 100 chars).
   - `ParentId`: `Guid?` (Self-referencing Foreign Key tới danh mục cha).
   - `CreatedAt`, `UpdatedAt`: `DateTimeOffset`.
   - Navigation: `Category? Parent`, `ICollection<Category> SubCategories`, `ICollection<Product> Products`.

2. **`Product`**:
   - `Id`: `Guid`.
   - `SellerId`: `Guid` (Định danh người bán sở hữu).
   - `CategoryId`: `Guid` (FK $\rightarrow$ `Category.Id`).
   - `Name`: `string` (Max 250 chars).
   - `Description`: `string` (Text).
   - `Price`: `decimal` (Precision: 18, Scale: 2).
   - `Currency`: `string` (Mặc định: `"VND"`, Max 3 chars).
   - `Slug`: `string` (Unique Index, Max 300 chars).
   - `Status`: `string` (`"Active"`, `"Draft"`, `"Archived"`).
   - `CreatedAt`, `UpdatedAt`: `DateTimeOffset`.
   - Navigation: `Category Category`, `Inventory? Inventory`.

3. **`Inventory`**:
   - `Id`: `Guid`.
   - `ProductId`: `Guid` (Unique FK $\rightarrow$ `Product.Id`).
   - `SellerId`: `Guid`.
   - `Available`: `int` (Số lượng hàng sẵn sàng bán, bắt buộc $\ge 0$).
   - `Reserved`: `int` (Số lượng hàng đang bị tạm giữ bởi các đơn hàng đang chờ thanh toán).
   - `LastAdjustedAt`: `DateTimeOffset`.
   - `LastAdjustmentReason`: `string?`.

---

## 2. QUY CHUẨN CƠ SỞ DỮ LIỆU & EF CORE

- Engine: PostgreSQL.
- Database: `category_db` tại port `5434`.
- Chuỗi kết nối: `Host=localhost;Port=5434;Database=category_db;Username=postgres;Password=MatkhaucuaTui123@`.
- Tên bảng: `categories`, `products`, `inventories`.
- **Concurrency Token**: Cấu hình thuộc tính Version / RowVersion (hoặc `xmin` trên PostgreSQL) trên bảng `inventories` để ngăn chặn lỗi bán vượt tồn kho (race condition / overselling).
- **No Repository Pattern**: Tầng Application tương tác trực tiếp với `CatalogDbContext`.
