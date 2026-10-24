# 02 — HỢP ĐỒNG GIAO TIẾP GRPC (CATALOG SERVICE GRPC CONTRACTS)

> **Trạng thái triển khai (2026-10-24):** ❌ **0/13 rpc đã triển khai** — repo **không có file `.proto`**, không có package `Grpc.*`, không có class `CatalogGrpcService`. Bề mặt giao tiếp thật là **13 action REST mock** dưới `/api/v1/catalog/*`.
> **Nguồn sự thật:** code trong repo này (bằng chứng `file:dòng`); không tham chiếu tài liệu ngoài repo.
> **Cập nhật lần cuối:** 2026-10-24.

Dịch vụ gRPC của CatalogService được **đặc tả** trong hợp đồng duy nhất `Contracts/shop.proto` dưới service `CatalogApi`:

> ❌ **File `Contracts/shop.proto` không tồn tại trong repo này** (không có thư mục `Catalog.Api/Contracts/`, không có `.proto` nào). Khối protobuf dưới đây là **đặc tả mục tiêu**, giữ nguyên để làm chuẩn đối chiếu, không phải mô tả hiện trạng. Không sửa khối này khi chưa đồng bộ hợp đồng với Gateway.

---

## 1. DANH SÁCH CÁC RPC METHODS

```protobuf
service CatalogApi {
  // Danh mục sản phẩm (Categories)
  rpc ListCategories(PageRequest) returns (CategoryPage);
  rpc GetCategory(IdRequest) returns (Category);
  rpc CreateCategory(CategoryInput) returns (Category);
  rpc UpdateCategory(UpdateCategoryRequest) returns (Category);
  rpc DeleteCategory(IdRequest) returns (DeleteResponse);
  
  // Sản phẩm công khai (Products)
  rpc ListProducts(ProductQuery) returns (ProductPage);
  rpc GetProduct(IdRequest) returns (Product);
  
  // Sản phẩm của người bán (Seller Products)
  rpc ListProductsBySeller(ProductQuery) returns (ProductPage);
  rpc CreateProduct(ProductInput) returns (Product);
  rpc UpdateProduct(UpdateProductRequest) returns (Product);
  rpc DeleteProduct(IdRequest) returns (DeleteResponse);
  
  // Kho hàng ảo (Inventory)
  rpc GetInventory(UserRequest) returns (InventoryPage);
  rpc AdjustInventory(AdjustInventoryRequest) returns (Inventory);
}
```

### 1.1. Bảng đối chiếu 13 rpc → bề mặt thật (REST mock)

| # | RPC (đặc tả) | Trạng thái gRPC | Thay thế hiện có (REST) | Trạng thái REST | Bằng chứng |
|---|---|---|---|---|---|
| 1 | `ListCategories` | ❌ Chưa có | `GET /api/v1/catalog/categories` | ⚠️ Mock | `CategoriesController.cs:27-33` |
| 2 | `GetCategory` | ❌ Chưa có | `GET /api/v1/catalog/categories/{id}` | ⚠️ Mock | `CategoriesController.cs:35-53` |
| 3 | `CreateCategory` | ❌ Chưa có | `POST /api/v1/catalog/categories` | ⚠️ Mock | `CategoriesController.cs:55-72` |
| 4 | `UpdateCategory` | ❌ Chưa có | `PUT /api/v1/catalog/categories/{id}` | ⚠️ Mock | `CategoriesController.cs:74-92` |
| 5 | `DeleteCategory` | ❌ Chưa có | `DELETE /api/v1/catalog/categories/{id}` | ⚠️ Mock | `CategoriesController.cs:94-112` |
| 6 | `ListProducts` | ❌ Chưa có | `GET /api/v1/catalog/products` | ⚠️ Mock | `ProductsController.cs:27-45` |
| 7 | `GetProduct` | ❌ Chưa có | `GET /api/v1/catalog/products/{id}` | ⚠️ Mock | `ProductsController.cs:55-73` |
| 8 | `ListProductsBySeller` | ❌ Chưa có | `GET /api/v1/catalog/products/seller/{sellerId}` | ⚠️ Mock | `ProductsController.cs:47-53` |
| 9 | `CreateProduct` | ❌ Chưa có | `POST /api/v1/catalog/products` | ⚠️ Mock | `ProductsController.cs:75-92` |
| 10 | `UpdateProduct` | ❌ Chưa có | `PUT /api/v1/catalog/products/{id}` | ⚠️ Mock | `ProductsController.cs:94-112` |
| 11 | `DeleteProduct` | ❌ Chưa có | `DELETE /api/v1/catalog/products/{id}` | ⚠️ Mock | `ProductsController.cs:114-132` |
| 12 | `GetInventory` | ❌ Chưa có | `GET /api/v1/catalog/inventory/{productId}` | ⚠️ Mock — trả `Available=100` cho cả product không tồn tại | `InventoryController.cs:25-31`, `MockCatalogStore.cs:188-195` |
| 13 | `AdjustInventory` | ❌ Chưa có | `POST /api/v1/catalog/inventory/{productId}/adjust` | ⚠️ Mock — product lạ được tạo với `Available=100` | `InventoryController.cs:33-39`, `MockCatalogStore.cs:197-210` |

