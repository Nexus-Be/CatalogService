# CATALOG SERVICE - PATCH /api/v1/admin/products/{id}/moderate

> **Feature / Use Case:** Quản trị viên và Nhân viên kiểm duyệt sản phẩm (Duyệt bán, Khóa tạm thời, Từ chối duyệt hoặc Cấm vĩnh viễn) [CAT-51] kèm kích hoạt tồn kho vật lý tự động sang FulfillmentService  
> **Trạng thái:** ✅ VERIFIED  
> **Ngày cập nhật:** 2026-11-12  
> **Tác giả:** Nexus Team & AI Agent  

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `PATCH`
- **Route Endpoint:** `/api/v1/admin/products/{id}/moderate` (Canonical route duy nhất)
- **Quyền hạn (RBAC Policy):** `[RequireRole("admin", "staff")]`
- **Header bắt buộc:**
  - `X-User-Id: <Guid>` (UUID của Quản trị viên hoặc Nhân viên thực hiện duyệt)
  - `X-User-Roles: admin` hoặc `X-User-Roles: staff`
- **Controller:** `Catalog.Api/Controllers/Public/AdminProductsController.cs`
- **Application Contract:** `Catalog.Application/Services/Product/IProductService.cs` (`ModerateProductAsync`)
- **Tương tác Liên Service (Deferred Inventory Sync):** Khi Quản trị viên/Nhân viên thực hiện hành động `APPROVE` hoặc `UNLOCK` đưa sản phẩm thương mại thường (`SaleType.Regular`) sang `Active`:
  - Hệ thống tự động quét toàn bộ biến thể hoạt động của sản phẩm (`v.Status == VariantStatus.Active && !v.IsDeleted`).
  - Gửi yêu cầu khởi tạo tồn kho vật lý sang **FulfillmentService** qua client `_fulfillmentClient.InitializeInventoryForSkuAsync(variant.Id, product.SellerId, initialQuantity: 0)`.
  - Giúp bảo vệ hệ thống kho không bị ô nhiễm bởi các sản phẩm rác hay sản phẩm nháp chưa qua kiểm duyệt.

---

## 2. Đầu vào & Cấu trúc DTO (Request Body Contract)

### 2.1 Route Parameters
| Tham số | Kiểu C# | Bắt buộc | Ràng buộc | Mô tả |
| :--- | :--- | :---: | :--- | :--- |
| `id` | `Guid` | Có | UUIDv4 hợp lệ | Mã định danh duy nhất của sản phẩm cần kiểm duyệt |

### 2.2 Request Body (`ModerateProductRequest`)
| Thuộc tính | Kiểu C# | Bắt buộc | Giá trị cho phép | Mô tả |
| :--- | :--- | :---: | :--- | :--- |
| `action` | `string` | Có | `APPROVE`, `SUSPEND`, `REJECT`, `UNLOCK`, `BAN` | Hành động kiểm duyệt (không phân biệt hoa thường) |
| `reason` | `string?` | Không | Max 200 ký tự | Lý do kiểm duyệt ngắn gọn |
| `note` | `string?` | Không | Max 1000 ký tự | Ghi chú chi tiết của kiểm duyệt viên (được lưu vào AuditLog và trả về cho Seller khi bị `REJECT`) |

### 2.3 Quy tắc Xử lý Nghiệp vụ & Chuyển đổi Trạng thái (State Machine)

1. **`APPROVE` / `UNLOCK` (Duyệt lên sàn hoặc Mở khóa bán):**
   - Đổi `Status = ProductStatus.Active`.
   - Xóa bỏ cờ khóa: `StatusReason = null`.
   - Cập nhật thời điểm phát hành: `PublishedAt ??= DateTimeOffset.UtcNow`.
   - Kích hoạt lại các biến thể hợp lệ (`v.Status = VariantStatus.Active`).
   - **Khởi tạo kho vật lý:** Gọi `InitializeInventoryForSkuAsync` sang FulfillmentService cho tất cả SKU active.
   - **Ràng buộc tiên quyết:** Sản phẩm phải có ít nhất 1 variant hợp lệ có giá và ít nhất 1 ảnh active.
   - **Rào chắn chống gỡ Ban:** Nếu sản phẩm đã bị `Banned` $\to$ Cấm đổi sang `APPROVE` hay `UNLOCK`, trả lỗi `400 PRODUCT_ALREADY_BANNED`.

2. **`REJECT` (Từ chối duyệt đưa lên sàn):**
   - Đổi `Status = ProductStatus.Draft`.
   - Đánh dấu lý do từ chối: `StatusReason = ProductInactiveReason.Rejected` (giá trị `4`).
   - Lưu trữ `request.Note` vào `AuditLog`.
   - Cho phép người bán chỉnh sửa hình ảnh, nội dung dựa theo phản hồi từ kiểm duyệt viên và gửi duyệt lại qua `PendingApproval`.

3. **`SUSPEND` (Đình chỉ / Khóa bán vi phạm tạm thời):**
   - Đổi `Status = ProductStatus.Inactive`.
   - Đánh dấu lý do cưỡng chế khóa: `StatusReason = ProductInactiveReason.AdminLocked` (giá trị `3`).
   - Vô hiệu hóa toàn bộ variants thuộc sản phẩm: `v.Status = VariantStatus.Inactive`.
   - Người bán không thể tự mở bán lại (bị chặn bởi `PRODUCT_LOCKED_BY_ADMIN`), bắt buộc phải liên hệ CSKH.

