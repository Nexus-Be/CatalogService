# CATALOG SERVICE - GET /api/v1/admin/products

> **Feature / Use Case:** Quản trị viên và Nhân viên kiểm duyệt tìm kiếm, tra cứu toàn bộ sản phẩm trên hệ thống [CAT-50]  
> **Trạng thái:** ✅ VERIFIED  
> **Ngày tạo:** 2026-11-07  
> **Tác giả:** Nexus Team & AI Agent  

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `GET`
- **Route Endpoint:**
  - Route chính thức (Chuẩn REST): `/api/v1/admin/products`
  - Route tương thích YARP Gateway: `/api/v1/products/admin` (khớp pattern `/api/v1/products/{**catch-all}`)
- **Quyền hạn (RBAC Policy):** `[RequireRole("admin", "staff")]`
- **Header bắt buộc:**
  - `X-User-Roles: admin` hoặc `X-User-Roles: staff`
- **Controller:** `Catalog.Api/Controllers/Public/AdminProductsController.cs`
- **Application Contract:** `Catalog.Application/Services/Product/IProductService.cs` (`SearchAdminProductsAsync`)

---

## 2. Đầu vào & Tham số Truy vấn (Input & Query Parameters)

### 2.1 Query Parameters
| Tham số | Kiểu C# | Bắt buộc | Mặc định | Giới hạn / Ràng buộc | Mô tả |
| :--- | :--- | :---: | :---: | :--- | :--- |
| `page` | `int` | Không | `1` | $\ge 1$ | Số trang hiện tại |
| `pageSize` | `int` | Không | `20` | $1 \le \text{pageSize} \le 100$ | Số phần tử trên mỗi trang |
| `keyword` | `string?` | Không | `null` | Max 100 ký tự | Tìm kiếm theo Tên sản phẩm hoặc Thương hiệu (`brand`) |
| `status` | `ProductStatus?` | Không | `null` | `Draft`, `Active`, `Inactive`, `Auctioned` | Lọc theo trạng thái vòng đời sản phẩm |
| `statusReason` | `ProductInactiveReason?`| Không | `null` | `None`, `SellerPaused`, `AdminLocked`, `OutOfStock`, `Discontinued` | Lọc theo lý do dừng bán / khóa sản phẩm |
| `categoryId` | `Guid?` | Không | `null` | UUIDv4 | Lọc sản phẩm theo danh mục |
| `sellerId` | `Guid?` | Không | `null` | UUIDv4 | Tra cứu toàn bộ sản phẩm của một Seller cụ thể |
| `saleType` | `SaleType?` | Không | `null` | `Normal`, `Auction` | Lọc theo loại hình bán hàng (thương mại thường hoặc đấu giá) |
| `sortBy` | `string?` | Không | `createdat` | `createdat`, `name`, `status` | Trường sắp xếp |
| `sortDirection`| `string?` | Không | `desc` | `asc`, `desc` | Hướng sắp xếp |

---

## 3. Đầu ra & Hợp đồng DTO thuần (Response Contract - Pure DTO)

- **Mã HTTP thành công:** `200 OK`
- **Kiểu trả về:** `PagedData<AdminProductListItemDto>`
- **Khai báo C# Record:**
```csharp
public sealed record AdminProductListItemDto(
    Guid ProductId,
    Guid SellerId,
    Guid CategoryId,
    string CategoryName,
    string Name,
    string? Brand,
    string SaleType,
    string Status,
    string? StatusReason,
    decimal? MinPrice,
    decimal? MaxPrice,
    string? PrimaryImageUrl,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PublishedAt);
```

- **JSON Response mẫu (camelCase):**
```json
{
  "items": [
    {
      "productId": "77777777-7777-7777-7777-777777777777",
      "sellerId": "11111111-1111-1111-1111-111111111111",
      "categoryId": "22222222-2222-2222-2222-222222222222",
      "categoryName": "Tranh & Tác phẩm Nghệ thuật",
      "name": "Bức Họa Cổ Phong Cảnh Mùa Thu",
      "brand": "Nexus Art",
      "saleType": "Auction",
      "status": "Inactive",
      "statusReason": "AdminLocked",
      "minPrice": 5000000.00,
      "maxPrice": 5000000.00,
      "primaryImageUrl": "https://res.cloudinary.com/nexus/image/upload/v1/autumn-painting.jpg",
      "createdAt": "2026-11-06T14:00:00Z",
      "publishedAt": null
    }
  ],
  "pagination": {
    "pageIndex": 1,
    "pageSize": 20,
    "totalCount": 1,
    "totalPages": 1,
    "hasPreviousPage": false,
    "hasNextPage": false
  }
}
```

---

## 4. Ma trận Mã lỗi RFC 9110 (Error Matrix)
| HTTP Status | Error Code | Detail | Điều kiện phát sinh |
| :---: | :--- | :--- | :--- |
| `400 Bad Request` | `VAL_INVALID_PAGINATION` | Page must be greater than or equal to 1, and pageSize must be between 1 and 100. | `page < 1` hoặc `pageSize < 1` hoặc `pageSize > 100` |
| `401 Unauthorized` | `UNAUTHORIZED` | Authentication credentials were missing or invalid. | Thiếu token xác thực hoặc header định danh |
| `403 Forbidden` | `FORBIDDEN` | Access denied. Required role: admin, staff. | Người dùng không mang vai trò `admin` hoặc `staff` |
| `500 Internal Error` | `INTERNAL_SERVER_ERROR` | An unexpected error occurred. | Lỗi cơ sở dữ liệu hoặc hệ thống |

