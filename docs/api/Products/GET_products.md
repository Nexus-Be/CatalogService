# CATALOG SERVICE - GET /api/v1/products

> **Feature / Use Case:** Tìm kiếm và duyệt danh sách sản phẩm công khai [CAT-05]
> **Trạng thái:** ✅ VERIFIED
> **Ngày tạo:** 2026-11-01
> **Tác giả:** Nexus Team & AI Agent

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `GET`
- **Route Endpoint:** `/api/v1/products`
- **Quyền hạn (RBAC Policy):** `AllowAnonymous` (Public - phục vụ khách mua hàng)
- **Quy tắc Sở hữu (Resource Ownership):** Không áp dụng (chỉ hiển thị sản phẩm trạng thái `ACTIVE`)
- **Controller:** `Catalog.Api/Controllers/Public/ProductsController.cs`
- **Application Contract:** `Catalog.Application/Services/Product/IProductService.cs`

---

## 2. Đầu vào & Quy chuẩn Validation (Input & Validation Schema)
### 2.1 Route Parameters & Query Parameters
| Tham số | Vị trí | Kiểu C# | Bắt buộc | Ràng buộc / Giới hạn | Ghi chú |
| :--- | :--- | :--- | :---: | :--- | :--- |
| `keyword` | Query | `string?` | Không | Max: 100 characters | Tìm theo tên, thương hiệu, mô tả |
| `categoryId` | Query | `Guid?` | Không | UUIDv4 format | Lọc theo danh mục sản phẩm |
| `minPrice` | Query | `decimal?` | Không | Min: 0 | Giá sàn |
| `maxPrice` | Query | `decimal?` | Không | Min: 0 | Giá trần |
| `saleType` | Query | `SaleType?` | Không | `Regular`, `Auction` | Hình thức bán |
| `sortBy` | Query | `string?` | Không | `name`, `price`, `rating`, `createdAt` / `newest` | Tiêu chí sắp xếp |
| `sortDirection`| Query | `string?` | Không | `asc`, `desc` (Default: `asc`) | Chiều sắp xếp tăng hoặc giảm |
| `page` | Query | `int` | Không | Min: 1, Default: 1 | Chỉ mục trang |
| `pageSize` | Query | `int` | Không | Range: 1 - 100, Default: 20 | Kích thước trang |

### 2.2 Request Body Record DTO
- Endpoint `GET` không sử dụng Request Body.

---

## 3. Đầu ra & Hợp đồng DTO thuần (Response Contract - Pure DTO)
- **Vị trí file:** `Catalog.Application/Services/Product/ProductDtos.cs`
- **Tên C# Record:** `PagedData<ProductListItemDto>`
- **Mã HTTP thành công:** `200 OK`
- **Khai báo C# Record:**
```csharp
public sealed record ProductListItemDto(
    Guid ProductId,
    string Name,
    string? Brand,
    string SaleType,
    string Status,
    decimal? MinPrice,
    decimal? MaxPrice,
    string? PrimaryImageUrl,
    decimal? AvgRating);
```
- **JSON Response Body mẫu (Pure DTO - camelCase):**
```json
{
  "items": [
    {
      "productId": "88888888-8888-8888-8888-888888888888",
      "name": "Áo Thun Cotton Nexus",
      "brand": "Nexus Fashion",
      "saleType": "Regular",
      "status": "Active",
      "minPrice": 150000,
      "maxPrice": 200000,
      "primaryImageUrl": "https://res.cloudinary.com/.../ao-thun.jpg",
      "avgRating": 4.8
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalItems": 1,
  "totalPages": 1
}
```

---

## 4. Ma trận Mã lỗi RFC 9110 (Error Matrix)
| HTTP Status | Error Code | Message (Sentence case) | Điều kiện kích hoạt (Trigger Condition) |
| :---: | :--- | :--- | :--- |
| `400 Bad Request` | `VAL_INVALID_INPUT` | `Minimum price cannot be greater than maximum price.` | `minPrice > maxPrice` |
| `500 Internal Error` | `INTERNAL_SERVER_ERROR` | `An unexpected error occurred while processing the request.` | Lỗi hệ thống |

### 4.1 Bảng Ánh xạ Enum liên quan
| Tên Enum C# | Giá trị DB (Integer) | Giá trị API (PascalCase) | Ý nghĩa nghiệp vụ |
| :--- | :---: | :--- | :--- |
| `ProductStatus.Active` | `1` | `"Active"` | Chỉ hiển thị các sản phẩm đã kích hoạt mở bán |
| `SaleType.Regular` | `0` | `"Regular"` | Bán giá cố định |
| `SaleType.Auction` | `1` | `"Auction"` | Bán qua đấu giá |

---

## 5. Kế hoạch Thực thi & Checklist Kiểm chứng (Pre-Code Plan & Checklist)
### 5.1 Các bước thực thi nghiệp vụ (Execution Flow Plan)
1. **Bước 1:** Bắt các query parameters tại `ProductsController.Search`.
2. **Bước 2:** Gọi `_productService.SearchAsync(query, ct)`.
3. **Bước 3:** Truy vấn PostgreSQL bảng `Products` lọc `Status == ProductStatus.Active` và `IsDeleted == false` (`.AsNoTracking()`).
4. **Bước 4:** Tính giá `MinPrice`/`MaxPrice` từ bảng `ProductVariants`, nạp `PrimaryImageUrl` từ bảng `ProductImages` (`IsActive == true`).
5. **Bước 5:** Phân trang tại cơ sở dữ liệu qua `.Skip().Take()` và `.CountAsync()`.
6. **Bước 6:** Trả về `Ok(pagedData)`.

### 5.2 Checklist hoàn thành
- [x] **Pre-Code:** Hoàn thiện đặc tả API.
- [x] **Application:** Đã cài đặt trong `ProductService.cs`.
- [x] **Api:** `ProductsController.Search` trả Pure DTO `PagedData<ProductListItemDto>`.
- [x] **Verify:** Build đạt 0 lỗi.

