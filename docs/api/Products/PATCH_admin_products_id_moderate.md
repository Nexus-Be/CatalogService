# CATALOG SERVICE - PATCH /api/v1/admin/products/{id}/moderate

> **Feature / Use Case:** Quản trị viên và Nhân viên kiểm duyệt sản phẩm (Duyệt bán, Khóa tạm thời hoặc Cấm vĩnh viễn) [CAT-51]  
> **Trạng thái:** ⏳ DRAFT (Pre-Code Specification Update)  
> **Ngày cập nhật:** 2026-11-10  
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
| `note` | `string?` | Không | Max 1000 ký tự | Ghi chú chi tiết của kiểm duyệt viên (lưu vào Audit Log) |

### 2.3 Quy tắc Xử lý Nghiệp vụ & Chuyển đổi Trạng thái (State Machine)
1. **`APPROVE` / `UNLOCK`:**
   - Đổi `Status = ProductStatus.Active`.
   - Xóa bỏ cờ khóa: `StatusReason = null`.
   - Cập nhật thời điểm phát hành: `PublishedAt ??= DateTimeOffset.UtcNow`.
   - **Ràng buộc tiên quyết:** Sản phẩm phải có ít nhất 1 variant hợp lệ (`VariantStatus.Active`, có giá nếu là hàng thường) và ít nhất 1 hình ảnh đang hoạt động (`IsActive == true`).
   - **Rào chắn chống gỡ Ban:** Nếu sản phẩm đã bị `Banned` $\to$ Cấm đổi sang `APPROVE` hay `UNLOCK`, trả lỗi `PRODUCT_ALREADY_BANNED`.
2. **`SUSPEND` / `REJECT`:**
   - Đổi `Status = ProductStatus.Inactive`.
   - Đánh dấu lý do cưỡng chế khóa: `StatusReason = ProductInactiveReason.AdminLocked`.
   - Vô hiệu hóa toàn bộ variants thuộc sản phẩm: `v.Status = VariantStatus.Inactive`.
   - **Quy tắc chặn Seller:** Một khi đã bị gán `AdminLocked`, Seller gọi API `PATCH /api/v1/products/{id}/status` sẽ bị chặn với mã `PRODUCT_LOCKED_BY_ADMIN`.
3. **`BAN` ⭐ (Bổ sung mới):**
   - Đổi `Status = ProductStatus.Banned`.
   - Xóa `StatusReason = null` (Banned là trạng thái cấm độc lập, không phải Inactive).
   - Vô hiệu hóa triệt để toàn bộ variants: `v.Status = VariantStatus.Inactive`, `v.IsActive = false`.
   - **Tự động đóng toàn bộ đơn tố cáo đang chờ của sản phẩm:** Các đơn `Pending`/`Investigating` trong `ProductReports` tự động chuyển sang `Resolved` với ghi chú của Admin.
   - **Rào chắn tuyệt đối:** Khi đã `Banned`, cấm mọi hành động sửa đổi từ Seller và cấm mở khóa thông thường.
4. **Bảo vệ Sản phẩm Đấu giá:** Nếu sản phẩm đã ở trạng thái `Auctioned` (đã đấu giá thành công), cấm Admin thay đổi trạng thái $\to$ trả lỗi `PRODUCT_ALREADY_AUCTIONED`.
5. **Kiểm toán (Audit Trail):**
   - Với `BAN`: Ghi `AuditLog` với `Action = AuditAction.ProductBan`.
   - Với các action khác: Ghi `AuditLog` với `Action = AuditAction.ProductUpdate`.

- **JSON Request mẫu (Cấm sản phẩm):**
```json
{
  "action": "BAN",
  "reason": "Buôn bán hàng cấm theo quy định pháp luật",
  "note": "Xác minh sản phẩm thuộc danh mục cấm lưu hành. Cấm vĩnh viễn khỏi toàn bộ hệ thống."
}
```

---

## 3. Đầu ra & Hợp đồng DTO thuần (Response Contract - Pure DTO)

- **Mã HTTP thành công:** `200 OK`
- **Kiểu trả về:** `ProductDto`
- **JSON Response mẫu:**
```json
{
  "id": "77777777-7777-7777-7777-777777777777",
  "sellerId": "11111111-1111-1111-1111-111111111111",
  "categoryId": "22222222-2222-2222-2222-222222222222",
  "name": "Bức Họa Cổ Phong Cảnh Mùa Thu",
  "description": "Bức họa tranh lụa thế kỷ 19",
  "brand": "Nexus Art",
  "status": "Banned"
}
```

---

## 4. Ma trận Mã lỗi RFC 9110 (Error Matrix)

| HTTP Status | Error Code | Detail Message (Sentence case) | Điều kiện phát sinh |
| :---: | :--- | :--- | :--- |
| `400 Bad Request` | `VAL_INVALID_INPUT` | `Action is required. Allowed values: APPROVE, SUSPEND, REJECT, UNLOCK, BAN.` | Trường `action` rỗng hoặc mang giá trị không hợp lệ |
| `400 Bad Request` | `VAL_INVALID_INPUT` | `Cannot approve product without at least one active variant with valid price.` | Duyệt bán sản phẩm nhưng chưa tạo variant hoạt động hoặc thiếu giá |
| `400 Bad Request` | `VAL_INVALID_INPUT` | `Cannot approve product without at least one active product image.` | Duyệt bán sản phẩm nhưng sản phẩm chưa có ảnh nào hoạt động |
| `400 Bad Request` | `PRODUCT_ALREADY_BANNED` | `This product has already been permanently banned and cannot be re-activated.` | Cố gắng duyệt (`APPROVE`/`UNLOCK`) lại một sản phẩm đã bị cấm vĩnh viễn |
| `400 Bad Request` | `PRODUCT_ALREADY_AUCTIONED` | `Auctioned products are permanently locked and cannot be moderated.` | Cố gắng duyệt/khóa sản phẩm đã hoàn tất đấu giá |
| `401 Unauthorized` | `UNAUTHORIZED` | `Authentication is required to access this resource.` | Không gửi kèm token hoặc thiếu `X-User-Id` |
| `403 Forbidden` | `FORBIDDEN` | `You do not have permission to access or modify this resource.` | Token không có role `admin` hoặc `staff` |
| `404 Not Found` | `PRODUCT_NOT_FOUND` | `Product not found in the system.` | Không tìm thấy sản phẩm với `id` tương ứng |
