# CATALOG SERVICE - GET /api/v1/categories/admin

> **Feature / Use Case:** Quản lý danh mục dành cho Admin/Staff [CAT-17] (Hỗ trợ lọc, phân trang, cấu trúc cây và xem cả danh mục đang bị vô hiệu hóa)
> **Trạng thái:** ✅ VERIFIED
> **Ngày tạo:** 2026-11-06
> **Tác giả:** Nexus Team & AI Agent

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `GET`
- **Route Endpoint:** `/api/v1/categories/admin`
- **Quyền hạn (RBAC Policy):** `RequireRole("admin", "staff")`
- **Quy tắc Sở hữu (Resource Ownership):** Dành riêng cho nhân sự vận hành sàn (Admin, Staff), không áp dụng kiểm tra sở hữu người bán.
- **Controller:** `Catalog.Api/Controllers/Public/CategoriesController.cs`
- **Application Contract:** `Catalog.Application/Services/Category/ICategoryService.cs`
- **Tương tác Liên Service:** Không có (truy vấn nội bộ database `AppDbContext.Categories`).

---

## 2. Đầu vào & Quy chuẩn Validation (Input & Validation Schema)
### 2.1 Route Parameters & Query Parameters
| Tham số | Vị trí | Kiểu C# | Bắt buộc | Ràng buộc / Giới hạn | Ghi chú |
| :--- | :--- | :--- | :---: | :--- | :--- |
| `page` | Query | `int` | Không | Min: 1, Mặc định: `1` | Số thứ tự trang |
| `pageSize` | Query | `int` | Không | Min: 1, Max: 100, Mặc định: `10` | Kích thước trang |
| `sortBy` | Query | `string?` | Không | `sortOrder`, `name`, `level`, Mặc định: `"sortOrder"` | Cột sắp xếp |
| `sortDirection` | Query | `string?` | Không | `asc`, `desc`, Mặc định: `"asc"` | Hướng sắp xếp |
| `parentId` | Query | `Guid?` | Không | UUIDv4 format | Lọc theo danh mục cha trực tiếp |
| `tree` | Query | `bool` | Không | Mặc định: `false` | Nếu `true`, trả về cấu trúc cây phân cấp |
| `keyword` | Query | `string?` | Không | Max: 100 ký tự | Tìm kiếm từ khóa theo tên danh mục |
| `includeDisabled` | Query | `bool` | Không | Mặc định: `true` | Bao gồm cả danh mục đã bị tắt hoạt động |

### 2.2 Request Body Record DTO
- Không có Request Body (HTTP GET).

---

## 3. Đầu ra & Hợp đồng DTO thuần (Response Contract - Pure DTO)
> **LƯU Ý:** Trả về DTO thuần `PagedData<CategoryItemDto>` với mã HTTP `200 OK`.

- **Vị trí file:** `Catalog.Application/Services/Category/CategoryDtos.cs`
- **Tên C# Record:** `PagedData<CategoryItemDto>`
- **Mã HTTP thành công:** `200 OK`
- **Khai báo C# Record:**
```csharp
public sealed record CategoryItemDto(
    Guid CategoryId,
    Guid? ParentId,
    string Name,
    string? Description,
    short Level,
    int SortOrder,
    bool IsDisabled = false,
    List<CategoryItemDto>? Children = null)
{
    public bool IsActive => !IsDisabled;
}
```
- **JSON Response Body mẫu (camelCase):**
```json
{
  "items": [
    {
      "categoryId": "10000000-0000-0000-0000-000000000001",
      "parentId": null,
      "name": "Thiết Bị Điện Tử & Công Nghệ",
      "description": "Máy tính, điện thoại, phụ kiện công nghệ",
      "level": 1,
      "sortOrder": 1,
      "isDisabled": false,
      "isActive": true,
      "children": null
    },
    {
      "categoryId": "10000000-0000-0000-0000-000000000099",
      "parentId": null,
      "name": "Danh Mục Tạm Dừng Hoạt Động",
      "description": "Ngừng kinh doanh mùa vụ",
      "level": 1,
      "sortOrder": 99,
      "isDisabled": true,
      "isActive": false,
      "children": null
    }
  ],
  "totalCount": 2,
  "page": 1,
  "pageSize": 10,
  "totalPages": 1,
  "hasPreviousPage": false,
  "hasNextPage": false
}
```

---

## 4. Ma trận Mã lỗi RFC 9110 (Error Matrix)
| HTTP Status | Error Code | Message (Sentence case) | Điều kiện kích hoạt (Trigger Condition) |
| :---: | :--- | :--- | :--- |
| `401 Unauthorized` | `UNAUTHORIZED` | `Authentication is required to access this resource.` | Không gửi token hoặc token hết hạn |
| `403 Forbidden` | `FORBIDDEN` | `You do not have permission to access this resource.` | Người dùng không mang vai trò `admin` hoặc `staff` |
| `500 Internal Error` | `INTERNAL_SERVER_ERROR` | `An unexpected error occurred while processing the request.` | Lỗi không mong muốn trong quá trình truy vấn dữ liệu |

---

## 5. Kế hoạch Thực thi & Checklist Kiểm chứng (Pre-Code Plan & Checklist)
### 5.1 Các bước thực thi nghiệp vụ (Execution Flow Plan)
1. **Bước 1:** `CategoriesController.ListAdmin` xác thực role `admin` hoặc `staff` qua attribute `[RequireRole("admin", "staff")]`.
2. **Bước 2:** Xây dựng `CategoryListQuery` với `includeDisabled = true`.
3. **Bước 3:** Gọi `_categoryService.GetCategoriesAsync(query, ct)`.
4. **Bước 4:** Ánh xạ kết quả sang `PagedData<CategoryItemDto>` và trả về với HTTP `200 OK`.

### 5.2 Checklist hoàn thành
- [x] **Pre-Code:** Đặc tả đầy đủ hợp đồng query params và response DTO.
- [x] **Application:** `CategoryService.cs` hỗ trợ `CategoryListQuery.IncludeDisabled`.
- [x] **Api:** `CategoriesController.ListAdmin` áp dụng `[RequireRole("admin", "staff")]`.
- [x] **Verify:** `dotnet build Catalog.sln -c Release` thành công 100% với 0 lỗi.

