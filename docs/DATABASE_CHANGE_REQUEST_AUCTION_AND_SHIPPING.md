# ĐỀ XUẤT THAY ĐỔI CƠ SỞ DỮ LIỆU (DATABASE CHANGE REQUEST - DCR)

* **Dự án:** CatalogService (Microservice phụ trách Danh mục & Sản phẩm)
* **Cơ sở dữ liệu:** PostgreSQL (`category_db`)
* **Người gửi:** Đội ngũ phát triển CatalogService
* **Người nhận:** Database Administrator (DBA) / Tech Lead
* **Ngày đề xuất:** 06/11/2026
* **Mức độ ảnh hưởng:** **Thấp - Tuyệt đối an toàn** (Chỉ thêm cột `NULLABLE`, không khoá bảng, không gây downtime, có script rollback đi kèm).

---

## I. MỤC TIÊU VÀ BỐI CẢNH (MOTIVATION)

CatalogService cần hỗ trợ 2 tính năng nghiệp vụ quan trọng tiếp theo của hệ thống:
1. **Quản lý vòng đời Sản phẩm Đấu giá:** Nhận biết sản phẩm đấu giá đã kết thúc phiên (`Auctioned`) để đóng băng trạng thái, không cho phép đấu giá lại hoặc chỉnh sửa thông tin.
2. **Tính cước phí vận chuyển chính xác:** Tích hợp với đơn vị vận chuyển (GHN, Viettel Post, GHTK) theo tiêu chuẩn sàn TMĐT (Shopee, Lazada). Cần thông số cân nặng (`gram`) và kích thước đóng gói thùng kiện (`dài x rộng x cao tính theo cm`) để tính cước thể tích quy đổi.

Sau khi cân nhắc các phương án, đội ngũ quyết định áp dụng **Mô hình Lai (Hybrid Model / Fallback)** chuẩn TMĐT:
* Thông số đóng gói mặc định đặt tại bảng **`Products`** (áp dụng cho toàn bộ sản phẩm đơn và **sản phẩm Đấu giá**).
* Bảng **`ProductVariants`** cho phép lưu thông số ghi đè (`NULLABLE`) cho các SKU có kích thước lệch hẳn (ví dụ: Tạ 1kg vs Tạ 10kg, Vali 20 inch vs 28 inch).

---

## II. CHI TIẾT THAY ĐỔI DATABASE

### 1. Về Trạng thái sản phẩm Đấu giá (`Auctioned`)
* **Hiện trạng:** Cột `Status` trong bảng `Products` đang dùng kiểu dữ liệu `text` (hoặc `varchar`), được ánh xạ từ C# Enum `ProductStatus`.
* **Thay đổi:** 
  * Bổ sung giá trị chuỗi `'Auctioned'` vào tập giá trị hợp lệ của cột `Status`.
  * *Ghi chú DBA:* Nếu database hiện tại **không có** `CHECK constraint` cứng trên cột `Status`, DBA không cần chạy câu lệnh nào cho phần này. Nếu DB có ràng buộc kiểm tra, xin vui lòng thêm `'Auctioned'` vào danh sách kiểm tra.

---

### 2. Về Thông số Vận chuyển (Cân nặng & Kích thước)
Bổ sung 4 cột vào bảng `Products` và 4 cột vào bảng `ProductVariants`:

| Tên cột | Kiểu dữ liệu PG | Nullable? | Ý nghĩa & Quy chuẩn đơn vị |
| :--- | :--- | :---: | :--- |
| `WeightGrams` | `integer` | **YES** | Trọng lượng thực tế đóng gói kiện hàng tính bằng **Gram (g)**. Ví dụ: `500` = 500g, `2000` = 2kg. |
| `LengthCm` | `numeric(10, 2)` | **YES** | Chiều dài kiện hàng sau đóng gói tính bằng **Centimet (cm)**. Ví dụ: `25.5`. |
| `WidthCm` | `numeric(10, 2)` | **YES** | Chiều rộng kiện hàng tính bằng **Centimet (cm)**. Ví dụ: `15.0`. |
| `HeightCm` | `numeric(10, 2)` | **YES** | Chiều cao kiện hàng tính bằng **Centimet (cm)**. Ví dụ: `10.0`. |

> **Tại sao đơn vị là Gram và Centimet?**
> Các đơn vị vận chuyển tại Việt Nam (GHN, Viettel Post) tính nấc cước từ 50g, 100g, 250g, 500g... Việc dùng số nguyên `integer` cho `gram` tránh triệt để lỗi làm tròn dấu phẩy động của kilogram (ví dụ: `0.25` kg).

---

## III. SCRIPT SQL THỰC THI (POSTGRESQL DDL)

Kính gửi DBA đoạn script đã được viết sẵn an toàn, có bọc `TRANSACTION` và sử dụng `IF NOT EXISTS`:

