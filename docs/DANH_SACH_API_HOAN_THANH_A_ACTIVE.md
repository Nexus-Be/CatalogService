# BÁO CÁO TỔNG HỢP CÁC API ĐÃ HOÀN THÀNH (NHÓM A_ACTIVE)
## DỰ ÁN NEXUS — PHÂN HỆ CATALOG SERVICE (PORT 8002)

> **Trạng thái:** ✅ 100% HOÀN THÀNH & KIỂM CHỨNG (13/13 APIs)  
> **Thư mục đối chiếu:** `D:\Apartment\FALL_26\FSA\Project\Catalog_api\A_Active`  
> **Repository triển khai:** `CatalogService` / `CatalogService_Refactor` (Branch `refactor-all-src`)  
> **Ngày cập nhật:** 2026-11-02  
> **Cơ sở dữ liệu:** PostgreSQL Neon Cloud (`catalog` / `category_db`)  

---

## 📌 PHẦN 1: MA TRẬN ĐỐI CHIẾU 13 API ĐÃ HOÀN THÀNH

| STT | File Tài Liệu (A_Active) | HTTP Method & Route Chuẩn | Route Alias | Mã Excel | Controller & Action Thực Tế | Phân Quyền (RBAC) | Trạng Thái Code |
| :---: | :--- | :--- | :---: | :---: | :--- | :---: | :---: |
| **01** | `R66_GET_PRODUCTS.md` | `GET /api/v1/products` | — | `CAT-06 / CAT-05` | `ProductsController.Search` | Public | ✅ Hoàn thành |
| **02** | `R67_GET_PRODUCTS_SEARCH.md` | `GET /api/v1/products/search` | `GET /api/v1/products` | `CAT-20 / CAT-21` | `ProductsController.Search` | Public | ✅ Hoàn thành |
| **03** | `R102_GET_PRODUCTS.md` | `GET /api/v1/seller/products` | `GET /api/v1/products?sellerId=` | `CAT-44 / CAT-06` | `SellerProductsController.SearchSellerProducts` | Seller, Admin | ✅ Hoàn thành |
| **04** | `R105_POST_PRODUCTS.md` | `POST /api/v1/products` | — | `CAT-01` | `ProductsController.Create` | Seller, Admin | ✅ Hoàn thành |
| **05** | `R115_POST_PRODUCT_VARIANTS.md` | `POST /api/v1/products/{id}/variants` | — | `CAT-07` | `ProductsController.AddVariant` | Seller, Admin | ✅ Hoàn thành |
| **06** | `R116_DELETE_VARIANT.md` | `DELETE /api/v1/variants/{id}` | — | `CAT-11` | `VariantsController.DeleteVariant` | Seller, Admin | ✅ Hoàn thành |
| **07** | `R117_PATCH_VARIANT.md` | `PATCH /api/v1/variants/{id}` | `PUT /api/v1/variants/{id}` | `CAT-08` | `VariantsController.UpdateVariant` | Seller, Admin | ✅ Hoàn thành |
| **08** | `R118_GET_VARIANT.md` | `GET /api/v1/variants/{id}` | — | `CAT-42` | `VariantsController.GetVariantById` | Public | ✅ Hoàn thành |
| **09** | `R120_GET_CATEGORIES.md` | `GET /api/v1/categories` | — | `CAT-14` | `CategoriesController.List` | Public | ✅ Hoàn thành |
| **10** | `R121_POST_CATEGORIES.md` | `POST /api/v1/categories` | — | `CAT-15` | `CategoriesController.Create` | Admin | ✅ Hoàn thành |
| **11** | `R122_PUT_CATEGORIES.md` | `PUT /api/v1/categories/{id}` | `PATCH /api/v1/categories/{id}` | `CAT-16` | `CategoriesController.Update` | Admin | ✅ Hoàn thành |
| **12** | `R123_DELETE_CATEGORIES.md` | `DELETE /api/v1/categories/{id}` | — | `CAT-17` | `CategoriesController.Delete` | Admin | ✅ Hoàn thành |
| **13** | `R124_GET_CATEGORIES.md` | `GET /api/v1/categories/{id}` | — | `CAT-18` | `CategoriesController.GetById` | Public | ✅ Hoàn thành |

---

