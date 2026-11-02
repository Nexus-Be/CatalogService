# CATALOG SERVICE - PUT /api/v1/categories/{id}

> **Feature / Use Case:** Cập nhật danh mục [CAT-16]
> **Trạng thái:** ✅ VERIFIED
> **Ngày tạo:** 2026-11-01
> **Tác giả:** Nexus Team & AI Agent

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `PUT` (Hỗ trợ kèm Route Alias `PATCH`)
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
| `id` | Route | `Guid` | Có | UUIDv4 format | Mã định danh danh mục cần sửa |

### 2.2 Request Body Record DTO
- **Vị trí file:** `Catalog.Application/Services/Category/CategoryRequests.cs`
- **Tên C# Record:** `UpdateCategoryRequest`
- **Khai báo C# & DataAnnotations:**
```csharp
public sealed record UpdateCategoryRequest(
    [Required(ErrorMessage = "Category name is required.")]
    [StringLength(255, MinimumLength = 1, ErrorMessage = "Category name cannot exceed 255 characters.")]
    string Name,

    string? Description = null,

    [Range(1, 3, ErrorMessage = "Category level must be between 1 and 3.")]
    short Level = 1,

    int SortOrder = 0,

    bool IsDisabled = false
);
```
- **JSON Request Body mẫu (camelCase):**
```json
{
  "name": "Áo Thun Cổ Tròn",
  "description": "Mô tả danh mục đã cập nhật",
  "level": 3,
  "sortOrder": 2,
  "isDisabled": false
}
```

---

## 3. Đầu ra & Hợp đồng DTO thuần (Response Contract - Pure DTO)
- **Vị trí file:** `Catalog.Application/Services/Category/CategoryDtos.cs`
- **Tên C# Record:** `CategoryDto`
- **Mã HTTP thành công:** `200 OK`
- **JSON Response Body mẫu (Pure DTO - camelCase):**
```json
{
  "id": "10000000-0000-0000-0000-000000000003",
  "parentId": "10000000-0000-0000-0000-000000000002",
  "name": "Áo Thun Cổ Tròn",
  "description": "Mô tả danh mục đã cập nhật",
  "level": 3,
  "sortOrder": 2,
  "isDisabled": false
}
```

---

## 4. Ma trận Mã lỗi RFC 9110 (Error Matrix)
| HTTP Status | Error Code | Message (Sentence case) | Điều kiện kích hoạt (Trigger Condition) |
| :---: | :--- | :--- | :--- |
| `400 Bad Request` | `VAL_INVALID_INPUT` | `Category name cannot be empty.` | Tên vi phạm validation |
| `401 Unauthorized` | `UNAUTHORIZED` | `Authentication is required to access this resource.` | Thiếu xác thực |
| `403 Forbidden` | `FORBIDDEN` | `You do not have permission to access or modify this resource.` | Không có quyền admin/staff |
| `404 Not Found` | `CATEGORY_NOT_FOUND` | `Category not found with the specified identifier.` | ID không tồn tại |

---

## 5. Kế hoạch Thực thi & Checklist Kiểm chứng (Pre-Code Plan & Checklist)
### 5.1 Các bước thực thi nghiệp vụ (Execution Flow Plan)
1. **Bước 1:** Bắt `id` và body `UpdateCategoryRequest` tại controller.
2. **Bước 2:** Gọi `_categoryService.UpdateAsync(id, request, currentUserId, ct)`.
3. **Bước 3:** Tìm category trong DB. Nếu không tìm thấy ném `EntityNotFoundException`.
4. **Bước 4:** Cập nhật thông tin, gán `UpdatedAt = UtcNow`, ghi `AuditLog` với `AuditAction.CategoryUpdate`.
5. **Bước 5:** Trả về `Ok(category)`.

### 5.2 Checklist hoàn thành
- [x] **Pre-Code:** Hoàn thiện đặc tả API.
- [x] **Application:** Đã triển khai trong `CategoryService.cs`.
- [x] **Api:** `CategoriesController.Update` hỗ trợ cả `[HttpPut]` và `[HttpPatch]`.
- [x] **Verify:** Build đạt 0 lỗi.

