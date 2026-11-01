# CATALOG SERVICE - POST /api/v1/categories

> **Feature / Use Case:** Tạo danh mục mới [CAT-15]
> **Trạng thái:** ✅ VERIFIED
> **Ngày tạo:** 2026-11-01
> **Tác giả:** Nexus Team & AI Agent

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `POST`
- **Route Endpoint:** `/api/v1/categories`
- **Quyền hạn (RBAC Policy):** `[RequireRole("admin", "staff")]`
- **Quy tắc Sở hữu (Resource Ownership):** Yêu cầu vai trò quản trị viên hoặc nhân viên vận hành
- **Controller:** `Catalog.Api/Controllers/Public/CategoriesController.cs`
- **Application Contract:** `Catalog.Application/Services/Category/ICategoryService.cs`

---

## 2. Đầu vào & Quy chuẩn Validation (Input & Validation Schema)
### 2.1 Route Parameters & Query Parameters
*Không có tham số route hoặc query.*

### 2.2 Request Body Record DTO
- **Vị trí file:** `Catalog.Application/Services/Category/CategoryRequests.cs`
- **Tên C# Record:** `CreateCategoryRequest`
- **Khai báo C# & DataAnnotations:**
```csharp
public sealed record CreateCategoryRequest(
    [Required(ErrorMessage = "Category name is required.")]
    [StringLength(255, MinimumLength = 1, ErrorMessage = "Category name cannot exceed 255 characters.")]
    string Name,

    string? Description = null,

    Guid? ParentId = null,

    [Range(1, 3, ErrorMessage = "Category level must be between 1 and 3.")]
    short Level = 1,

    int SortOrder = 0
);
```
- **JSON Request Body mẫu (camelCase):**
```json
{
  "name": "Áo Thun Nam",
  "description": "Các loại áo phông nam",
  "parentId": "10000000-0000-0000-0000-000000000002",
  "level": 3,
  "sortOrder": 1
}
```

---

## 3. Đầu ra & Hợp đồng DTO thuần (Response Contract - Pure DTO)
- **Vị trí file:** `Catalog.Application/Services/Category/CategoryDtos.cs`
- **Tên C# Record:** `CategoryDto`
- **Mã HTTP thành công:** `201 Created`
- **JSON Response Body mẫu (Pure DTO - camelCase):**
```json
{
  "id": "10000000-0000-0000-0000-000000000003",
  "parentId": "10000000-0000-0000-0000-000000000002",
  "name": "Áo Thun Nam",
  "description": "Các loại áo phông nam",
  "level": 3,
  "sortOrder": 1,
  "isDisabled": false
}
```

---

## 4. Ma trận Mã lỗi RFC 9110 (Error Matrix)
| HTTP Status | Error Code | Message (Sentence case) | Điều kiện kích hoạt (Trigger Condition) |
| :---: | :--- | :--- | :--- |
| `400 Bad Request` | `VAL_INVALID_INPUT` | `Category name cannot be empty.` | Tên để trống hoặc vi phạm validation độ dài |
| `400 Bad Request` | `VAL_INVALID_INPUT` | `Category level must be between 1 and 3.` | Cấp độ vượt ngoài khoảng 1 đến 3 |
| `401 Unauthorized` | `UNAUTHORIZED` | `Authentication is required to access this resource.` | Thiếu token hoặc thông tin header |
| `403 Forbidden` | `FORBIDDEN` | `You do not have permission to access or modify this resource.` | Không có vai trò admin hoặc staff |
| `404 Not Found` | `CATEGORY_NOT_FOUND` | `Category not found with the specified identifier.` | `parentId` cung cấp không tồn tại |

---

## 5. Kế hoạch Thực thi & Checklist Kiểm chứng (Pre-Code Plan & Checklist)
### 5.1 Các bước thực thi nghiệp vụ (Execution Flow Plan)
1. **Bước 1:** Bắt request body tại controller, xác thực role `admin`/`staff`.
2. **Bước 2:** Lấy `currentUserId` từ `this.GetUserId()`.
3. **Bước 3:** Validate tên, level và kiểm tra danh mục cha `parentId` (nếu có).
4. **Bước 4:** Tạo entity `Category`, lưu vào PostgreSQL và ghi `AuditLog` với `AuditAction.CategoryCreate`.
5. **Bước 5:** Trả về `StatusCode(StatusCodes.Status201Created, category)`.

### 5.2 Checklist hoàn thành
- [x] **Pre-Code:** Hoàn thiện đặc tả API.
- [x] **Application:** Triển khai `CreateAsync` trong `CategoryService.cs`.
- [x] **Api:** `CategoriesController.Create` trả về HTTP 201 với `CategoryDto`.
- [x] **Verify:** `dotnet build Catalog.sln -c Release` 0 lỗi.