## 📁 PHẦN 2: CHI TIẾT ĐẶC TẢ TỪNG NHÓM NGHIỆP VỤ

### 🏷️ NHÓM A: PHÂN HỆ DANH MỤC (CATEGORIES)

#### 1. `GET /api/v1/categories` — Lấy danh sách danh mục (R120_GET_CATEGORIES.md - CAT-14)
- **Mục đích:** Xem toàn bộ danh mục sản phẩm, hỗ trợ duyệt danh sách phẳng có phân trang hoặc duyệt dạng cây đa cấp.
- **Phân quyền:** `Public / AllowAnonymous`
- **Tham số truy vấn (Query):**
  - `page`: Chỉ mục trang (Mặc định: `1`).
  - `pageSize`: Kích thước trang (Mặc định: `10`, tối đa `100`).
  - `sortBy`: Trường sắp xếp (`sortOrder`, `name`, `level`, `createdAt`).
  - `sortDirection`: Chiều sắp xếp (`asc`, `desc`, mặc định: `asc`).
  - `tree`: Boolean (`true` = trả cây lồng nhau, `false` = danh sách phẳng).
  - `parentId`: GUID lọc danh mục con trực thuộc cha.
- **Dữ liệu trả về:** `PagedData<CategoryItemDto>` (chứa `id`, `name`, `slug`, `iconUrl`, `parentId`, `level`, `sortOrder`, `subCategoriesCount`).

#### 2. `GET /api/v1/categories/{id}` — Lấy chi tiết danh mục (R124_GET_CATEGORIES.md - CAT-18)
- **Mục đích:** Xem thông tin chi tiết một danh mục cụ thể; **tự động kiểm tra và nhúng cây con trực thuộc (`children`)** nếu có.
- **Phân quyền:** `Public / AllowAnonymous`
- **Tham số đường dẫn (Route):** `id` (GUID danh mục).
- **Dữ liệu trả về:** `CategoryDto` (chứa `id`, `name`, `slug`, `description`, `iconUrl`, `parentId`, `level`, `sortOrder`, `isActive`, `createdAt`, `updatedAt`, `children`).

#### 3. `POST /api/v1/categories` — Tạo danh mục mới (R121_POST_CATEGORIES.md - CAT-15)
- **Mục đích:** Admin tạo danh mục mới; nếu truyền `parentId` thì tự động tính `level = parent.Level + 1`.
- **Phân quyền:** `RequireRole("admin")`
- **Request Body:**
  ```json
  {
    "name": "string (bắt buộc, max 100 ký tự)",
    "description": "string? (tùy chọn)",
    "parentId": "uuid? (nếu là danh mục con)",
    "iconUrl": "string? (URL icon)",
    "sortOrder": 0
  }
  ```
- **Dữ liệu trả về:** `CategoryDto` (Mã HTTP `201 Created`).

#### 4. `PUT /api/v1/categories/{id}` — Cập nhật danh mục (R122_PUT_CATEGORIES.md - CAT-16)
- **Mục đích:** Cập nhật tên, mô tả, thứ tự sắp xếp, icon hoặc thay đổi danh mục cha (hỗ trợ cả method `PUT` và `PATCH`).
- **Phân quyền:** `RequireRole("admin")`
- **Request Body:** `UpdateCategoryRequest` (tên, mô tả, parentId, iconUrl, sortOrder, isActive).
- **Dữ liệu trả về:** `CategoryDto` (Mã HTTP `200 OK`).

#### 5. `DELETE /api/v1/categories/{id}` — Xóa danh mục (R123_DELETE_CATEGORIES.md - CAT-17)
- **Mục đích:** Xóa mềm danh mục (`IsDeleted = true`), tự động kiểm tra chặn xóa nếu danh mục đang chứa danh mục con hoặc chứa sản phẩm.
- **Phân quyền:** `RequireRole("admin")`
- **Dữ liệu trả về:** `204 NoContent`.

---

### 📦 NHÓM B: PHÂN HỆ SẢN PHẨM CÔNG KHAI & TÌM KIẾM (PUBLIC PRODUCTS)

