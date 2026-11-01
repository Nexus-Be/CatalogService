# TÀI LIỆU ĐẶC TẢ HỢP ĐỒNG API (API CONTRACT SPECIFICATION) — CATALOG SERVICE

> **Tuân thủ quy chuẩn:** `.agents/skills/new-feature-endpoint/SKILL.md` và `.agents/rules/01_clean_architecture_layers.md` $\to$ `06_database_and_migrations.md`  
> **Dịch vụ:** Nexus Catalog Service (Quản lý Danh mục, Sản phẩm, Biến thể SKU, Hình ảnh)  
> **Cổng dịch vụ (HTTP Port):** `8002` (Docker / Kestrel Local)  
> **Cơ sở dữ liệu:** PostgreSQL Neon Cloud (`category_db` / `catalog`)  
> **Quy chuẩn định tuyến:** 100% danh từ số nhiều `/api/v1/<plural>`, trả về DTO thuần túy, Zero ResultFilter (Gateway tự động bọc envelope outer).  
> **Quy chuẩn lỗi:** RFC 9110 ProblemDetails (`application/problem+json`), mã lỗi `SCREAMING_SNAKE_CASE`, thông điệp tiếng Anh 100% `Sentence case`.  
> **Quy chuẩn Enum:** Lưu Database dạng `integer` (int32), tuần tự hóa REST JSON dạng `PascalCase` string.

---

## 📌 MỤC LỤC DANH SÁCH 30 ENDPOINTS

| STT | Phương thức | Endpoint Route | Controller & Action | Nhóm nghiệp vụ |
| :---: | :---: | :--- | :--- | :--- |
| **01** | `GET` | `/api/v1/categories` | `CategoriesController.List` | Phân hệ Danh mục |
| **02** | `GET` | `/api/v1/categories/{id}` | `CategoriesController.GetById` | Phân hệ Danh mục |
| **03** | `POST` | `/api/v1/categories` | `CategoriesController.Create` | Phân hệ Danh mục (CAT-15) |
| **04** | `PUT` | `/api/v1/categories/{id}` | `CategoriesController.Update` | Phân hệ Danh mục (CAT-16) |
| **05** | `PATCH` | `/api/v1/categories/{id}` | `CategoriesController.Update` (Alias) | Phân hệ Danh mục (CAT-16) |
| **06** | `DELETE` | `/api/v1/categories/{id}` | `CategoriesController.Delete` | Phân hệ Danh mục (CAT-17) |
| **07** | `GET` | `/api/v1/products` | `ProductsController.Search` | Phân hệ Sản phẩm (CAT-05) |
| **08** | `GET` | `/api/v1/products/{id}` | `ProductsController.GetDetail` | Phân hệ Sản phẩm (CAT-06) |
| **09** | `GET` | `/api/v1/seller/products` | `SellerProductsController.List` | Phân hệ Sản phẩm (CAT-44) |
| **10** | `GET` | `/api/v1/seller/products/{id}` | `SellerProductsController.GetDetail` | Phân hệ Sản phẩm (CAT-45) |
| **11** | `POST` | `/api/v1/products` | `ProductsController.Create` | Phân hệ Sản phẩm (CAT-01) |
| **12** | `POST` | `/api/v1/products/auction` | `ProductsController.CreateAuction` | Phân hệ Sản phẩm (CAT-43) |
| **13** | `PUT` | `/api/v1/products/{id}` | `ProductsController.Update` | Phân hệ Sản phẩm (CAT-02) |
| **14** | `PATCH` | `/api/v1/products/{id}` | `ProductsController.Update` (Alias) | Phân hệ Sản phẩm (CAT-02) |
| **15** | `PATCH` | `/api/v1/products/{id}/status` | `ProductsController.ChangeStatus` | Phân hệ Sản phẩm (CAT-03) |
| **16** | `DELETE` | `/api/v1/products/{id}` | `ProductsController.Delete` | Phân hệ Sản phẩm (CAT-04) |
| **17** | `GET` | `/api/v1/variants/{id}` | `VariantsController.GetVariantById` | Phân hệ Biến thể (CAT-42) |
| **18** | `POST` | `/api/v1/products/{id}/variants` | `ProductsController.AddVariant` | Phân hệ Biến thể (CAT-07) |
| **19** | `PUT` | `/api/v1/variants/{id}` | `VariantsController.UpdateVariant` | Phân hệ Biến thể (CAT-08) |
| **20** | `PATCH` | `/api/v1/variants/{id}` | `VariantsController.UpdateVariant` (Alias) | Phân hệ Biến thể (CAT-08) |
| **21** | `PUT` | `/api/v1/products/{id}/default-variant` | `ProductsController.SetDefaultVariant` | Phân hệ Biến thể (CAT-10) |
| **22** | `PATCH` | `/api/v1/variants/{id}/status` | `VariantsController.ChangeVariantStatus` | Phân hệ Biến thể (CAT-09) |
| **23** | `DELETE` | `/api/v1/variants/{id}` | `VariantsController.DeleteVariant` | Phân hệ Biến thể (CAT-11) |
| **24** | `GET` | `/api/v1/products/{id}/images` | `ProductsController.GetImages` | Phân hệ Hình ảnh (CAT-40) |
| **25** | `POST` | `/api/v1/products/{id}/images` | `ProductsController.UploadImage` | Phân hệ Hình ảnh (CAT-12) |
| **26** | `PATCH` | `/api/v1/images/{id}` | `ImagesController.UpdateImage` | Phân hệ Hình ảnh (CAT-14) |
| **27** | `DELETE` | `/api/v1/images/{id}` | `ImagesController.DeleteImage` | Phân hệ Hình ảnh (CAT-13) |
| **28** | `DELETE` | `/api/v1/products/{id}/images/{imageId}` | `ProductsController.DeleteImage` | Phân hệ Hình ảnh (Lồng) |
| **29** | `POST` | `/api/v1/internal/variants` | `InternalVariantsController.BatchGet` | Liên Service (Batch) |
| **30** | `POST` | `/api/v1/internal/variants/check-status` | `InternalVariantsController.CheckStatus` | Liên Service (Cart/Order) |

---

## 📁 PHẦN A: PHÂN HỆ DANH MỤC (CATEGORY MANAGEMENT)

---

### API 01. `GET /api/v1/categories` — Lấy danh sách danh mục