**Ghi chú chênh lệch hợp đồng**: kiểu message trong khối protobuf (`ProductQuery`, `ProductInput`, `InventoryPage`, `UserRequest`) **chưa từng được định nghĩa ở đâu trong repo**. Ở REST, `GetInventory` nhận `productId:guid` trên route chứ không nhận `UserRequest`, và trả **một object** `InventoryDto`, không phải `InventoryPage`.

---

## 2. QUY CHUẨN XỬ LÝ LỖI & MÃ LỖI (ERROR STATUS CODES)

Khi xử lý trong `CatalogGrpcService`, ném ra `RpcException` (❌ class này **chưa tồn tại**, đây là quy chuẩn mục tiêu):
- **`StatusCode.InvalidArgument`** (400 Bad Request): Giá sản phẩm $\le 0$, tên sản phẩm rỗng, `Available < 0`, `CategoryId` không đúng định dạng UUID.
- **`StatusCode.NotFound`** (404 Not Found): Không tìm thấy sản phẩm hoặc danh mục với ID yêu cầu.
- **`StatusCode.PermissionDenied`** (403 Forbidden): Seller sửa hoặc xóa sản phẩm thuộc quyền sở hữu của seller khác.
- **`StatusCode.AlreadyExists`** (409 Conflict): Slug sản phẩm bị trùng lặp trong hệ thống.
- **`StatusCode.FailedPrecondition`** (412 Precondition Failed): Xóa danh mục khi danh mục đó vẫn còn chứa sản phẩm con hoặc danh mục con.

### 2.1. Mã lỗi đang thực sự dùng (REST)

| Mã lỗi | HTTP | Điều kiện kích hoạt thật | Bằng chứng |
|---|---|---|---|
| `VAL_INVALID_PAGINATION` | 400 | `page < 1` hoặc `pageSize` ngoài `1..100` khi list products | `ProductsController.cs:32-40` |
| `PRODUCT_NOT_FOUND` | 404 | Không tìm thấy product theo id | `ProductsController.cs:60-69` |
| `CATEGORY_NOT_FOUND` | 404 | Không tìm thấy category theo id | `CategoriesController.cs:40-49` |
| `VAL_INVALID_INPUT` | 400 | `Name`/`SellerId`/`CategoryId` rỗng khi tạo product; `Name` rỗng khi tạo category | `ProductsController.cs:80-87`, `CategoriesController.cs:60-67` |
| `NOT_FOUND` | 404 | `EntityNotFoundException` (middleware) — **hiện 0 nơi ném** | `GlobalExceptionHandlerMiddleware.cs:38-42` |
| `BUSINESS_RULE_VIOLATION` | 400 | `BusinessRuleValidationException` — **hiện 0 nơi ném** | `GlobalExceptionHandlerMiddleware.cs:43-47` |
| `DOMAIN_ERROR` | 400 | `DomainException` — **hiện 0 nơi ném** | `GlobalExceptionHandlerMiddleware.cs:48-52` |
| `VAL_INVALID_ARGUMENT` | 400 | `ArgumentException` — chưa có nơi ném | `GlobalExceptionHandlerMiddleware.cs:53-57` |
| `INTERNAL_SERVER_ERROR` | 500 | Mọi exception còn lại | `GlobalExceptionHandlerMiddleware.cs:58-62` |

**Chưa có** (đặc tả hứa): `PERMISSION_DENIED`/403 và `ALREADY_EXISTS`/409 — repo **0 `[Authorize]`, 0 kiểm tra quyền sở hữu seller**, và `Product` không có `Slug` để phát hiện trùng. Việc xoá danh mục còn sản phẩm con **không bị chặn** (`MockCatalogStore.cs:124-132` chỉ xoá khỏi list).