#### 6. `GET /api/v1/products` — Lấy danh sách sản phẩm (R66_GET_PRODUCTS.md - CAT-06 / CAT-05)
- **Mục đích:** Khách hàng duyệt danh sách sản phẩm đang mở bán (`Status == Active`), hỗ trợ phân trang và sắp xếp.
- **Phân quyền:** `Public / AllowAnonymous`
- **Tham số truy vấn (Query):**
  - `page`: Chỉ mục trang (Mặc định: `1`).
  - `pageSize`: Kích thước trang (Mặc định: `20`, tối đa `100`).
  - `sortBy`: `createdAt` / `newest`, `price`, `name`, `rating`.
  - `sortDirection`: `asc` hoặc `desc` (Mặc định: `asc`).
- **Dữ liệu trả về:** `PagedData<ProductListItemDto>` (gồm `productId`, `name`, `brand`, `saleType`, `status`, `minPrice`, `maxPrice`, `primaryImageUrl`, `avgRating`).

#### 7. `GET /api/v1/products/search` — Tìm kiếm & Lọc nâng cao (R67_GET_PRODUCTS_SEARCH.md - CAT-20 / CAT-21)
- **Mục đích:** Tìm kiếm theo từ khóa và lọc đa chiều cho người mua hàng.
- **Route:** Ánh xạ qua attribute alias `[HttpGet("search")]` trỏ cùng action `ProductsController.Search`.
- **Phân quyền:** `Public / AllowAnonymous`
- **Tham số truy vấn (Query):**
  - `keyword`: Từ khóa tìm kiếm theo tên sản phẩm, thương hiệu, mô tả.
  - `categoryId`: Lọc theo danh mục.
  - `minPrice`, `maxPrice`: Khoảng giá biến thể.
  - `brand`: Lọc theo thương hiệu.
  - `saleType`: `Regular` (Bán thường) hoặc `Auction` (Đấu giá).
  - `minRating`: Lọc theo điểm đánh giá tối thiểu (ví dụ: từ 4 sao trở lên).
  - `sortBy`, `sortDirection`, `page`, `pageSize`.
- **Dữ liệu trả về:** `PagedData<ProductListItemDto>`.

---

### 🏪 NHÓM C: PHÂN HỆ SẢN PHẨM NGƯỜI BÁN (SELLER PRODUCTS)

#### 8. `GET /api/v1/seller/products` — Xem sản phẩm của shop (R102_GET_PRODUCTS.md - CAT-44)
- **Mục đích:** Người bán xem toàn bộ danh sách sản phẩm của shop mình (bao gồm cả `Draft`, `Active`, `Inactive`, `Suspended`), hỗ trợ lọc theo trạng thái và tìm kiếm nội bộ.
- **Phân quyền:** `RequireRole("seller", "admin")` (tự động lấy `currentUserId` từ header Gateway).
- **Hỗ trợ truy vấn mở rộng:** Cho phép gọi `GET /api/v1/products?sellerId={id}` để xem danh sách công khai của shop.
- **Dữ liệu trả về:** `PagedData<SellerProductListItemDto>`.

#### 9. `POST /api/v1/products` — Tạo sản phẩm mới (R105_POST_PRODUCTS.md - CAT-01)
- **Mục đích:** Người bán đăng sản phẩm mới ở trạng thái `Draft`, có thể kèm danh mục và thông tin biến thể khởi tạo.
- **Phân quyền:** `RequireRole("seller", "admin")`
- **Request Body:**
  ```json
  {
    "sellerId": "uuid? (nếu admin tạo hộ, nếu không tự lấy từ token)",
    "categoryId": "uuid (bắt buộc)",
    "name": "string (bắt buộc)",
    "description": "string?",
    "brand": "string?",
    "saleType": "Regular | Auction",
    "specifications": "string? (JSON format)"
  }
  ```
- **Dữ liệu trả về:** `ProductDto` (Mã HTTP `201 Created`).

---

### ⚙️ NHÓM D: PHÂN HỆ BIẾN THỂ SẢN PHẨM (VARIANTS / SKU)

#### 10. `GET /api/v1/variants/{id}` — Chi tiết biến thể SKU (R118_GET_VARIANT.md - CAT-42)
- **Mục đích:** Xem chi tiết một biến thể cụ thể (SKU, giá, giá gốc, số lượng tồn kho ảo, thuộc tính JSON, trạng thái).
- **Phân quyền:** `Public / AllowAnonymous`
- **Dữ liệu trả về:** `ProductVariantDto` (`id`, `productId`, `sku`, `name`, `price`, `originalPrice`, `inventory`, `attributes`, `isDefault`, `status`).

