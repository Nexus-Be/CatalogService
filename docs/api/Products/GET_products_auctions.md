# CATALOG SERVICE - GET /api/v1/products/auctions

> **Feature / Use Case:** Liệt kê danh sách sản phẩm phục vụ đấu giá công khai có phân trang và lọc [CAT-46]  
> **Trạng thái:** ✅ VERIFIED  
> **Ngày tạo:** 2026-11-07  
> **Tác giả:** Nexus Team & AI Agent  

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `GET`
- **Route Endpoint:** `/api/v1/products/auctions`
- **Quyền hạn (RBAC Policy):** `AllowAnonymous` (Công khai cho toàn bộ khách hàng và Auction Service)
- **Cơ chế Gateway YARP:** Khớp pattern `"/api/v1/products/{**catch-all}"` chuyển tiếp thẳng vào `catalog-cluster` (Port `8002`). Không gây xung đột với `auction-cluster`.
- **Controller:** `Catalog.Api/Controllers/Public/ProductsController.cs`
- **Application Contract:** `Catalog.Application/Services/Product/IProductService.cs` (`SearchAuctionsAsync`)

---

## 2. Đầu vào & Tham số Truy vấn (Input & Query Parameters)

### 2.1 Query Parameters
| Tham số | Kiểu C# | Bắt buộc | Mặc định | Giới hạn / Ràng buộc | Mô tả |
| :--- | :--- | :---: | :---: | :--- | :--- |
| `page` | `int` | Không | `1` | $\ge 1$ | Số trang hiện tại |
| `pageSize` | `int` | Không | `20` | $1 \le \text{pageSize} \le 100$ | Số phần tử trên mỗi trang |
| `keyword` | `string?` | Không | `null` | Max 100 ký tự | Tìm kiếm không dấu theo Tên hoặc Thương hiệu hiện vật |
| `categoryId` | `Guid?` | Không | `null` | UUIDv4 | Lọc theo danh mục sản phẩm |
| `sellerId` | `Guid?` | Không | `null` | UUIDv4 | Lọc sản phẩm đấu giá theo nhà bán hàng |
| `status` | `string?` | Không | `Active` | `Active`, `Auctioned`, `all` | Mặc định chỉ hiển thị sản phẩm `Active`. Truyền `all` để xem cả đồ đã đấu giá xong |
| `sortBy` | `string?` | Không | `newest` | `newest`, `oldest` | Sắp xếp theo ngày đăng |

---

## 3. Đầu ra & Hợp đồng DTO thuần (Response Contract - Pure DTO)

- **Mã HTTP thành công:** `200 OK`
- **Kiểu trả về:** `PagedData<AuctionProductListItemDto>`
- **Khai báo C# Record:**
```csharp
public sealed record AuctionProductListItemDto(
    Guid ProductId,
    Guid SellerId,
    Guid CategoryId,
    string Name,
    string? Brand,
    string SkuCode,
    string Status,
    string? PrimaryImageUrl,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PublishedAt);
```

- **JSON Response mẫu (camelCase):**
```json
{
  "items": [
    {
      "productId": "88888888-8888-8888-8888-888888888888",
      "sellerId": "11111111-1111-1111-1111-111111111111",
      "categoryId": "22222222-2222-2222-2222-222222222222",
      "name": "Bình Hoa Gốm Cổ Chu Đậu Thế Kỷ 15",
      "brand": "Chu Đậu",
      "skuCode": "AUC-CERAMIC-15TH-001",
      "status": "Active",
      "primaryImageUrl": "https://res.cloudinary.com/nexus/image/upload/v1/chudau-vase.jpg",
      "createdAt": "2026-11-06T10:00:00Z",
      "publishedAt": "2026-11-06T10:15:00Z"
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
| `500 Internal Error` | `INTERNAL_SERVER_ERROR` | An unexpected error occurred. | Lỗi cơ sở dữ liệu hoặc hệ thống |