#### 1. Contract Handoff Block
| Thuộc tính (Attribute) | Giá trị đặc tả (Specification Value) |
| :--- | :--- |
| **Service** | `CatalogService` (Port 8002) |
| **Endpoint** | `GET /api/v1/categories` |
| **Controller** | `Catalog.Api.Controllers.Public.CategoriesController.List` |
| **Application Service** | `Catalog.Application.Services.Category.ICategoryService.GetCategoriesAsync` |
| **Primary Entity** | `Catalog.Domain.Entities.Category` (Bảng `Categories`) |
| **RBAC & Identity** | `Public` (Không yêu cầu đăng nhập) |
| **Enums Involved** | Không |
| **Audit Logging** | Không (Truy vấn đọc Read-only) |
| **Cross-service Call** | `None` (Xử lý 100% trong Catalog DB) |

#### 2. Validation Rules & Guardrails
* `parentId`: Đúng định dạng `Guid` nếu có truyền.
* `tree`: `true` hoặc `false`.

#### 3. Request Contract
* **Query Parameters:**
  * `parentId` (`Guid?`, Tùy chọn): Lọc danh mục con trực tiếp thuộc về danh mục cha này.
  * `tree` (`Boolean`, Mặc định `false`): `true` trả về cây phân cấp lồng nhau, `false` trả về danh sách phẳng.
  * `keyword` (`String?`, Tùy chọn): Từ khóa tìm kiếm theo tên.

#### 4. Response Contract
* **HTTP Status Code:** `200 OK`
* **Response Body (`IReadOnlyList<CategoryItemDto>`):**
  * `categoryId` (`Guid`): ID danh mục.
  * `parentId` (`Guid?`): ID danh mục cha.
  * `name` (`String`): Tên danh mục.
  * `description` (`String?`): Mô tả.
  * `level` (`Short`): Cấp độ cây (1, 2, 3).
  * `sortOrder` (`Integer`): Thứ tự hiển thị.
  * `children` (`Array of CategoryItemDto?`): Danh mục con trực thuộc (khi `tree = true`).
```json
[
  {
    "categoryId": "10000000-0000-0000-0000-000000000001",
    "parentId": null,
    "name": "Thời Trang",
    "description": "Quần áo thời trang nam nữ",
    "level": 1,
    "sortOrder": 1,
    "children": []
  }
]
```

#### 5. Processing Flow
1. Tiếp nhận query parameters từ client.
2. Truy vấn bảng `Categories` lọc `IsDeleted == false` và `IsDisabled == false` (`.AsNoTracking()`).
3. Nếu `tree == true`: xây dựng cây lồng nhau theo `ParentId`.
4. Trả về mảng `CategoryItemDto`.

---

### API 02. `GET /api/v1/categories/{id}` — Lấy chi tiết một danh mục

#### 1. Contract Handoff Block
| Thuộc tính (Attribute) | Giá trị đặc tả (Specification Value) |
| :--- | :--- |
| **Service** | `CatalogService` (Port 8002) |
| **Endpoint** | `GET /api/v1/categories/{id}` |
| **Controller** | `Catalog.Api.Controllers.Public.CategoriesController.GetById` |
| **Application Service** | `Catalog.Application.Services.Category.ICategoryService.GetByIdAsync` |
| **Primary Entity** | `Catalog.Domain.Entities.Category` (Bảng `Categories`) |
| **RBAC & Identity** | `Public` |
| **Cross-service Call** | `None` |

#### 2. Validation Rules & Guardrails
* `id`: Phải là GUID hợp lệ.

#### 3. Request Contract
* **Path Parameters:**
  * `id` (`Guid`, Bắt buộc): ID danh mục cần tra cứu.

#### 4. Response Contract
* **HTTP Status Code:** `200 OK`
* **Response Body (`CategoryDto`):**
  * `id` (`Guid`): ID danh mục.
  * `parentId` (`Guid?`): ID danh mục cha.
  * `name` (`String`): Tên danh mục.
  * `description` (`String?`): Mô tả.
  * `level` (`Short`): Cấp độ (1, 2, 3).
  * `sortOrder` (`Integer`): Thứ tự hiển thị.
  * `isDisabled` (`Boolean`): Cờ vô hiệu hóa.
* **Error Response (`404 Not Found`):**
```json
{
  "code": "CATEGORY_NOT_FOUND",
  "detail": "Category not found with ID '10000000-0000-0000-0000-000000000001'.",
  "status": 404,
  "title": "Not Found",
  "instance": "/api/v1/categories/10000000-0000-0000-0000-000000000001"
}
```

---

### API 03. `POST /api/v1/categories` — Tạo danh mục mới [CAT-15]

#### 1. Contract Handoff Block
| Thuộc tính (Attribute) | Giá trị đặc tả (Specification Value) |
| :--- | :--- |
| **Service** | `CatalogService` (Port 8002) |
| **Endpoint** | `POST /api/v1/categories` |
| **Controller** | `Catalog.Api.Controllers.Public.CategoriesController.Create` |
| **Application Service** | `Catalog.Application.Services.Category.ICategoryService.CreateAsync` |
| **Primary Entity** | `Catalog.Domain.Entities.Category` |
| **RBAC & Identity** | `[RequireRole("admin", "staff")]`. Identity trích xuất qua `this.GetUserId()` |
| **Audit Logging** | `_dbContext.AddAuditLog(AuditAction.CategoryCreate, ...)` |
| **Cross-service Call** | `None` |

#### 2. Validation Rules & Guardrails
* `name`: Bắt buộc, không để trống, $\le 255$ ký tự (`VAL_INVALID_INPUT`).
* `level`: Bắt buộc, giá trị từ 1 đến 3 (`VAL_INVALID_INPUT`).
* `parentId`: Nếu cung cấp, danh mục cha phải tồn tại (`CATEGORY_NOT_FOUND`) và có `Level == request.Level - 1`.

#### 3. Request Contract
* **Headers:** `Authorization` (`String`), `x-user-id` (`Guid`), `x-user-roles` (`String`: `admin`, `staff`).
* **Request Body (`CreateCategoryRequest`):**
  * `name` (`String`, Bắt buộc): Tên danh mục.
  * `description` (`String?`, Tùy chọn): Mô tả tóm tắt.
  * `parentId` (`Guid?`, Tùy chọn): ID danh mục cha.
  * `level` (`Short`, Mặc định `1`): Cấp độ (1, 2, 3).
  * `sortOrder` (`Integer`, Mặc định `0`): Thứ tự sắp xếp.
```json
{
  "name": "Áo Thun Nam",
  "description": "Các loại áo phông nam",
  "parentId": "10000000-0000-0000-0000-000000000002",
  "level": 3,
  "sortOrder": 1
}
```