#### 11. `POST /api/v1/products/{id}/variants` — Thêm biến thể cho sản phẩm (R115_POST_PRODUCT_VARIANTS.md - CAT-07)
- **Mục đích:** Người bán thêm biến thể mới vào sản phẩm đã có; kiểm tra xác thực quyền sở hữu sản phẩm của shop.
- **Phân quyền:** `RequireRole("seller", "admin")`
- **Request Body:**
  ```json
  {
    "sku": "string (bắt buộc, duy nhất)",
    "name": "string (bắt buộc, ví dụ: 128GB - Titan Tự nhiên)",
    "price": 25000000,
    "originalPrice": 27000000,
    "inventory": 100,
    "attributes": "{\"color\":\"Titan Tự nhiên\",\"storage\":\"128GB\"}",
    "isDefault": false
  }
  ```
- **Dữ liệu trả về:** `ProductVariantDto` (Mã HTTP `201 Created`).

#### 12. `PATCH /api/v1/variants/{id}` — Cập nhật biến thể (R117_PATCH_VARIANT.md - CAT-08)
- **Mục đích:** Cập nhật thông tin giá bán, giá gốc, tồn kho ảo, tên và thuộc tính biến thể (hỗ trợ cả `PATCH` và `PUT`).
- **Phân quyền:** `RequireRole("seller", "admin")`
- **Request Body:** `UpdateProductVariantRequest`.
- **Dữ liệu trả về:** `ProductVariantDto` (Mã HTTP `200 OK`).

#### 13. `DELETE /api/v1/variants/{id}` — Xóa biến thể (R116_DELETE_VARIANT.md - CAT-11)
- **Mục đích:** Xóa mềm biến thể SKU (`IsDeleted = true`); tự động ngăn chặn nếu biến thể đang là `isDefault` duy nhất của sản phẩm.
- **Phân quyền:** `RequireRole("seller", "admin")`
- **Dữ liệu trả về:** `204 NoContent`.

---

## 🛠️ PHẦN 3: KIỂM CHỨNG KỸ THUẬT & QUY CHUẨN KIẾN TRÚC

1. **Tuân thủ Clean Architecture:**
   - Hoàn toàn **No-Repository pattern** bọc ngoài EF Core: Sử dụng trực tiếp `ICatalogDbContext` và DbSet trong tầng Application.
   - Trả về **Pure DTO**: Không bọc vỏ ngoài thừa thãi trong tầng nội bộ Service; API Gateway chịu trách nhiệm bọc envelope đồng nhất hệ thống.
2. **Quy chuẩn Lỗi RFC 9110:**
   - 100% Error message bằng **tiếng Anh Sentence case**.
   - 100% Error code theo định dạng **`SCREAMING_SNAKE_CASE`** (`VAL_INVALID_INPUT`, `CAT_PRODUCT_NOT_FOUND`, `AUTH_FORBIDDEN`...).
3. **Kết quả Build & Kiểm tra mã nguồn:**
   - Lệnh thực thi: `dotnet build Catalog.sln -c Release`
   - Kết quả: **`Build succeeded. 0 Warning(s), 0 Error(s)`**.

---

## 📌 PHẦN 4: 4 API CÒN LẠI THUỘC NHÓM `B_none` (CHƯA TRIỂN KHAI)

Các file nằm trong thư mục `B_none` là phân hệ Đánh giá / Review sản phẩm (chưa có controller, chỉ mới có entity bảng dữ liệu):
- `R111_GET_PRODUCTS_TOTALRATING.md` — `GET /api/v1/products/{id}/totalRating` (Tổng quan rating sao).
- `R112_GET_PRODUCTS_RATING.md` — `GET /api/v1/products/{id}/rating` (Danh sách bình luận, đánh giá).
- `R92_POST_ORDER_PRODUCTS_REVIEWS.md` — `POST /api/v1/orders/{orderId}/products/{productId}/reviews` (Tạo review theo đơn mua).
- `R94_PATCH_ORDER_PRODUCTS_REVIEWS.md` — `PATCH /api/v1/orders/{orderId}/products/{productId}/reviews` (Chỉnh sửa review).