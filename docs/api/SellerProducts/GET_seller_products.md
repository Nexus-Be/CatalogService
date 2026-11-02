# CATALOG SERVICE - GET /api/v1/seller/products

> **Feature / Use Case:** Tìm kiếm và duyệt danh sách sản phẩm thuộc quyền sở hữu của người bán [CAT-44] (Bao gồm sản phẩm ở mọi trạng thái: DRAFT, ACTIVE, INACTIVE)
> **Trạng thái:** ✅ VERIFIED
> **Ngày tạo:** 2026-11-01
> **Tác giả:** Nexus Team & AI Agent

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `GET`
- **Route Endpoint:** `/api/v1/seller/products`
- **Quyền hạn (RBAC Policy):** `RequireRole("seller", "admin")`
- **Quy tắc Sở hữu (Resource Ownership):** Tự động lọc danh sách sản phẩm theo User ID của người bán đang đăng nhập (`p.SellerId == currentUserId`).
- **Controller:** `Catalog.Api/Controllers/Public/SellerProductsController.cs`
- **Application Contract:** `Catalog.Application/Services/Product/IProductService.cs`

---

## 2. Đầu vào & Quy chuẩn Validation (Input & Validation Schema)
### 2.1 Route Parameters & Query Parameters
| Tham số | Vị trí | Kiểu C# | Bắt buộc | Ràng buộc / Giới hạn | Ghi chú |
| :--- | :--- | :--- | :---: | :--- | :--- |
| `page` | Query | `int` | Không | Min: 1, Default: 1 | Chỉ mục trang |
| `pageSize` | Query | `int` | Không | Range: 1 - 100, Default: 20 | Kích thước trang |
| `keyword` | Query | `string?` | Không | Max: 100 characters | Tìm theo tên hoặc thương hiệu |
| `status` | Query | `ProductStatus?`| Không | `Draft`, `Active`, `Inactive` | Lọc theo trạng thái kinh doanh |
| `categoryId`| Query | `Guid?` | Không | UUIDv4 format | Lọc theo danh mục sản phẩm |
| `saleType` | Query | `SaleType?` | Không | `Regular`, `Auction` | Lọc theo hình thức bán |
| `sortBy` | Query | `string?` | Không | `name`, `price`, `createdAt`, `status` | Tiêu chí sắp xếp |
| `sortDirection`| Query | `string?` | Không | `asc`, `desc` (Default: `asc`) | Chiều sắp xếp tăng hoặc giảm |

### 2.2 Request Body Record DTO
- Endpoint `GET` không sử dụng Request Body.

---

## 3. Đầu ra & Hợp đồng DTO thuần (Response Contract - Pure DTO)
- **Vị trí file:** `Catalog.Application/Services/Product/ProductDtos.cs`
- **Tên C# Record:** `PagedData<SellerProductListItemDto>`
- **Mã HTTP thành công:** `200 OK`
- **Khai báo C# Record:**
```csharp
public sealed record SellerProductListItemDto(
    Guid ProductId,
    string Name,
    string? Brand,
    Guid CategoryId,
    string? CategoryName,
    string SaleType,
    string Status,
    string? StatusReason,
    decimal? MinPrice,
    decimal? MaxPrice,
    string? PrimaryImageUrl,
    int VariantCount,
    bool HasOrder,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PublishedAt);
```
- **JSON Response Body mẫu (camelCase):**
```json
{
  "items": [
    {
      "productId": "77777777-7777-7777-7777-777777777777",
      "name": "Bàn Phím Cơ Không Dây Nexus RGB",
      "brand": "Nexus Tech",
      "categoryId": "22222222-2222-2222-2222-222222222222",
      "categoryName": "Bàn Phím Cơ",
      "saleType": "Regular",
      "status": "Active",
      "statusReason": null,
      "minPrice": 1250000,
      "maxPrice": 1350000,
      "primaryImageUrl": "https://res.cloudinary.com/nexus/image/upload/v1/products/kb-primary.webp",
      "variantCount": 2,
      "hasOrder": true,
      "createdAt": "2026-11-01T08:00:00Z",
      "publishedAt": "2026-11-01T10:00:00Z"
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
| `400 Bad Request` | `VAL_INVALID_INPUT` | `Invalid query parameters provided.` | Tham số phân trang âm hoặc không hợp lệ |
| `401 Unauthorized` | `UNAUTHORIZED` | `Authentication is required to access this resource.` | Thiếu Bearer token |
| `403 Forbidden` | `FORBIDDEN` | `You do not have permission to access seller resources.` | User không sở hữu role `seller` hoặc `admin` |
| `500 Internal Error` | `INTERNAL_SERVER_ERROR` | `An unexpected error occurred while processing the request.` | Lỗi không mong muốn |

### 4.1 Bảng Ánh xạ Enum liên quan
| Tên Enum C# | Giá trị DB (Integer) | Giá trị API (PascalCase) | Ý nghĩa nghiệp vụ |
| :--- | :---: | :--- | :--- |
| `ProductStatus.Draft` | `0` | `"Draft"` | Bản nháp của người bán |
| `ProductStatus.Active` | `1` | `"Active"` | Đang mở bán công khai |
| `ProductStatus.Inactive` | `2` | `"Inactive"` | Đang tạm khóa / hết hàng |

---

## 5. Kế hoạch Thực thi & Checklist Kiểm chứng (Pre-Code Plan & Checklist)
### 5.1 Các bước thực thi nghiệp vụ (Execution Flow Plan)
1. **Bước 1:** `SellerProductsController.SearchSellerProducts` nhận query parameters.
2. **Bước 2:** Lấy `currentUserId` từ JWT qua `TryGetUserId`.
3. **Bước 3:** Gọi `_productService.SearchSellerProductsAsync(currentUserId, query, ct)`.
4. **Bước 4:** Truy vấn database lọc theo `SellerId == currentUserId` và `IsDeleted == false` (`.AsNoTracking()`).
5. **Bước 5:** Nạp kèm Category, Variants và Images để tính toán `MinPrice`/`MaxPrice`, `VariantCount`, `HasOrder`.
6. **Bước 6:** Thực hiện phân trang trực tiếp trên database qua `.Skip().Take()` và `.CountAsync()`.
7. **Bước 7:** Trả về Pure DTO `PagedData<SellerProductListItemDto>` với mã HTTP `200 OK`.

### 5.2 Checklist hoàn thành
- [x] **Pre-Code:** Đặc tả bộ lọc dành riêng cho người bán (hỗ trợ hiển thị cả trạng thái nháp).
- [x] **Application:** Triển khai phương thức `SearchSellerProductsAsync` trong `ProductService.cs`.
- [x] **Api:** `SellerProductsController.SearchSellerProducts` trả Pure DTO.
- [x] **Verify:** `dotnet build Catalog.sln -c Release` thành công 100% với 0 lỗi.

