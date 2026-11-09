# CATALOG SERVICE - POST /api/v1/internal/products/{id}/mark-auctioned

> **Feature / Use Case:** Chốt bán sản phẩm khi phiên đấu giá thành công [CAT-55]
> **Trạng thái:** ✅ VERIFIED
> **Ngày tạo:** 2026-11-09
> **Tác giả:** Nexus Team & AI Agent

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `POST`
- **Route Endpoint:** `/api/v1/internal/products/{id}/mark-auctioned`
- **Quyền hạn (RBAC Policy):** `Internal` (Gọi trực tiếp liên service qua Docker network giữa `AuctionService` và `CatalogService`).
- **Controller:** `Catalog.Api/Controllers/Internal/InternalProductsController.cs`
- **Application Service:** `Catalog.Application/Services/Product/IProductService.cs`

---

## 2. Đầu vào & Tham số (Parameters)
### 2.1 Route Parameters
| Tham số | Vị trí | Kiểu C# | Bắt buộc | Ràng buộc / Giới hạn | Ghi chú |
| :--- | :--- | :--- | :---: | :--- | :--- |
| `id` | Route | `Guid` | Có | UUIDv4 format | Mã ID của sản phẩm đấu giá cần chốt bán |

---

## 3. Cấu trúc Phản hồi (Response Schema)

### 3.1 DTO Phản hồi (`ProductDto`)
* **HTTP Status:** `200 OK`
* **Tính chất:** Idempotent (Nếu sản phẩm đã mang trạng thái `Auctioned`, gọi lại vẫn trả về 200 OK thành công).
* **JSON Response Mẫu:**
```json
{
  "id": "d25074e2-9ca8-41fa-a5e0-31bbbfd3c0eb",
  "sellerId": "97b3ae37-9ebc-4d15-b812-35cf6971ea76",
  "categoryId": "10000000-0000-0000-0000-000000000001",
  "name": "Dien Thoai Co Dien Vertu Signature S",
  "description": "Dien thoai Vertu Signature S che tac thu cong",
  "brand": "Vertu",
  "status": "Auctioned",
  "weightGrams": 350
}
```

---

## 4. Ma trận Mã Lỗi (Error Matrix RFC 9110)
| HTTP Status | Error Code (`errorCode`) | Error Message (`detail`) | Nguyên nhân |
| :--- | :--- | :--- | :--- |
| `400 Bad Request` | `VAL_INVALID_INPUT` | This operation is only valid for auction products. | Sản phẩm là hàng thường (`Regular`), không thể mark auctioned |
| `404 Not Found` | `PRODUCT_NOT_FOUND` | Product with id '{id}' was not found. | ID sản phẩm không tồn tại hoặc đã bị xóa |

