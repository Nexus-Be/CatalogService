# CATALOG SERVICE - PATCH /api/v1/products/auctions/{id}/status

> **Feature / Use Case:** Cập nhật trạng thái sản phẩm đấu giá (`Draft` ➔ `Active` ➔ `Auctioned`). Đóng băng vĩnh viễn khi đã bán xong [CAT-49]  
> **Trạng thái:** ✅ VERIFIED  
> **Ngày tạo:** 2026-11-07  
> **Tác giả:** Nexus Team & AI Agent  

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `PATCH`
- **Route Endpoint:** `/api/v1/products/auctions/{id:guid}/status`
- **Quyền hạn (RBAC Policy):** `RequireRole("seller", "admin")`
- **Cơ chế Gateway YARP:** Khớp pattern `"/api/v1/products/{**catch-all}"` chuyển tiếp thẳng vào `catalog-cluster` (Port `8002`).
- **Controller:** `Catalog.Api/Controllers/Public/ProductsController.cs`
- **Application Contract:** `Catalog.Application/Services/Product/IProductService.cs` (`ChangeAuctionStatusAsync`)

---

## 2. Đầu vào & Quy chuẩn Validation (Input & Schema)

### 2.1 Request Headers
| Header | Kiểu | Bắt buộc | Mô tả |
| :--- | :--- | :---: | :--- |
| `x-user-id` | `string` (UUIDv4) | Có | ID của người bán hoặc admin |
| `x-user-roles` | `string` | Có | Chứa vai trò `seller` hoặc `admin` |

### 2.2 Route Parameters
| Tham số | Kiểu C# | Bắt buộc | Mô tả |
| :--- | :--- | :---: | :--- |
| `id` | `Guid` | Có | ID sản phẩm đấu giá |

### 2.3 Request Body
```json
{
  "status": "Auctioned",
  "statusReason": null
}
```

- **Quy tắc chuyển đổi trạng thái (State Machine Transitions):**
  1. `Draft` $\to$ `Active`: Người bán kích hoạt mở sàn đấu giá (Yêu cầu phải có ít nhất 1 ảnh hiện vật).
  2. `Active` $\to$ `Auctioned`: Sàn đấu giá (Auction Service) hoặc Người bán chốt phiên thành công khi đã có người thắng cuộc.
     * Hệ thống tự động chuyển trạng thái của biến thể duy nhất sang `Inactive`.
     * Sản phẩm chuyển vào **Trạng thái kết thúc (Terminal State)**: Bị đóng băng vĩnh viễn, không thể chuyển ngược lại bất kỳ trạng thái nào khác.
  3. `Auctioned` $\to$ Bất kỳ trạng thái nào khác: **BỊ TỪ CHỐI** với mã lỗi `PRODUCT_ALREADY_AUCTIONED`.

---

## 3. Đầu ra & Hợp đồng DTO thuần (Response Contract - Pure DTO)

- **Mã HTTP thành công:** `200 OK`
- **Kiểu trả về:** `ProductDto`
- **JSON Response mẫu (camelCase):**
```json
{
  "id": "88888888-8888-8888-8888-888888888888",
  "sellerId": "11111111-1111-1111-1111-111111111111",
  "categoryId": "22222222-2222-2222-2222-222222222222",
  "name": "Bình Hoa Gốm Cổ Chu Đậu Thế Kỷ 15",
  "description": "Hiện vật gốm sứ hoa lam men rạn nguyên bản có chứng thư giám định cổ vật.",
  "brand": "Chu Đậu",
  "status": "Auctioned"
}
```

---

## 4. Ma trận Mã lỗi RFC 9110 (Error Matrix)
| HTTP Status | Error Code | Detail | Điều kiện phát sinh |
| :---: | :--- | :--- | :--- |
| `400 Bad Request` | `VAL_INVALID_INPUT` | Status is required. / Invalid status value. Allowed values are DRAFT, ACTIVE, INACTIVE, AUCTIONED. | `status` để trống hoặc giá trị không hợp lệ |
| `400 Bad Request` | `VAL_INVALID_INPUT` | This operation is only valid for auction products. | Gọi nhầm vào sản phẩm thường (`SaleType.Regular`) |
| `400 Bad Request` | `PRODUCT_IN_AUCTION` | Product is currently in an active auction and cannot change status. | Thử đổi trạng thái khi sản phẩm đang ở `InAuction` |
| `400 Bad Request` | `PRODUCT_ALREADY_AUCTIONED` | Auctioned products are permanently locked and cannot change status. | Thử đổi trạng thái khi sản phẩm đã ở `Auctioned` |
| `401 Unauthorized` | `UNAUTHORIZED` | Authentication is required to access this resource. | Thiếu header `x-user-id` |
| `403 Forbidden` | `FORBIDDEN` | You do not have permission to modify this product. | Không phải chủ sở hữu hoặc không có vai trò hợp lệ |
| `404 Not Found` | `PRODUCT_NOT_FOUND` | Product with ID '{id}' was not found in the system. | Không tìm thấy sản phẩm |
| `500 Internal Error` | `INTERNAL_SERVER_ERROR` | An unexpected error occurred. | Lỗi cơ sở dữ liệu hoặc hệ thống |