#### 4. Response Contract
* **HTTP Status Code:** `201 Created`
* **Response Body (`CategoryDto`):** Trả về đối tượng danh mục vừa tạo.

---

### API 04. `PUT /api/v1/categories/{id}` — Cập nhật toàn bộ thông tin danh mục [CAT-16]

#### 1. Contract Handoff Block
| Thuộc tính (Attribute) | Giá trị đặc tả (Specification Value) |
| :--- | :--- |
| **Service** | `CatalogService` (Port 8002) |
| **Endpoint** | `PUT /api/v1/categories/{id}` |
| **Controller** | `Catalog.Api.Controllers.Public.CategoriesController.Update` |
| **Application Service** | `Catalog.Application.Services.Category.ICategoryService.UpdateAsync` |
| **Primary Entity** | `Catalog.Domain.Entities.Category` |
| **RBAC & Identity** | `[RequireRole("admin", "staff")]` |
| **Audit Logging** | `_dbContext.AddAuditLog(AuditAction.CategoryUpdate, ...)` |
| **Cross-service Call** | `None` |

#### 2. Validation Rules & Request / Response
* `id`: GUID danh mục phải tồn tại.
* `name`: Bắt buộc, không để trống, $\le 255$ ký tự.
* `Request Body (`UpdateCategoryRequest`):`
  * `name` (`String`, Bắt buộc).
  * `description` (`String?`, Tùy chọn).
  * `level` (`Short`, Mặc định `1`).
  * `sortOrder` (`Integer`, Mặc định `0`).
  * `isDisabled` (`Boolean`, Mặc định `false`).
* **HTTP Status Code:** `200 OK` trả về `CategoryDto`.

---

### API 05. `PATCH /api/v1/categories/{id}` — Cập nhật danh mục (Bí danh PATCH) [CAT-16]

#### 1. Contract Handoff Block
| Thuộc tính (Attribute) | Giá trị đặc tả (Specification Value) |
| :--- | :--- |
| **Service** | `CatalogService` (Port 8002) |
| **Endpoint** | `PATCH /api/v1/categories/{id}` |
| **Controller** | `Catalog.Api.Controllers.Public.CategoriesController.Update` (Alias) |
| **Application Service** | `Catalog.Application.Services.Category.ICategoryService.UpdateAsync` |
| **Cross-service Call** | `None` |

*Quy cách Request/Response và Validation giống hoàn toàn API 04.*

---

### API 06. `DELETE /api/v1/categories/{id}` — Xóa mềm danh mục [CAT-17]

#### 1. Contract Handoff Block
| Thuộc tính (Attribute) | Giá trị đặc tả (Specification Value) |
| :--- | :--- |
| **Service** | `CatalogService` (Port 8002) |
| **Endpoint** | `DELETE /api/v1/categories/{id}` |
| **Controller** | `Catalog.Api.Controllers.Public.CategoriesController.Delete` |
| **Application Service** | `Catalog.Application.Services.Category.ICategoryService.DeleteAsync` |
| **Primary Entity** | `Catalog.Domain.Entities.Category` |
| **RBAC & Identity** | `[RequireRole("admin", "staff")]` |
| **Audit Logging** | `_dbContext.AddAuditLog(AuditAction.CategoryDelete, ...)` |
| **Cross-service Call** | `None` |

#### 2. Validation Rules & Guardrails
* Danh mục phải tồn tại (`CATEGORY_NOT_FOUND`).
* Không cho phép xóa nếu danh mục đang có danh mục con bên dưới (`VAL_INVALID_INPUT`).

#### 3. Response Contract
* **HTTP Status Code:** `204 NoContent` (Không có body).

---

## 📦 PHẦN B: PHÂN HỆ SẢN PHẨM (PRODUCT MANAGEMENT)

---

### API 07. `GET /api/v1/products` — Tìm kiếm và lọc sản phẩm công khai [CAT-05]

#### 1. Contract Handoff Block
| Thuộc tính (Attribute) | Giá trị đặc tả (Specification Value) |
| :--- | :--- |
| **Service** | `CatalogService` (Port 8002) |
| **Endpoint** | `GET /api/v1/products` |
| **Controller** | `Catalog.Api.Controllers.Public.ProductsController.Search` |
| **Application Service** | `Catalog.Application.Services.Product.IProductService.SearchAsync` |
| **Primary Entity** | `Catalog.Domain.Entities.Product` |
| **RBAC & Identity** | `Public` |
| **Enums Involved** | `ProductStatus` (Active = 1), `SaleType` (Regular = 0, Auction = 1) |
| **Cross-service Call** | `None` |

#### 2. Validation Rules & Parameters
* `page` (`Integer`, $\ge 1$, Mặc định `1`), `pageSize` (`Integer`, $1 \le pageSize \le 100$, Mặc định `20`).
* `minPrice`, `maxPrice` (`Decimal?`, $\ge 0$).
* `sortBy` (`String?`): `price_asc`, `price_desc`, `rating_desc`, `newest`.

#### 3. Response Contract
* **HTTP Status Code:** `200 OK`
* **Response Body (`PagedData<ProductListItemDto>`):**
  * `items` (`Array of ProductListItemDto`):
    * `productId` (`Guid`), `name` (`String`), `brand` (`String?`), `saleType` (`String`), `status` (`String`), `minPrice` (`Decimal?`), `maxPrice` (`Decimal?`), `primaryImageUrl` (`String?`), `avgRating` (`Decimal?`).
  * `page` (`Integer`), `pageSize` (`Integer`), `totalItems` (`Integer`), `totalPages` (`Integer`).

---

### API 08. `GET /api/v1/products/{id}` — Xem chi tiết sản phẩm công khai [CAT-06]

#### 1. Contract Handoff Block
| Thuộc tính (Attribute) | Giá trị đặc tả (Specification Value) |
| :--- | :--- |
| **Service** | `CatalogService` (Port 8002) |
| **Endpoint** | `GET /api/v1/products/{id}` |
| **Controller** | `Catalog.Api.Controllers.Public.ProductsController.GetDetail` |
| **Application Service** | `Catalog.Application.Services.Product.IProductService.GetDetailAsync` |
| **Primary Entity** | `Catalog.Domain.Entities.Product` |
| **RBAC & Identity** | `Public` |
| **Cross-service Call** | `None` |

