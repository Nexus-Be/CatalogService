# CATALOG SERVICE - GET /api/v1/categories/{id}

> **Feature / Use Case:** Lấy thông tin chi tiết danh mục theo ID (Tự động nạp danh sách các danh mục con `children` nếu có)
> **Trạng thái:** ✅ VERIFIED
> **Ngày tạo:** 2026-11-01
> **Tác giả:** Nexus Team & AI Agent

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `GET`
- **Route Endpoint:** `/api/v1/categories/{id}`
- **Quyền hạn (RBAC Policy):** `AllowAnonymous` (Công khai cho toàn bộ người dùng)
- **Quy tắc Sở hữu (Resource Ownership):** Không yêu cầu
- **Controller:** `Catalog.Api/Controllers/Public/CategoriesController.cs`
- **Application Contract:** `Catalog.Application/Services/Category/ICategoryService.cs`

---

## 2. Đầu vào & Quy chuẩn Validation (Input & Validation Schema)
### 2.1 Route Parameters & Query Parameters
| Tham số | Vị trí | Kiểu C# | Bắt buộc | Ràng buộc / Giới hạn | Ghi chú |
| :--- | :--- | :--- | :---: | :--- | :--- |
| `id` | Route | `Guid` | Có | UUIDv4 format | Khóa chính của danh mục cần lấy chi tiết |

### 2.2 Request Body Record DTO
- Endpoint phương thức `GET` không sử dụng Request Body.

---

## 3. Đầu ra & Hợp đồng DTO thuần (Response Contract - Pure DTO)
- **Vị trí file:** `Catalog.Application/Services/Category/CategoryDtos.cs`
- **Tên C# Record:** `CategoryDto`
- **Mã HTTP thành công:** `200 OK`
- **Khai báo C# Record:**
```csharp
public sealed record CategoryDto(
    Guid Id,
    Guid? ParentId,
    string Name,
    string? Description,
    short Level,
    int SortOrder,
    bool IsDisabled,
    IReadOnlyList<CategoryDto>? Children = null);
```
- **JSON Response Body mẫu (Pure DTO - camelCase):**
```json
{
  "id": "10000000-0000-0000-0000-000000000001",
  "parentId": null,
  "name": "Thời Trang",
  "description": "Quần áo thời trang nam nữ",
  "level": 1,
  "sortOrder": 1,
  "isDisabled": false,
  "children": [
    {
      "id": "10000000-0000-0000-0000-000000000002",
      "parentId": "10000000-0000-0000-0000-000000000001",
      "name": "Thời Trang Nam",
      "description": "Thời trang dành cho phái nam",
      "level": 2,
      "sortOrder": 1,
      "isDisabled": false,
      "children": null
    }
  ]
}
```

---

## 4. Ma trận Mã lỗi RFC 9110 (Error Matrix)
| HTTP Status | Error Code | Message (Sentence case) | Điều kiện kích hoạt (Trigger Condition) |
| :---: | :--- | :--- | :--- |
| `404 Not Found` | `CATEGORY_NOT_FOUND` | `Category not found with the specified identifier.` | ID không tồn tại hoặc đã bị xóa mềm |
| `500 Internal Error` | `INTERNAL_SERVER_ERROR` | `An unexpected error occurred while processing the request.` | Lỗi hệ thống |

---

## 5. Kế hoạch Thực thi & Checklist Kiểm chứng (Pre-Code Plan & Checklist)
### 5.1 Các bước thực thi nghiệp vụ (Execution Flow Plan)
1. **Bước 1:** Bắt `id` từ Route.
2. **Bước 2:** Gọi `_categoryService.GetByIdAsync(id, ct)`.
3. **Bước 3:** Truy vấn entity Category trong DB với `.AsNoTracking()`. Nếu không tìm thấy ném `EntityNotFoundException("CATEGORY_NOT_FOUND", ...)`.
4. **Bước 4 (Nạp Cây Con):** Truy vấn các danh mục con có `ParentId == id` và `IsDeleted == false`. Nếu có cây con, gán danh sách vào thuộc tính `Children`.
5. **Bước 5:** Trả về `Ok(category)`.

### 5.2 Checklist hoàn thành
- [x] **Pre-Code:** Bổ sung trường `Children` vào `CategoryDto`.
- [x] **Application:** Đã cài đặt nạp cây con trong `CategoryService.cs`.
- [x] **Api:** `CategoriesController.GetById` trả Pure DTO `CategoryDto`.
- [x] **Verify:** Đã kiểm tra logic.
