# 02 — HỢP ĐỒNG GIAO TIẾP RESTFUL (CATALOG SERVICE REST CONTRACTS)

> **Trạng thái triển khai (2026-10-30):** ✅ ĐÃ CHUẨN HÓA 100% RESTFUL — Toàn bộ giao tiếp sản phẩm và danh mục qua RESTful Web API (`CategoriesController`, `ProductsController`). Bãi bỏ hoàn toàn gRPC và RabbitMQ.
> **Nguồn sự thật:** code (`Catalog.Api/Controllers/CategoriesController.cs`, `Catalog.Api/Controllers/ProductsController.cs`).
> **Cập nhật lần cuối:** 2026-10-30.

---

## 1. DANH MỤC ENDPOINTS RESTFUL CHÍNH THỨC

### 1.1. Sản phẩm (`ProductsController` — `/api/v1/products`)
* `GET /api/v1/products`: Danh sách sản phẩm có phân trang (`page`, `pageSize`).
* `GET /api/v1/products/{id}`: Chi tiết sản phẩm theo mã định danh `id` (GUID).

### 1.2. Danh mục (`CategoriesController` — `/api/v1/categories`)
* `GET /api/v1/categories`: Danh sách toàn bộ danh mục ngành hàng.
* `GET /api/v1/categories/{id}`: Chi tiết danh mục theo mã định danh `id` (GUID).

---

## 2. NGUYÊN TẮC BẮT BIẾN

* **Envelope & Errors**: Phản hồi bọc trong phong bì `ApiResponse<T>` hoặc `ApiResponse<PagedData<T>>`.
* **Casing**: DTO dùng PascalCase trong C# và camelCase trong JSON serialization.
* **1-to-1 Controller Pairing**: Mỗi controller chỉ inject duy nhất feature service tương ứng (`IProductService`, `ICategoryService`).