#### 2. Response Contract
* **HTTP Status Code:** `200 OK`
* **Response Body (`ProductDetailDto`):**
  * Chứa thông tin sản phẩm, danh mục, mảng `Variants` (SKU, giá, thuộc tính), mảng `Images` và `Rating`.

---

### API 09. `GET /api/v1/seller/products` — Lấy danh sách sản phẩm của Seller [CAT-44]

#### 1. Contract Handoff Block
| Thuộc tính (Attribute) | Giá trị đặc tả (Specification Value) |
| :--- | :--- |
| **Service** | `CatalogService` (Port 8002) |
| **Endpoint** | `GET /api/v1/seller/products` |
| **Controller** | `Catalog.Api.Controllers.Public.SellerProductsController.List` |
| **Application Service** | `Catalog.Application.Services.Product.IProductService.SearchSellerProductsAsync` |
| **Primary Entity** | `Catalog.Domain.Entities.Product` |
| **RBAC & Identity** | `[RequireRole("seller", "admin")]`. `SellerId` bắt buộc trích xuất từ `this.TryGetUserId()` |
| **Cross-service Call** | `None` |

#### 2. Request & Response
* **Headers:** `x-user-id` (`Guid`, Bắt buộc).
* **Query Parameters:** `keyword` (`String?`), `status` (`String?`), `page` (`Integer`), `pageSize` (`Integer`).
* **HTTP Status Code:** `200 OK` trả về `PagedData<SellerProductListItemDto>` kèm `variantCount`, `hasOrder`.

---

### API 10. `GET /api/v1/seller/products/{id}` — Chi tiết sản phẩm dành cho Seller [CAT-45]

#### 1. Contract Handoff Block
| Thuộc tính (Attribute) | Giá trị đặc tả (Specification Value) |
| :--- | :--- |
| **Service** | `CatalogService` (Port 8002) |
| **Endpoint** | `GET /api/v1/seller/products/{id}` |
| **Controller** | `Catalog.Api.Controllers.Public.SellerProductsController.GetDetail` |
| **Application Service** | `Catalog.Application.Services.Product.IProductService.GetSellerProductDetailAsync` |
| **RBAC & Identity** | `[RequireRole("seller", "admin")]`. Chặn truy cập nếu `product.SellerId != currentUserId` (HTTP 403 `PERMISSION_DENIED`) |
| **Cross-service Call** | `None` |

#### 2. Response Contract
* **HTTP Status Code:** `200 OK` trả về `SellerProductDetailDto` (đầy đủ tất cả SKU active/inactive, lịch sử đơn hàng, và ảnh).

---

### API 11. `POST /api/v1/products` — Tạo mới sản phẩm thường (REGULAR) [CAT-01]

#### 1. Contract Handoff Block
| Thuộc tính (Attribute) | Giá trị đặc tả (Specification Value) |
| :--- | :--- |
| **Service** | `CatalogService` (Port 8002) |
| **Endpoint** | `POST /api/v1/products` |
| **Controller** | `Catalog.Api.Controllers.Public.ProductsController.Create` |
| **Application Service** | `Catalog.Application.Services.Product.IProductService.CreateAsync` |
| **Primary Entity** | `Catalog.Domain.Entities.Product`, `ProductVariant` |
| **RBAC & Identity** | `[RequireRole("seller", "admin")]`. `SellerId` lấy từ `HttpContext` (`x-user-id`), cấm client tự truyền trong body |
| **Enums Involved** | `ProductStatus` (Draft = 0), `SaleType` (Regular = 0), `VariantStatus` (Active = 1) |
| **Audit Logging** | `_dbContext.AddAuditLog(AuditAction.ProductCreate, ...)` |
| **Cross-service Call** | **CÓ** $\to$ `FulfillmentService` (`POST /api/v1/inventories/initialize`) |

#### 2. Validation Rules & Guardrails
* `name`: Bắt buộc, $1 \le \text{length} \le 255$ (`VAL_INVALID_INPUT`).
* `brand`: Tối đa 150 ký tự; `description`: Tối đa 4000 ký tự.
* `categoryId`: Bắt buộc là **danh mục lá (Leaf Category)**, cấm chọn danh mục cha (`VAL_INVALID_INPUT`).
* **Hỗ trợ Hybrid:** Nếu có gửi mảng `variants`:
  * Tối đa 10 biến thể (`MAX_VARIANTS_REACHED`).
  * `skuCode` không được trùng lặp trong nội bộ gian hàng của Seller (`409 DUPLICATE_SKU`).
  * `price` của từng biến thể phải $> 0$.
  * Toàn bộ Product và Variants được tạo trong **1 Database Transaction nguyên tử**.

#### 3. Request Contract
* **Headers:** `Authorization` (`String`), `x-user-id` (`Guid`), `x-user-roles` (`String`).
* **Request Body (`CreateProductRequest`):**
  * `name` (`String`, Bắt buộc).
  * `categoryId` (`Guid`, Bắt buộc).
  * `brand` (`String?`, Tùy chọn).
  * `description` (`String?`, Tùy chọn).
  * `saleType` (`String`, Mặc định `Regular`).
  * `specifications` (`String?`, Tùy chọn).
  * `variants` (`Array of CreateVariantRequest?`, Tùy chọn, tối đa 10 items):
    * `skuCode` (`String`, Bắt buộc).
    * `name` (`String`, Bắt buộc).
    * `price` (`Decimal`, Bắt buộc, $> 0$).
    * `currency` (`String?`, Mặc định `VND`).
    * `attributes` (`Array of Object?`): `attrName` (`String`), `attrValue` (`String`).
```json
{
  "name": "Áo Khoác Gió Nam Nexus",
  "categoryId": "10000000-0000-0000-0000-000000000003",
  "brand": "Nexus",
  "description": "Áo gió chống nước 2 lớp",
  "saleType": "Regular",
  "variants": [
    {
      "skuCode": "AKG-XANH-L",
      "name": "Xanh Navy - L",
      "price": 320000,
      "currency": "VND",
      "attributes": [
        { "attrName": "Color", "attrValue": "Xanh Navy" },
        { "attrName": "Size", "attrValue": "L" }
      ]
    }
  ]
}
```

#### 4. Response Contract
* **HTTP Status Code:** `201 Created`
* **Response Body (`ProductDto`):**
  * `id` (`Guid`), `sellerId` (`Guid`), `categoryId` (`Guid`), `name` (`String`), `description` (`String?`), `brand` (`String?`), `status` (`String`: `"Draft"`).