4. **`BAN` (Cấm vĩnh viễn):**
   - Đổi `Status = ProductStatus.Banned`.
   - Vô hiệu hóa triệt để toàn bộ variants (`v.Status = VariantStatus.Inactive`).
   - **Tự động đóng toàn bộ đơn tố cáo vi phạm:** Các đơn tố cáo `Pending` hoặc `Investigating` trong `ProductReports` tự động chuyển sang `Resolved` với ghi chú: `"Product permanently banned by administration: " + note`.
   - Ghi `AuditLog` với `Action = AuditAction.ProductBan`.

5. **Bảo vệ Sản phẩm Đấu giá:** Nếu sản phẩm đã ở trạng thái `Auctioned` (đã đấu giá thành công), cấm Admin thay đổi trạng thái $\to$ trả lỗi `PRODUCT_ALREADY_AUCTIONED`.

6. **Tự động gửi thông báo In-App tới Người bán (Seller) qua NotificationService:**
   - Khi `APPROVE` / `UNLOCK`: Gửi thông báo `PRODUCT_APPROVED` ("Sản phẩm đã được phê duyệt thành công").
   - Khi `REJECT`: Gửi thông báo `PRODUCT_REJECTED` ("Sản phẩm bị từ chối phê duyệt", đính kèm lý do).
   - Khi `SUSPEND`: Gửi thông báo `PRODUCT_SUSPENDED` ("Sản phẩm bị tạm dừng hiển thị", đính kèm lý do).
   - Khi `BAN`: Gửi thông báo `PRODUCT_BANNED` ("CẢNH BÁO: Sản phẩm bị cấm vĩnh viễn và trừ 10 điểm uy tín").
   - **Cơ chế chịu lỗi:** Non-blocking resilient execution (bọc try-catch). Nếu NotificationService offline, thao tác kiểm duyệt vẫn thành công.

---

## 3. Request Body Mẫu

### 3.1 Admin phê duyệt sản phẩm lên sàn:
```json
{
  "action": "APPROVE",
  "note": "Sản phẩm đạt tiêu chuẩn hình ảnh và mô tả để mở bán công khai."
}
```

### 3.2 Admin từ chối phê duyệt với lý do yêu cầu chỉnh sửa:
```json
{
  "action": "REJECT",
  "note": "Hình ảnh bị mờ và thiếu kích thước sản phẩm. Vui lòng bổ sung ảnh chụp thực tế rõ nét."
}
```

---

## 4. Đầu ra & Hợp đồng DTO thuần (Response Contract - Pure DTO)

- **Mã HTTP thành công:** `200 OK`
- **Kiểu trả về:** `ProductDto`
- **JSON Response mẫu:**
```json
{
  "id": "f3f47581-6036-4fa1-b9fa-803770308c71",
  "sellerId": "cccccccc-3333-3333-3333-333333333333",
  "categoryId": "250ac1a7-eef4-43f4-9d0d-310755f735c4",
  "name": "Quần Jogger Nexus Streetwear",
  "description": "Quần jogger thể thao cao cấp co giãn 4 chiều.",
  "brand": "Nexus Wear",
  "status": "Active",
  "weightGrams": 300
}
```

---

## 5. Ma trận Mã lỗi RFC 9110 (Error Matrix)

| HTTP Status | Error Code | Detail Message (Sentence case) | Điều kiện phát sinh |
| :---: | :--- | :--- | :--- |
| `400 Bad Request` | `VAL_INVALID_INPUT` | `Action is required. Allowed values: APPROVE, SUSPEND, REJECT, UNLOCK, BAN.` | Trường `action` rỗng hoặc không thuộc danh sách cho phép |
| `400 Bad Request` | `VAL_INVALID_INPUT` | `Cannot approve product without at least one active variant with valid price.` | Duyệt bán sản phẩm nhưng chưa tạo variant hoạt động hoặc thiếu giá |
| `400 Bad Request` | `VAL_INVALID_INPUT` | `Cannot approve product without at least one active product image.` | Duyệt bán sản phẩm nhưng sản phẩm chưa có ảnh nào hoạt động |
| `400 Bad Request` | `PRODUCT_ALREADY_BANNED` | `This product has already been permanently banned and cannot be re-activated.` | Cố gắng duyệt (`APPROVE`/`UNLOCK`) lại một sản phẩm đã bị cấm vĩnh viễn |
| `400 Bad Request` | `PRODUCT_ALREADY_AUCTIONED` | `Auctioned products are permanently locked and cannot be moderated.` | Cố gắng duyệt/khóa sản phẩm đã hoàn tất đấu giá |
| `401 Unauthorized` | `UNAUTHORIZED` | `Authentication is required to access this resource.` | Không gửi kèm token hoặc thiếu `X-User-Id` |
| `403 Forbidden` | `FORBIDDEN` | `You do not have permission to access or modify this resource.` | Token không có role `admin` hoặc `staff` |
| `404 Not Found` | `PRODUCT_NOT_FOUND` | `Product not found in the system.` | Không tìm thấy sản phẩm với `id` tương ứng |
| `500 Internal Error` | `INTERNAL_SERVER_ERROR` | `An unexpected error occurred while processing the request.` | Lỗi hệ thống trong quá trình lưu dữ liệu |
