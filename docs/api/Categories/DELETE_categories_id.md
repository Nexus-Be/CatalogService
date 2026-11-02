# CATALOG SERVICE - DELETE /api/v1/categories/{id}

> **Feature / Use Case:** Xóa mềm danh mục [CAT-17]
> **Trạng thái:** ✅ VERIFIED
> **Ngày tạo:** 2026-11-01
> **Tác giả:** Nexus Team & AI Agent

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `DELETE`
- **Route Endpoint:** `/api/v1/categories/{id}`
- **Quyền hạn (RBAC Policy):** `[RequireRole("admin", "staff")]`
- **Quy tắc Sở hữu (Resource Ownership):** Yêu cầu vai trò quản trị viên hoặc nhân viên vận hành
- **Controller:** `Catalog.Api/Controllers/Public/CategoriesController.cs`
- **Application Contract:** `Catalog.Application/Services/Category/ICategoryService.cs`

---

## 2. Đầu vào & Quy chuẩn Validation (Input & Validation Schema)
### 2.1 Route Parameters & Query Parameters
| Tham số | Vị trí | Kiểu C# | Bắt buộc | Ràng buộc / Giới hạn | Ghi chú |
| :--- | :--- | :--- | :---: | :--- | :--- |
| `id` | Route | `Guid` | Có | UUIDv4 format | Mã định danh danh mục cần xóa |

### 2.2 Request Body Record DTO
- Endpoint `DELETE` không sử dụng Request Body.

---

## 3. Đầu ra & Hợp đồng DTO thuần (Response Contract - Pure DTO)
- **Mã HTTP thành công:** `204 NoContent`
- **Response Body:** Rỗng (Empty body) theo chuẩn RESTful delete.

---

## 4. Ma trận Mã lỗi RFC 9110 (Error Matrix)
| HTTP Status | Error Code | Message (Sentence case) | Điều kiện kích hoạt (Trigger Condition) |
| :---: | :--- | :--- | :--- |
| `400 Bad Request` | `VAL_INVALID_INPUT` | `Cannot delete category because it has active child categories.` | Danh mục đang chứa danh mục con bên dưới |
| `401 Unauthorized` | `UNAUTHORIZED` | `Authentication is required to access this resource.` | Thiếu thông tin xác thực |
| `403 Forbidden` | `FORBIDDEN` | `You do not have permission to access or modify this resource.` | Không có quyền admin/staff |
| `404 Not Found` | `CATEGORY_NOT_FOUND` | `Category not found with the specified identifier.` | ID không tồn tại hoặc đã bị xóa |

---

## 5. Kế hoạch Thực thi & Checklist Kiểm chứng (Pre-Code Plan & Checklist)
### 5.1 Các bước thực thi nghiệp vụ (Execution Flow Plan)
1. **Bước 1:** Bắt `id` từ route tại `CategoriesController.Delete`.
2. **Bước 2:** Gọi `_categoryService.DeleteAsync(id, currentUserId, ct)`.
3. **Bước 3:** Kiểm tra ràng buộc: nếu có danh mục con trực thuộc, ném ngoại lệ `BusinessRuleValidationException("VAL_INVALID_INPUT", ...)`.
4. **Bước 4:** Gán `category.IsDeleted = true`, `DeletedAt = UtcNow`, ghi `AuditLog` với `AuditAction.CategoryDelete`.
5. **Bước 5:** Trả về `NoContent()`.

### 5.2 Checklist hoàn thành
- [x] **Pre-Code:** Hoàn thiện đặc tả API.
- [x] **Application:** Đã triển khai trong `CategoryService.cs`.
- [x] **Api:** `CategoriesController.Delete` trả `204 NoContent`.
- [x] **Verify:** Build đạt 0 lỗi.

