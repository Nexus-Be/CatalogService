# CATALOG SERVICE - DELETE /api/v1/variants/{id}

> **Feature / Use Case:** Xóa mềm biến thể sản phẩm [CAT-11] (Chặn xóa nếu biến thể đã phát sinh đơn hàng trong hệ thống - `hasOrder == true`)
> **Trạng thái:** ✅ VERIFIED
> **Ngày tạo:** 2026-11-01
> **Tác giả:** Nexus Team & AI Agent

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `DELETE`
- **Route Endpoint:** `/api/v1/variants/{id}`
- **Quyền hạn (RBAC Policy):** `RequireRole("seller", "admin")`
- **Quy tắc Sở hữu (Resource Ownership):** Bắt buộc sản phẩm chứa biến thể này phải thuộc quyền sở hữu của người bán (`product.SellerId == currentUserId`). Admin có quyền can thiệp.
- **Controller:** `Catalog.Api/Controllers/Public/VariantsController.cs`
- **Application Contract:** `Catalog.Application/Services/Product/IProductService.cs`

---

## 2. Đầu vào & Quy chuẩn Validation (Input & Validation Schema)
### 2.1 Route Parameters & Query Parameters
| Tham số | Vị trí | Kiểu C# | Bắt buộc | Ràng buộc / Giới hạn | Ghi chú |
| :--- | :--- | :--- | :---: | :--- | :--- |
| `id` | Route | `Guid` | Có | UUIDv4 format | Khóa chính của biến thể cần xóa |

### 2.2 Request Body Record DTO
- Endpoint `DELETE` không sử dụng Request Body.

---

## 3. Đầu ra & Hợp đồng DTO thuần (Response Contract - Pure DTO)
- **Mã HTTP thành công:** `204 NoContent`
- **Phản hồi:** Trống (Empty body).

---

## 4. Ma trận Mã lỗi RFC 9110 (Error Matrix)
| HTTP Status | Error Code | Message (Sentence case) | Điều kiện kích hoạt (Trigger Condition) |
| :---: | :--- | :--- | :--- |
| `400 Bad Request` | `VAL_VARIANT_HAS_ORDERS` | `Cannot delete a variant that has associated orders.` | Biến thể có cờ `HasOrder == true` |
| `401 Unauthorized` | `UNAUTHORIZED` | `Authentication is required to access this resource.` | Không có Bearer token hợp lệ |
| `403 Forbidden` | `FORBIDDEN` | `You do not have permission to delete this variant.` | Người bán không sở hữu sản phẩm chứa biến thể |
| `404 Not Found` | `VARIANT_NOT_FOUND` | `Variant not found with the specified identifier.` | Không tìm thấy biến thể hoặc đã bị xóa |
| `500 Internal Error` | `INTERNAL_SERVER_ERROR` | `An unexpected error occurred while processing the request.` | Lỗi không mong muốn trong quá trình thực thi |

---

## 5. Kế hoạch Thực thi & Checklist Kiểm chứng (Pre-Code Plan & Checklist)
### 5.1 Các bước thực thi nghiệp vụ (Execution Flow Plan)
1. **Bước 1:** `VariantsController.DeleteVariant` nhận `id`.
2. **Bước 2:** Lấy `currentUserId` từ JWT, gọi `_productService.SoftDeleteVariantAsync(id, currentUserId, ct)`.
3. **Bước 3:** Nạp entity `ProductVariant` kèm `Product` cha.
4. **Bước 4:** Kiểm tra quyền sở hữu của người bán đối với sản phẩm.
5. **Bước 5 (Rào chắn Bất biến):** Kiểm tra `variant.HasOrder`. Nếu `true`, ném `BusinessRuleValidationException("VAL_VARIANT_HAS_ORDERS", "Cannot delete a variant that has associated orders.")`.
6. **Bước 6:** Đánh dấu xóa mềm: `variant.IsDeleted = true; variant.DeletedAt = DateTimeOffset.UtcNow;`.
7. **Bước 7:** Commit transaction vào database và trả về `204 NoContent`.

### 5.2 Checklist hoàn thành
- [x] **Pre-Code:** Đặc tả rào chắn kiểm tra đơn hàng `HasOrder`.
- [x] **Application:** Triển khai phương thức `SoftDeleteVariantAsync` trong `ProductService.cs`.
- [x] **Api:** `VariantsController.DeleteVariant` trả `204 NoContent`.
- [x] **Verify:** `dotnet build Catalog.sln -c Release` thành công 100% với 0 lỗi.

