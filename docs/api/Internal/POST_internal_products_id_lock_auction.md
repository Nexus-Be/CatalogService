# CATALOG SERVICE - POST /api/v1/internal/products/{id}/lock-auction

> **Feature / Use Case:** Khóa sản phẩm khi phiên đấu giá bắt đầu mở [CAT-53]
> **Trạng thái:** ✅ VERIFIED
> **Ngày tạo:** 2026-11-09
> **Tác giả:** Nexus Team & AI Agent

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `POST`
- **Route Endpoint:** `/api/v1/internal/products/{id}/lock-auction`
- **Quyền hạn (RBAC Policy):** `Internal` (Gọi trực tiếp liên service qua Docker network giữa `AuctionService` và `CatalogService`).
- **Controller:** `Catalog.Api/Controllers/Internal/InternalProductsController.cs`
- **Application Service:** `Catalog.Application/Services/Product/IProductService.cs`

---

## 2. Đầu vào & Tham số (Parameters)
### 2.1 Route Parameters
| Tham số | Vị trí | Kiểu C# | Bắt buộc | Ràng buộc / Giới hạn | Ghi chú |
| :--- | :--- | :--- | :---: | :--- | :--- |
| `id` | Route | `Guid` | Có | UUIDv4 format | Mã ID của sản phẩm đấu giá cần khóa |

---

## 3. Cấu trúc Phản hồi (Response Schema)

### 3.1 DTO Phản hồi (`ProductDto`)
* **HTTP Status:** `200 OK`
* **Tính chất Idempotent:** Nếu sản phẩm đã ở trạng thái `InAuction`, Catalog lập tức trả về `200 OK` với thông tin sản phẩm hiện tại, đảm bảo an toàn tuyệt đối khi AuctionService retry do chập chờn mạng.
* **JSON Response Mẫu:**
```json
{
  "id": "d25074e2-9ca8-41fa-a5e0-31bbbfd3c0eb",
  "sellerId": "97b3ae37-9ebc-4d15-b812-35cf6971ea76",
  "categoryId": "10000000-0000-0000-0000-000000000001",
  "name": "Dien Thoai Co Dien Vertu Signature S",
  "description": "Dien thoai Vertu Signature S che tac thu cong",
  "brand": "Vertu",
  "status": "InAuction",
  "weightGrams": 350
}
```

---

## 4. Ma trận Mã Lỗi (Error Matrix RFC 9110)
| HTTP Status | Error Code (`errorCode`) | Error Message (`detail`) | Nguyên nhân |
| :--- | :--- | :--- | :--- |
| `400 Bad Request` | `PRODUCT_ALREADY_AUCTIONED` | Auctioned products cannot be put into auction again. | Sản phẩm đã bán xong trước đó |
| `400 Bad Request` | `VAL_INVALID_INPUT` | This operation is only valid for auction products. | Sản phẩm là hàng thường (`Regular`), không phải hàng đấu giá |
| `400 Bad Request` | `VAL_INVALID_INPUT` | Cannot lock product with status '{status}' for auction. Product must be in Active status. | Sản phẩm đang ở trạng thái `Draft`, `PendingApproval`, `Inactive` hoặc `Banned` |
| `404 Not Found` | `PRODUCT_NOT_FOUND` | Product with id '{id}' was not found. | ID sản phẩm không tồn tại hoặc đã bị xóa |

