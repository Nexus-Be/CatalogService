# CATALOG SERVICE - GET /api/v1/categories

> **Feature / Use Case:** Duyệt cây danh mục đa cấp và tìm kiếm danh mục công khai (Hỗ trợ phân trang pageSize = 10 và sắp xếp tăng/giảm)
> **Trạng thái:** ✅ VERIFIED
> **Ngày tạo:** 2026-11-01
> **Tác giả:** Nexus Team & AI Agent

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `GET`
- **Route Endpoint:** `/api/v1/categories`
- **Quyền hạn (RBAC Policy):** `AllowAnonymous` (Khách vãng lai, Buyer và Seller đều truy cập công khai)
- **Quy tắc Sở hữu (Resource Ownership):** Không yêu cầu
- **Controller:** `Catalog.Api/Controllers/Public/CategoriesController.cs`
- **Application Contract:** `Catalog.Application/Services/Category/ICategoryService.cs`

---

## 2. Đầu vào & Quy chuẩn Validation (Input & Validation Schema)
### 2.1 Route Parameters & Query Parameters
| Tham số | Vị trí | Kiểu C# | Bắt buộc | Ràng buộc / Giới hạn | Ghi chú |
| :--- | :--- | :--- | :---: | :--- | :--- |
| `page` | Query | `int` | Không | Min: 1, Default: 1 | Chỉ mục trang |
| `pageSize` | Query | `int` | Không | Range: 1 - 100, Default: 10 | Kích thước trang chuẩn là 10 |
| `sortBy` | Query | `string?` | Không | `sortOrder`, `name`, `level`, `createdAt` | Tiêu chí sắp xếp danh mục |
| `sortDirection`| Query | `string?` | Không | `asc`, `desc` (Default: `asc`) | Chiều sắp xếp tăng hoặc giảm |
| `parentId` | Query | `Guid?` | Không | UUIDv4 format | Lọc danh mục con trực tiếp thuộc cha này |
| `tree` | Query | `bool` | Không | Default: `false` | `true`: phân trang theo cây danh mục gốc và nhúng danh mục con |
| `keyword` | Query | `string?` | Không | Max: 100 characters | Tìm kiếm không phân biệt dấu tiếng Việt (`unaccent`) theo tên hoặc mô tả danh mục |

### 2.2 Request Body Record DTO
- Endpoint phương thức `GET` không sử dụng Request Body.

---

## 3. Đầu ra & Hợp đồng DTO thuần (Response Contract - Pure DTO)
> **LƯU Ý:** CatalogService CHỈ trả về DTO thuần hoặc `PagedData<T>`, KHÔNG bọc qua `ApiResponse<T>`. Vỏ `ApiResponse<T>` sẽ do API Gateway tự động đóng gói tại biên.

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
    List<CategoryItemDto>? Children = null);
```
- **JSON Response Body mẫu (Pure DTO - camelCase):**
```json
{
  "items": [
    {
      "categoryId": "10000000-0000-0000-0000-000000000001",
      "parentId": null,
      "name": "Thời Trang",
      "description": "Quần áo thời trang nam nữ",
      "level": 1,
      "sortOrder": 1,
      "children": [
        {
          "categoryId": "10000000-0000-0000-0000-000000000002",
          "parentId": "10000000-0000-0000-0000-000000000001",
          "name": "Thời Trang Nam",
          "description": "Thời trang dành cho phái nam",
          "level": 2,
          "sortOrder": 1,
          "children": []
        }
      ]
    }
  ],
  "pagination": {
    "pageIndex": 1,
    "pageSize": 10,
    "totalCount": 5,
    "totalPages": 1,
    "hasPreviousPage": false,
    "hasNextPage": false
  }
}
```

---

## 4. Ma trận Mã lỗi RFC 9110 (Error Matrix)
| HTTP Status | Error Code | Message (Sentence case) | Điều kiện kích hoạt (Trigger Condition) |
| :---: | :--- | :--- | :--- |
| `400 Bad Request` | `VAL_INVALID_INPUT` | `Invalid query parameter format.` | Tham số `parentId` không đúng định dạng GUID hoặc phân trang âm |
| `500 Internal Error` | `INTERNAL_SERVER_ERROR` | `An unexpected error occurred while processing the request.` | Ngoại lệ hệ thống chưa được bắt |

---

## 5. Kế hoạch Thực thi & Checklist Kiểm chứng (Pre-Code Plan & Checklist)
### 5.1 Các bước thực thi nghiệp vụ (Execution Flow Plan)
1. **Bước 1:** `CategoriesController.List` nhận `page`, `pageSize = 10`, `sortBy`, `sortDirection`, `parentId`, `tree`, `keyword`.
2. **Bước 2:** Gọi `_categoryService.GetCategoriesAsync(query, ct)`.
3. **Bước 3:** Lọc danh mục chưa xóa mềm (`!IsDeleted`) và đang kích hoạt (`!IsDisabled`). Nếu có `keyword`, áp dụng tìm kiếm không dấu `EF.Functions.Unaccent(...)` trên `Name` và `Description`.
4. **Bước 4:** Sắp xếp theo `sortBy` (`name`, `sortOrder`, `level`, `createdAt`) và hướng `sortDirection` (`asc`/`desc`).
5. **Bước 5:** Phân trang tại database với kích thước trang chuẩn là 10.
6. **Bước 6:** Trả về `PagedData<CategoryItemDto>` với mã HTTP `200 OK`.

### 5.2 Checklist hoàn thành
- [x] **Pre-Code:** Đặc tả phân trang pageSize = 10 và chiều sắp xếp tăng/giảm.
- [x] **Application:** Triển khai trong `CategoryService.cs`.
- [x] **Api:** `CategoriesController.List` trả Pure DTO `PagedData<CategoryItemDto>`.
- [x] **Verify:** Đã kiểm tra logic.