#### 5. Cross-service Integration Detail
* **Service gọi đến:** `FulfillmentService` (`http://localhost:8006/api/v1/inventories/initialize`).
* **Payload:** `{ "productId": "...", "skuId": null, "initialQuantity": 0 }`.
* **Cơ chế chịu lỗi:** Bọc trong `try-catch` non-fatal. Nếu Fulfillment Service mất kết nối hoặc trả về lỗi, hệ thống chỉ ghi warning log và vẫn hoàn thành việc tạo Product trong Catalog DB.

---

### API 12. `POST /api/v1/products/auction` — Tạo mới sản phẩm Đấu giá [CAT-43]

#### 1. Contract Handoff Block
| Thuộc tính (Attribute) | Giá trị đặc tả (Specification Value) |
| :--- | :--- |
| **Service** | `CatalogService` (Port 8002) |
| **Endpoint** | `POST /api/v1/products/auction` |
| **Controller** | `Catalog.Api.Controllers.Public.ProductsController.CreateAuction` |
| **Application Service** | `Catalog.Application.Services.Product.IProductService.CreateAuctionProductAsync` |
| **Primary Entity** | `Catalog.Domain.Entities.Product`, `ProductVariant` |
| **RBAC & Identity** | `[RequireRole("seller", "admin")]` |
| **Enums Involved** | `SaleType` (Auction = 1), `ProductStatus` (Draft = 0) |
| **Audit Logging** | `_dbContext.AddAuditLog(AuditAction.ProductCreate, ...)` |
| **Cross-service Call** | **CÓ** $\to$ `FulfillmentService` (`POST /api/v1/inventories/initialize`) |

#### 2. Validation Rules & Request / Response
* Không nhận mảng `variants` từ bên ngoài. Tự động sinh 1 SKU duy nhất với `price = null`.
* **Request Body (`CreateAuctionProductRequest`):** `name` (`String`), `categoryId` (`Guid`), `brand` (`String?`), `description` (`String?`), `specifications` (`String?`).
* **HTTP Status Code:** `201 Created` trả về `ProductDto` (`Status = "Draft"`).

---

### API 13. `PUT /api/v1/products/{id}` — Cập nhật thông tin cơ bản sản phẩm [CAT-02]

#### 1. Contract Handoff Block
| Thuộc tính (Attribute) | Giá trị đặc tả (Specification Value) |
| :--- | :--- |
| **Service** | `CatalogService` (Port 8002) |
| **Endpoint** | `PUT /api/v1/products/{id}` |
| **Controller** | `Catalog.Api.Controllers.Public.ProductsController.Update` |
| **Application Service** | `Catalog.Application.Services.Product.IProductService.UpdateAsync` |
| **RBAC & Identity** | `[RequireRole("seller", "admin")]`. Kiểm tra `product.SellerId == currentUserId` |
| **Audit Logging** | `_dbContext.AddAuditLog(AuditAction.ProductUpdate, ...)` |
| **Cross-service Call** | `None` |

#### 2. Request & Response
* **Path Parameters:** `id` (`Guid`, Bắt buộc).
* **Request Body (`UpdateProductRequest`):** `name` (`String`), `categoryId` (`Guid`), `brand` (`String?`), `description` (`String?`), `specifications` (`String?`).
* **HTTP Status Code:** `200 OK` trả về `ProductDto`.

---

### API 14. `PATCH /api/v1/products/{id}` — Cập nhật thông tin sản phẩm (Bí danh PATCH) [CAT-02]

#### 1. Contract Handoff Block
| Thuộc tính (Attribute) | Giá trị đặc tả (Specification Value) |
| :--- | :--- |
| **Endpoint** | `PATCH /api/v1/products/{id}` |
| **Controller** | `Catalog.Api.Controllers.Public.ProductsController.Update` (Alias) |
| **Cross-service Call** | `None` |

*Quy cách Request/Response và Validation giống hoàn toàn API 13.*

---

### API 15. `PATCH /api/v1/products/{id}/status` — Đổi trạng thái sản phẩm [CAT-03]

#### 1. Contract Handoff Block
| Thuộc tính (Attribute) | Giá trị đặc tả (Specification Value) |
| :--- | :--- |
| **Service** | `CatalogService` (Port 8002) |
| **Endpoint** | `PATCH /api/v1/products/{id}/status` |
| **Controller** | `Catalog.Api.Controllers.Public.ProductsController.ChangeStatus` |
| **Application Service** | `Catalog.Application.Services.Product.IProductService.ChangeStatusAsync` |
| **Primary Entity** | `Catalog.Domain.Entities.Product` |
| **RBAC & Identity** | `[RequireRole("seller", "admin")]`. Kiểm tra sở hữu seller |
| **Enums Involved** | `ProductStatus` (Draft = 0, Active = 1, Inactive = 2), `ProductInactiveReason` (SellerPaused = 0, AdminLocked = 1, OutOfStock = 2, Violation = 3) |
| **Audit Logging** | `_dbContext.AddAuditLog(AuditAction.ProductUpdate, ...)` |
| **Cross-service Call** | `None` |

#### 2. Validation Rules & Guardrails Bắt buộc
* Để chuyển sang `ACTIVE`, hệ thống bắt buộc kiểm tra 2 điều kiện:
  1. Phải có ít nhất **1 biến thể đang `ACTIVE`** và có giá bán $> 0$ (sản phẩm thường).
  2. Phải có ít nhất **1 hình ảnh sản phẩm đã upload** và mang cờ đại diện `IsActive = true`.
  $\implies$ Vi phạm điều kiện: trả về lỗi `400 Bad Request` (`VAL_INVALID_INPUT`).

#### 3. Request & Response
* **Request Body (`ChangeProductStatusRequest`):** `status` (`String`, Bắt buộc: `DRAFT`, `ACTIVE`, `INACTIVE`), `statusReason` (`String?`, Tùy chọn).
* **HTTP Status Code:** `200 OK` trả về `ProductDto` kèm thời gian `publishedAt = UtcNow`.

---

### API 16. `DELETE /api/v1/products/{id}` — Xóa mềm sản phẩm [CAT-04]

#### 1. Contract Handoff Block
| Thuộc tính (Attribute) | Giá trị đặc tả (Specification Value) |
| :--- | :--- |
| **Service** | `CatalogService` (Port 8002) |
| **Endpoint** | `DELETE /api/v1/products/{id}` |
| **Controller** | `Catalog.Api.Controllers.Public.ProductsController.Delete` |
| **Application Service** | `Catalog.Application.Services.Product.IProductService.SoftDeleteAsync` |
| **Primary Entity** | `Catalog.Domain.Entities.Product`, `ProductVariant` |
| **RBAC & Identity** | `[RequireRole("seller", "admin")]` |
| **Audit Logging** | `_dbContext.AddAuditLog(AuditAction.ProductDelete, ...)` |
| **Cross-service Call** | `None` |