```sql
-- =========================================================================
-- MIGRATION SCRIPT: ADD SHIPPING DIMENSIONS TO PRODUCTS & VARIANTS
-- Target Database: category_db (PostgreSQL)
-- Safe execution: Non-blocking, Add NULLABLE columns only
-- =========================================================================

BEGIN;

-- 1. Bổ sung các cột thông số vận chuyển vào bảng Products (Thông số chuẩn/mặc định)
ALTER TABLE "Products"
    ADD COLUMN IF NOT EXISTS "WeightGrams" integer NULL,
    ADD COLUMN IF NOT EXISTS "LengthCm" numeric(10, 2) NULL,
    ADD COLUMN IF NOT EXISTS "WidthCm" numeric(10, 2) NULL,
    ADD COLUMN IF NOT EXISTS "HeightCm" numeric(10, 2) NULL;

-- Đánh chú thích (Comment) cho bảng Products
COMMENT ON COLUMN "Products"."WeightGrams" IS 'Vận chuyển: Trọng lượng đóng gói thực tế (Gram). Ví dụ: 500g';
COMMENT ON COLUMN "Products"."LengthCm" IS 'Vận chuyển: Chiều dài bưu kiện (cm). Ví dụ: 25.5';
COMMENT ON COLUMN "Products"."WidthCm" IS 'Vận chuyển: Chiều rộng bưu kiện (cm). Ví dụ: 15.0';
COMMENT ON COLUMN "Products"."HeightCm" IS 'Vận chuyển: Chiều cao bưu kiện (cm). Ví dụ: 10.0';


-- 2. Bổ sung các cột thông số vận chuyển vào bảng ProductVariants (Dùng để ghi đè theo từng SKU)
ALTER TABLE "ProductVariants"
    ADD COLUMN IF NOT EXISTS "WeightGrams" integer NULL,
    ADD COLUMN IF NOT EXISTS "LengthCm" numeric(10, 2) NULL,
    ADD COLUMN IF NOT EXISTS "WidthCm" numeric(10, 2) NULL,
    ADD COLUMN IF NOT EXISTS "HeightCm" numeric(10, 2) NULL;

-- Đánh chú thích (Comment) cho bảng ProductVariants
COMMENT ON COLUMN "ProductVariants"."WeightGrams" IS 'Vận chuyển riêng của SKU (Gram). Nếu NULL, tự động kế thừa từ Products.WeightGrams';
COMMENT ON COLUMN "ProductVariants"."LengthCm" IS 'Vận chuyển riêng của SKU (cm). Nếu NULL, kế thừa từ Products.LengthCm';
COMMENT ON COLUMN "ProductVariants"."WidthCm" IS 'Vận chuyển riêng của SKU (cm). Nếu NULL, kế thừa từ Products.WidthCm';
COMMENT ON COLUMN "ProductVariants"."HeightCm" IS 'Vận chuyển riêng của SKU (cm). Nếu NULL, kế thừa từ Products.HeightCm';

COMMIT;
```

---

## IV. SCRIPT ROLLBACK (KHI CẦN HOÀN TÁC)

Nếu có sự cố phát sinh cần rollback về schema cũ:

```sql
BEGIN;

ALTER TABLE "ProductVariants"
    DROP COLUMN IF EXISTS "WeightGrams",
    DROP COLUMN IF EXISTS "LengthCm",
    DROP COLUMN IF EXISTS "WidthCm",
    DROP COLUMN IF EXISTS "HeightCm";

ALTER TABLE "Products"
    DROP COLUMN IF EXISTS "WeightGrams",
    DROP COLUMN IF EXISTS "LengthCm",
    DROP COLUMN IF EXISTS "WidthCm",
    DROP COLUMN IF EXISTS "HeightCm";

COMMIT;
```

---

## V. LOGIC TRUY VẤN MẪU (DÀNH CHO BACKEND & SERVICE GIAO HÀNG)

Khi Fulfillment Service hoặc đơn vị vận chuyển cần lấy kích thước của 1 SKU để tính cước, câu lệnh SQL sẽ tự động dùng hàm `COALESCE` để ưu tiên kích thước riêng của biến thể, nếu biến thể là `NULL` thì tự động dùng kích thước của sản phẩm:

```sql
SELECT 
    v."Id" AS "VariantId",
    v."SkuCode",
    p."Name" AS "ProductName",
    COALESCE(v."WeightGrams", p."WeightGrams") AS "EffectiveWeightGrams",
    COALESCE(v."LengthCm", p."LengthCm") AS "EffectiveLengthCm",
    COALESCE(v."WidthCm", p."WidthCm") AS "EffectiveWidthCm",
    COALESCE(v."HeightCm", p."HeightCm") AS "EffectiveHeightCm"
FROM "ProductVariants" v
INNER JOIN "Products" p ON v."ProductId" = p."Id"
WHERE v."Id" = :targetVariantId;
```

