# CATALOG SERVICE - POST /api/v1/internal/products/{id}/revert-auctioned

> **Feature / Use Case:** Hoàn tác sản phẩm đấu giá về Active khi Seller từ chối đơn / hủy đơn sau thanh toán [CAT-56]  
> **Trạng thái:** ✅ VERIFIED  
> **Ngày tạo:** 2026-11-10  
> **Tác giả:** Nexus Team & AI Agent  

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `POST`
- **Route Endpoint:** `/api/v1/internal/products/{id}/revert-auctioned`
- **Quyền hạn (RBAC Policy):** `Internal` (Gọi trực tiếp liên service qua Docker network giữa `AuctionService` và `CatalogService`).
- **Controller:** `Catalog.Api/Controllers/Internal/InternalProductsController.cs`
- **Application Service:** `Catalog.Application/Services/Product/IProductService.cs`

---

## 2. Đầu vào & Tham số (Parameters)

### 2.1 Route Parameters
| Tham số | Vị trí | Kiểu C# | Bắt buộc | Ràng buộc / Giới hạn | Ghi chú |
| :--- | :--- | :--- | :---: | :--- | :--- |
| `id` | Route | `Guid` | Có | UUIDv4 format | Mã ID của sản phẩm đấu giá cần hoàn tác |

### 2.2 Request Body (`RevertAuctionedRequest` - Tùy chọn)
| Trường | Kiểu dữ liệu | Bắt buộc | Mặc định | Mô tả |
| :--- | :--- | :---: | :--- | :--- |
| `reason` | `String` | Không | `"SELLER_REJECTED"` | Lý do hoàn tác (ví dụ: `SELLER_REJECTED`, `BUYER_CANCELLED`, `DAMAGED_ITEM`, `PAYMENT_REFUNDED`). |
| `note` | `String?` | Không | `null` | Ghi chú bổ sung từ Auction Service / Seller. |

#### JSON Request Mẫu:
```json
{
  "reason": "SELLER_REJECTED",
  "note": "Seller canceled shipment due to damaged item."
}
```

---

## 3. Cấu trúc Phản hồi (Response Schema)

### 3.1 DTO Phản hồi (`ProductDto`)
* **HTTP Status:** `200 OK`
* **JSON Response Mẫu:**
```json
{
  "id": "56b9292a-d83d-484f-9018-6df7ae3727c9",
  "sellerId": "cccccccc-3333-3333-3333-333333333333",
  "categoryId": "10000000-0000-0000-0000-000000000001",
  "name": "Dong Ho Co Dien Vertu Watch",
  "description": "Dong ho che tac thu cong sang trong",
  "brand": "Vertu",
  "status": "Active",
  "weightGrams": 350
}
```

### 3.2 Cơ chế Lũy thừa (Idempotency)
- Nếu sản phẩm **đã ở trạng thái `Active`** (ví dụ do Auction retry lệnh do mạng chập chờn), Catalog trả về ngay `200 OK` với dữ liệu hiện tại mà không báo lỗi, đảm bảo an toàn tuyệt đối cho các tiến trình retry tự động.

---

## 4. Ma trận Mã Lỗi (Error Matrix RFC 9110)
| HTTP Status | Error Code (`errorCode`) | Error Message (`detail`) | Nguyên nhân |
| :--- | :--- | :--- | :--- |
| `400 Bad Request` | `PRODUCT_NOT_AUCTIONED` | Product is not in Auctioned status. Current status is '{status}'. | Sản phẩm hiện không ở trạng thái `Auctioned` (ví dụ đang `InAuction`, `Draft` hoặc `Inactive`). |
| `400 Bad Request` | `VAL_INVALID_INPUT` | This operation is only valid for auction products. | Sản phẩm thuộc loại bán lẻ thường (`SaleType = Regular`). |
| `404 Not Found` | `PRODUCT_NOT_FOUND` | Product with id '{id}' was not found. | ID sản phẩm không tồn tại hoặc đã bị xóa mềm. |