#### 2. Response Contract
* **HTTP Status Code:** `204 NoContent` (Xóa mềm cả Product và toàn bộ Variants con trong DB).

---

## 🏷️ PHẦN C: PHÂN HỆ BIẾN THỂ / SKU (VARIANT MANAGEMENT)

---

### API 17. `GET /api/v1/variants/{id}` — Xem chi tiết một biến thể SKU [CAT-42]

#### 1. Contract Handoff Block
| Thuộc tính (Attribute) | Giá trị đặc tả (Specification Value) |
| :--- | :--- |
| **Service** | `CatalogService` (Port 8002) |
| **Endpoint** | `GET /api/v1/variants/{id}` |
| **Controller** | `Catalog.Api.Controllers.Public.VariantsController.GetVariantById` |
| **Application Service** | `Catalog.Application.Services.Product.IProductService.GetVariantByIdAsync` |
| **Primary Entity** | `Catalog.Domain.Entities.ProductVariant` |
| **RBAC & Identity** | `Public` / `Seller` |
| **Cross-service Call** | `None` |

#### 2. Response Contract
* **HTTP Status Code:** `200 OK`
* **Response Body (`VariantDetailDto`):**
  * `id` (`Guid`), `productId` (`Guid`), `skuCode` (`String`), `name` (`String`), `price` (`Decimal?`), `currency` (`String`), `isActive` (`Boolean`), `hasOrder` (`Boolean`), `status` (`String`), `statusReason` (`String?`), `attributes` (`Array of ProductVariantAttributeDto`).

---

### API 18. `POST /api/v1/products/{id}/variants` — Thêm biến thể cho sản phẩm [CAT-07]

#### 1. Contract Handoff Block
| Thuộc tính (Attribute) | Giá trị đặc tả (Specification Value) |
| :--- | :--- |
| **Service** | `CatalogService` (Port 8002) |
| **Endpoint** | `POST /api/v1/products/{id}/variants` |
| **Controller** | `Catalog.Api.Controllers.Public.ProductsController.AddVariant` |
| **Application Service** | `Catalog.Application.Services.Product.IProductService.AddVariantAsync` |
| **Primary Entity** | `Catalog.Domain.Entities.ProductVariant` |
| **RBAC & Identity** | `[RequireRole("seller", "admin")]` |
| **Audit Logging** | `_dbContext.AddAuditLog(AuditAction.VariantCreate, ...)` |
| **Cross-service Call** | `None` |

#### 2. Validation Rules & Guardrails
* Cấm thêm biến thể cho sản phẩm Đấu giá (`AUCTION`) (`VAL_INVALID_INPUT`).
* Tối đa 10 biến thể cho 1 sản phẩm (`MAX_VARIANTS_REACHED`).
* `skuCode` không được trùng với SKU đã có trong shop của Seller (`409 DUPLICATE_SKU`).
* `price` phải $> 0$.

#### 3. Request & Response
* **Path Parameters:** `id` (`Guid`, ID sản phẩm).
* **Request Body (`CreateVariantRequest`):** `skuCode` (`String`), `name` (`String`), `price` (`Decimal`), `currency` (`String?`), `attributes` (`Array of Object?`).
* **HTTP Status Code:** `201 Created` trả về `VariantDetailDto`.

---

### API 19. `PUT /api/v1/variants/{id}` — Cập nhật thông tin biến thể [CAT-08]

#### 1. Contract Handoff Block
| Thuộc tính (Attribute) | Giá trị đặc tả (Specification Value) |
| :--- | :--- |
| **Service** | `CatalogService` (Port 8002) |
| **Endpoint** | `PUT /api/v1/variants/{id}` |
| **Controller** | `Catalog.Api.Controllers.Public.VariantsController.UpdateVariant` |
| **Application Service** | `Catalog.Application.Services.Product.IProductService.UpdateVariantAsync` |
| **RBAC & Identity** | `[RequireRole("seller", "admin")]` |
| **Audit Logging** | `_dbContext.AddAuditLog(AuditAction.VariantUpdate, ...)` |
| **Cross-service Call** | `None` |

#### 2. Request & Response
* **Path Parameters:** `id` (`Guid`, ID biến thể).
* **Request Body (`UpdateVariantRequest`):** `name` (`String`), `price` (`Decimal`), `currency` (`String?`), `attributes` (`Array of Object?`).
* **HTTP Status Code:** `200 OK` trả về `VariantDetailDto`.

---

### API 20. `PATCH /api/v1/variants/{id}` — Cập nhật biến thể (Bí danh PATCH) [CAT-08]

#### 1. Contract Handoff Block
| Thuộc tính (Attribute) | Giá trị đặc tả (Specification Value) |
| :--- | :--- |
| **Endpoint** | `PATCH /api/v1/variants/{id}` |
| **Controller** | `Catalog.Api.Controllers.Public.VariantsController.UpdateVariant` (Alias) |
| **Cross-service Call** | `None` |

*Quy cách Request/Response và Validation giống hoàn toàn API 19.*

---

### API 21. `PUT /api/v1/products/{id}/default-variant` — Đặt biến thể mặc định [CAT-10]

#### 1. Contract Handoff Block
| Thuộc tính (Attribute) | Giá trị đặc tả (Specification Value) |
| :--- | :--- |
| **Service** | `CatalogService` (Port 8002) |
| **Endpoint** | `PUT /api/v1/products/{id}/default-variant` |
| **Controller** | `Catalog.Api.Controllers.Public.ProductsController.SetDefaultVariant` |
| **Application Service** | `Catalog.Application.Services.Product.IProductService.SetDefaultVariantAsync` |
| **RBAC & Identity** | `[RequireRole("seller", "admin")]` |
| **Audit Logging** | `_dbContext.AddAuditLog(AuditAction.VariantUpdate, ...)` |
| **Cross-service Call** | `None` |

#### 2. Request & Response
* **Path Parameters:** `id` (`Guid`, ID sản phẩm).
* **Request Body (`SetDefaultVariantRequest`):** `variantId` (`Guid`, Bắt buộc).
* **HTTP Status Code:** `200 OK` (Gán `isActive = true` cho SKU này và `isActive = false` cho tất cả SKU khác).

