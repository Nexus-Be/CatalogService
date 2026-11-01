# CATALOG SERVICE - [HTTP_METHOD] [ROUTE]

> **Feature / Use Case:** {FeatureName}
> **Trạng thái:** ⏳ DRAFT (Pre-Code Plan) | 🛠 IN_PROGRESS | ✅ VERIFIED
> **Ngày tạo:** YYYY-MM-DD
> **Tác giả:** {Author / AI Agent}

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `GET` | `POST` | `PUT` | `PATCH` | `DELETE`
- **Route Endpoint:** `/api/v1/products/...` | `/api/v1/categories/...` | `/api/v1/variants/...`
- **Quyền hạn (RBAC Policy):** `AllowAnonymous` | `Seller` | `StaffOrAdmin` | `Admin`
- **Quy tắc Sở hữu (Resource Ownership):** `sellerId == currentUserId` hoặc Admin bypass.
- **Controller:** `Catalog.Api/Controllers/Public/{Feature}Controller.cs`
- **Application Contract:** `Catalog.Application/Services/{Feature}/I{Feature}Service.cs`

---

## 2. Đầu vào & Quy chuẩn Validation (Input & Validation Schema)
### 2.1 Route Parameters & Query Parameters
| Tham số | Vị trí | Kiểu C# | Bắt buộc | Ràng buộc / Giới hạn | Ghi chú |
| :--- | :--- | :--- | :---: | :--- | :--- |
| `productId` | Route | `Guid` | Có | UUIDv4 format | Khóa chính sản phẩm |
| `page` | Query | `int` | Không | Min: 1, Default: 1 | Tham số phân trang chuẩn |
| `pageSize` | Query | `int` | Không | Range: 1 - 100, Default: 20 | Giới hạn tối đa 100 bản ghi |

### 2.2 Request Body Record DTO
- **Vị trí file:** `Catalog.Application/Services/{Feature}/{Feature}Requests.cs`
- **Tên C# Record:** `{ActionName}Request`
- **Khai báo C# & DataAnnotations:**
```csharp
public sealed record {ActionName}Request(
    [Required(ErrorMessage = "Product name is required.")]
    [StringLength(200, MinimumLength = 3, ErrorMessage = "Product name must be between 3 and 200 characters.")]
    string Name,

    [Required(ErrorMessage = "Category ID is required.")]
    Guid CategoryId,

    [Range(0, 1000000000, ErrorMessage = "Base price must be non-negative.")]
    decimal BasePrice
);
```
- **JSON Request Body mẫu (camelCase):**
```json
{
  "name": "Mechanical Keyboard Pro",
  "categoryId": "2bb85f64-5717-4562-b3fc-2c963f66afa1",
  "basePrice": 1250000.00
}
```

---

## 3. Đầu ra & Hợp đồng DTO thuần (Response Contract - Pure DTO)
> **LƯU Ý:** CatalogService CHỈ trả về DTO thuần hoặc `PagedData<T>`, KHÔNG bọc qua `ApiResponse<T>`. Vỏ `ApiResponse<T>` sẽ do API Gateway tự động đóng gói tại biên.

- **Vị trí file:** `Catalog.Application/Services/{Feature}/{Feature}Responses.cs`
- **Tên C# Record:** `{ActionName}Response` (hoặc `PagedData<{Item}Dto>`)
- **Mã HTTP thành công:** `200 OK` | `201 Created` | `204 NoContent`
- **Khai báo C# Record:**
```csharp
public sealed record {ActionName}Response(
    Guid Id,
    string Name,
    decimal BasePrice,
    string Status, // PascalCase enum string
    DateTimeOffset CreatedAt
);
```
- **JSON Response Body mẫu (Pure DTO - camelCase):**
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "name": "Mechanical Keyboard Pro",
  "basePrice": 1250000.00,
  "status": "Active",
  "createdAt": "2026-11-01T14:30:00Z"
}
```

---

## 4. Ma trận Mã lỗi RFC 9110 (Error Matrix)
> **Quy chuẩn:** 100% tiếng Anh, Sentence case cho `message`, SCREAMING_SNAKE_CASE cho `errorCode` (không dùng tiền tố `ERR_`).

| HTTP Status | Error Code | Message (Sentence case) | Điều kiện kích hoạt (Trigger Condition) |
| :---: | :--- | :--- | :--- |
| `400 Bad Request` | `VAL_INVALID_INPUT` | `One or more validation errors occurred.` | Vi phạm DataAnnotations trên request model |
| `401 Unauthorized` | `UNAUTHORIZED` | `Authentication is required to access this resource.` | Thiếu hoặc token không hợp lệ |
| `403 Forbidden` | `FORBIDDEN` | `You do not have permission to access or modify this resource.` | Người bán không sở hữu sản phẩm |
| `404 Not Found` | `PRODUCT_NOT_FOUND` | `Product not found with the specified identifier.` | Không tìm thấy sản phẩm trong DB |
| `404 Not Found` | `CATEGORY_NOT_FOUND` | `Category not found with the specified identifier.` | Không tìm thấy danh mục liên kết |
| `500 Internal Error` | `INTERNAL_SERVER_ERROR` | `An unexpected error occurred while processing the request.` | Ngoại lệ chưa được xử lý |

### 4.1 Bảng Ánh xạ Enum liên quan (Nếu có)
| Tên Enum C# | Giá trị DB (Integer) | Giá trị API (PascalCase) | Ý nghĩa nghiệp vụ |
| :--- | :---: | :--- | :--- |
| `ProductStatus.Draft` | `0` | `"Draft"` | Bản nháp chưa hiển thị |
| `ProductStatus.Active` | `1` | `"Active"` | Đang mở bán công khai |
| `ProductStatus.Archived` | `2` | `"Archived"` | Đã lưu trữ / ngừng kinh doanh |

---

## 5. Kế hoạch Thực thi & Checklist Kiểm chứng (Pre-Code Plan & Checklist)
### 5.1 Các bước thực thi nghiệp vụ (Execution Flow Plan)
1. **Bước 1:** Validate tham số đầu vào tại Controller boundary.
2. **Bước 2:** Lấy `sellerId` từ `HttpContext.GetUserId()` và kiểm tra quyền sở hữu sản phẩm.
3. **Bước 3:** Truy vấn entity từ DbContext qua `DbSet<T>`.
4. **Bước 4:** Kiểm tra logic nghiệp vụ, ném Domain Exception nếu vi phạm điều kiện.
5. **Bước 5:** Thực hiện thao tác nghiệp vụ, commit transaction nếu có thay đổi DB.
6. **Bước 6:** Trả về Pure DTO với mã HTTP phù hợp (`200`, `201`, `204`).

### 5.2 Checklist hoàn thành
- [ ] **Pre-Code:** Đã đối soát code có sẵn trong CatalogService để tái sử dụng DTO/Enum/Naming.
- [ ] **Pre-Code:** Tài liệu API nội bộ này đã được viết và lưu tại `docs/api/{Feature}/{METHOD}_{ENDPOINT}.md`.
- [ ] **Persistence:** Entity & EF Core Configuration đã hoàn thiện (nếu có thay đổi schema).
- [ ] **Application:** Interface, Service, Request/Response DTOs đã viết xong trong `Services/{Feature}/`.
- [ ] **Api:** Thin Controller đã ghép cặp 1-to-1, trả DTO thuần, XML summary 1 dòng.
- [ ] **Tests:** Đã viết unit/integration tests bao phủ happy path và các mã lỗi trong Error Matrix.
- [ ] **Verify:** `dotnet build Catalog.sln -c Release` vượt qua 100% với 0 lỗi.