---

### API 22. `PATCH /api/v1/variants/{id}/status` — Đổi trạng thái biến thể [CAT-09]

#### 1. Contract Handoff Block
| Thuộc tính (Attribute) | Giá trị đặc tả (Specification Value) |
| :--- | :--- |
| **Service** | `CatalogService` (Port 8002) |
| **Endpoint** | `PATCH /api/v1/variants/{id}/status` |
| **Controller** | `Catalog.Api.Controllers.Public.VariantsController.ChangeVariantStatus` |
| **Application Service** | `Catalog.Application.Services.Product.IProductService.ChangeVariantStatusAsync` |
| **RBAC & Identity** | `[RequireRole("seller", "admin")]` |
| **Enums Involved** | `VariantStatus` (Active = 1, Inactive = 2) |
| **Audit Logging** | `_dbContext.AddAuditLog(AuditAction.VariantUpdate, ...)` |
| **Cross-service Call** | `None` |

#### 2. Request & Response
* **Request Body (`ChangeVariantStatusRequest`):** `status` (`String`: `ACTIVE`, `INACTIVE`), `statusReason` (`String?`).
* **HTTP Status Code:** `200 OK` trả về `VariantDetailDto`.

---

### API 23. `DELETE /api/v1/variants/{id}` — Xóa mềm biến thể [CAT-11]

#### 1. Contract Handoff Block
| Thuộc tính (Attribute) | Giá trị đặc tả (Specification Value) |
| :--- | :--- |
| **Service** | `CatalogService` (Port 8002) |
| **Endpoint** | `DELETE /api/v1/variants/{id}` |
| **Controller** | `Catalog.Api.Controllers.Public.VariantsController.DeleteVariant` |
| **Application Service** | `Catalog.Application.Services.Product.IProductService.SoftDeleteVariantAsync` |
| **Primary Entity** | `Catalog.Domain.Entities.ProductVariant` |
| **RBAC & Identity** | `[RequireRole("seller", "admin")]` |
| **Audit Logging** | `_dbContext.AddAuditLog(AuditAction.VariantDelete, ...)` |
| **Cross-service Call** | `None` |

#### 2. Validation Rules & Guardrails
* **Chặn xóa khi đã có đơn hàng:** Nếu biến thể mang cờ `hasOrder == true`, hệ thống từ chối xóa và bắt buộc Seller phải chuyển sang `INACTIVE` (`VAL_INVALID_INPUT`).
* **Chặn xóa biến thể cuối cùng** của sản phẩm (`VAL_INVALID_INPUT`).

#### 3. Response Contract
* **HTTP Status Code:** `204 NoContent`.

---

## 🖼️ PHẦN D: PHÂN HỆ HÌNH ẢNH SẢN PHẨM (PRODUCT IMAGES)

---

### API 24. `GET /api/v1/products/{id}/images` — Lấy danh sách ảnh sản phẩm [CAT-40]

#### 1. Contract Handoff Block
| Thuộc tính (Attribute) | Giá trị đặc tả (Specification Value) |
| :--- | :--- |
| **Service** | `CatalogService` (Port 8002) |
| **Endpoint** | `GET /api/v1/products/{id}/images` |
| **Controller** | `Catalog.Api.Controllers.Public.ProductsController.GetImages` |
| **Application Service** | `Catalog.Application.Services.Product.IProductService.GetProductImagesAsync` |
| **Primary Entity** | `Catalog.Domain.Entities.ProductImage` |
| **RBAC & Identity** | `Public` / `Seller` |
| **Cross-service Call** | `None` |

#### 2. Response Contract
* **HTTP Status Code:** `200 OK`
* **Response Body (`IReadOnlyList<ProductImageDto>`):**
  * `id` (`Guid`), `productId` (`Guid`), `variantId` (`Guid?`), `url` (`String`), `altText` (`String?`), `isActive` (`Boolean`), `sortOrder` (`Short`).

---

### API 25. `POST /api/v1/products/{id}/images` — Tải lên hình ảnh sản phẩm [CAT-12]

#### 1. Contract Handoff Block
| Thuộc tính (Attribute) | Giá trị đặc tả (Specification Value) |
| :--- | :--- |
| **Service** | `CatalogService` (Port 8002) |
| **Endpoint** | `POST /api/v1/products/{id}/images` |
| **Controller** | `Catalog.Api.Controllers.Public.ProductsController.UploadImage` |
| **Application Service** | `Catalog.Application.Services.Product.IProductService.AddProductImageAsync` |
| **Primary Entity** | `Catalog.Domain.Entities.ProductImage` |
| **RBAC & Identity** | `[RequireRole("seller", "admin")]` |
| **Cross-service Call** | **CÓ** $\to$ `Cloudinary Media API` (`IImageStorageService.UploadAsync`) |

#### 2. Validation Rules & Guardrails
* File ảnh bắt buộc $> 0$ bytes.
* Tối đa **5 hình ảnh** cho mỗi sản phẩm (`VAL_LIMIT_EXCEEDED`).
* Ảnh đầu tiên tải lên tự động được gán làm ảnh đại diện chính (`IsActive = true`).

#### 3. Request Contract
* **Content-Type:** `multipart/form-data`
* **Form Data:**
  * `file` (`IFormFile`, Bắt buộc): File nhị phân ảnh.
  * `variantId` (`Guid?`, Tùy chọn): ID biến thể liên kết riêng.
  * `altText` (`String?`, Tùy chọn): Mô tả ảnh.
  * `sortOrder` (`Short`, Mặc định `0`): Thứ tự hiển thị.

#### 4. Response Contract
* **HTTP Status Code:** `201 Created` trả về `ProductImageDto` kèm link CDN Cloudinary.

#### 5. Cross-service Integration Detail
* Gọi SDK Cloudinary đẩy file stream lên đám mây. Nếu lỗi kết nối hoặc sai credentials, ngoại lệ được ném ra và dừng toàn bộ luồng, không ghi bản ghi rỗng vào database.

---

### API 26. `PATCH /api/v1/images/{id}` — Cập nhật ảnh đại diện chính & Thứ tự [CAT-14]

#### 1. Contract Handoff Block
| Thuộc tính (Attribute) | Giá trị đặc tả (Specification Value) |
| :--- | :--- |
| **Service** | `CatalogService` (Port 8002) |
| **Endpoint** | `PATCH /api/v1/images/{id}` |
| **Controller** | `Catalog.Api.Controllers.Public.ImagesController.UpdateImage` |
| **Application Service** | `Catalog.Application.Services.Product.IProductService.UpdateImageAsync` |
| **Primary Entity** | `Catalog.Domain.Entities.ProductImage` |
| **RBAC & Identity** | `[RequireRole("seller", "admin")]` |
| **Cross-service Call** | `None` |

#### 2. Validation Rules & Quy tắc Cốt lõi
* Khi đặt `isActive = true` cho ảnh này, hệ thống **tự động gỡ cờ `isActive = false` ở toàn bộ các ảnh khác** của sản phẩm đó (Đảm bảo mỗi sản phẩm luôn chỉ có **duy nhất 1 ảnh đại diện chính**).

#### 3. Request & Response
* **Path Parameters:** `id` (`Guid`, ID ảnh).
* **Request Body (`UpdateProductImageRequest`):** `isActive` (`Boolean?`), `sortOrder` (`Short?`), `altText` (`String?`).
* **HTTP Status Code:** `200 OK` trả về `ProductImageDto`.

---

### API 27. `DELETE /api/v1/images/{id}` — Xóa ảnh sản phẩm theo ID ảnh [CAT-13]

#### 1. Contract Handoff Block
| Thuộc tính (Attribute) | Giá trị đặc tả (Specification Value) |
| :--- | :--- |
| **Service** | `CatalogService` (Port 8002) |
| **Endpoint** | `DELETE /api/v1/images/{id}` |
| **Controller** | `Catalog.Api.Controllers.Public.ImagesController.DeleteImage` |
| **Application Service** | `Catalog.Application.Services.Product.IProductService.DeleteImageByIdAsync` |
| **Primary Entity** | `Catalog.Domain.Entities.ProductImage` |
| **RBAC & Identity** | `[RequireRole("seller", "admin")]` |
| **Cross-service Call** | `None` |

#### 2. Validation Rules & Quy tắc Kế thừa Đại diện
* Nếu xóa đúng ảnh đang là đại diện chính (`IsActive == true`), hệ thống **tự động tìm ảnh kế tiếp có `SortOrder` nhỏ nhất** của sản phẩm đó để gán `IsActive = true`, ngăn ngừa việc sản phẩm bị khuyết ảnh đại diện.

#### 3. Response Contract
* **HTTP Status Code:** `204 NoContent`.

---

### API 28. `DELETE /api/v1/products/{id}/images/{imageId}` — Xóa ảnh (Route lồng Product)

#### 1. Contract Handoff Block
| Thuộc tính (Attribute) | Giá trị đặc tả (Specification Value) |
| :--- | :--- |
| **Service** | `CatalogService` (Port 8002) |
| **Endpoint** | `DELETE /api/v1/products/{id}/images/{imageId}` |
| **Controller** | `Catalog.Api.Controllers.Public.ProductsController.DeleteImage` |
| **Application Service** | `Catalog.Application.Services.Product.IProductService.DeleteProductImageAsync` |
| **Cross-service Call** | `None` |

*Quy cách Request/Response và quy tắc chuyển giao đại diện giống hoàn toàn API 27.*

---

## 🔗 PHẦN E: PHÂN HỆ TÍCH HỢP LIÊN SERVICE (INTERNAL APIS)

---

### API 29. `POST /api/v1/internal/variants` — Lấy danh sách biến thể hàng loạt

#### 1. Contract Handoff Block
| Thuộc tính (Attribute) | Giá trị đặc tả (Specification Value) |
| :--- | :--- |
| **Service** | `CatalogService` (Port 8002) |
| **Endpoint** | `POST /api/v1/internal/variants` |
| **Controller** | `Catalog.Api.Controllers.Internal.InternalVariantsController.BatchGet` |
| **Application Service** | `Catalog.Application.Services.Product.IProductService.GetVariantsBatchAsync` |
| **Primary Entity** | `Catalog.Domain.Entities.ProductVariant` |
| **RBAC & Identity** | `Internal` (Gọi giữa các microservices: Order, Cart, Auction) |
| **Cross-service Call** | `None` (Đóng vai trò Server cung cấp dữ liệu cho service khác) |

#### 2. Request & Response
* **Request Body (`BatchGetVariantsRequest`):**
  * `variantIds` (`Array of Guid`, Bắt buộc, $1 \le \text{count} \le 100$).
* **HTTP Status Code:** `200 OK`
* **Response Body (`BatchVariantResponse`):**
  * `items` (`Array of BatchVariantItemDto`): Thông tin chi tiết mã SKU, tên, giá bán, tiền tệ, trạng thái, ảnh chính, thuộc tính.
  * `notFoundIds` (`Array of Guid`): Danh sách ID không tìm thấy trong hệ thống.

---

### API 30. `POST /api/v1/internal/variants/check-status` — Kiểm tra tính hợp lệ của biến thể

#### 1. Contract Handoff Block
| Thuộc tính (Attribute) | Giá trị đặc tả (Specification Value) |
| :--- | :--- |
| **Service** | `CatalogService` (Port 8002) |
| **Endpoint** | `POST /api/v1/internal/variants/check-status` |
| **Controller** | `Catalog.Api.Controllers.Internal.InternalVariantsController.CheckStatus` |
| **Application Service** | `Catalog.Application.Services.Product.IProductService.CheckVariantStatusBatchAsync` |
| **Primary Entity** | `Catalog.Domain.Entities.ProductVariant`, `Product` |
| **RBAC & Identity** | `Internal` |
| **Cross-service Call** | `None` |

#### 2. Validation & Quy tắc Mua sắm
* `isPurchasable = true` khi và chỉ khi: Product và Variant đều `Active`, chưa bị xóa mềm, và giá bán $> 0$ (sản phẩm thường).

#### 3. Request & Response
* **Request Body (`CheckVariantStatusRequest`):**
  * `variantIds` (`Array of Guid`, Bắt buộc).
  * `purpose` (`String`, Bắt buộc: `ORDER`, `AUCTION`, hoặc `CART`).
* **HTTP Status Code:** `200 OK`
* **Response Body (`CheckVariantStatusResponse`):**
  * `items` (`Array of VariantStatusItemDto`): `variantId` (`Guid`), `productId` (`Guid`), `productStatus` (`String`), `variantStatus` (`String`), `saleType` (`String`), `isPurchasable` (`Boolean`), `reason` (`String`: `"OK"` hoặc mã lý do từ chối), `price` (`Decimal?`), `currency` (`String`).
  * `notFoundIds` (`Array of Guid`).
