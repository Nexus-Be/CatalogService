# CATALOG SERVICE - GET /api/v1/seller/products/auctions

> **Feature / Use Case:** Người bán (Seller) quản lý danh sách hiện vật đấu giá của chính mình theo mọi trạng thái [CAT-48]  
> **Trạng thái:** ✅ VERIFIED  
> **Ngày tạo:** 2026-11-07  
> **Tác giả:** Nexus Team & AI Agent  

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `GET`
- **Route Endpoint:** `/api/v1/seller/products/auctions`
- **Quyền hạn (RBAC Policy):** `RequireRole("seller", "admin")`
- **Cơ chế Gateway YARP:** Khớp pattern `"/api/v1/seller/products/{**catch-all}"` chuyển tiếp thẳng vào `catalog-cluster` (Port `8002`).
- **Controller:** `Catalog.Api/Controllers/Public/SellerProductsController.cs`
- **Application Contract:** `Catalog.Application/Services/Product/IProductService.cs` (`SearchSellerAuctionsAsync`)

---

## 2. Đầu vào & Tham số (Input Parameters)

### 2.1 Request Headers (Bắt buộc)
| Header | Kiểu | Bắt buộc | Mô tả |
| :--- | :--- | :---: | :--- |
| `x-user-id` | `string` (UUIDv4) | Có | ID của người bán đang đăng nhập |
| `x-user-roles` | `string` | Có | Phải chứa vai trò `seller` hoặc `admin` |

### 2.2 Query Parameters
| Tham số | Kiểu C# | Bắt buộc | Mặc định | Mô tả |
| :--- | :--- | :---: | :---: | :--- |
| `page` | `int` | Không | `1` | Số trang |
| `pageSize` | `int` | Không | `20` | Số lượng phần tử mỗi trang |
| `keyword` | `string?` | Không | `null` | Tìm kiếm theo tên hoặc thương hiệu |
| `categoryId` | `Guid?` | Không | `null` | Lọc theo danh mục |
| `status` | `string?` | Không | `null` | Lọc theo trạng thái (`Draft`, `Active`, `Auctioned`, `Inactive`). Bỏ trống để lấy tất cả |
| `sortBy` | `string?` | Không | `newest` | `newest`, `oldest` |

---

## 3. Đầu ra & Hợp đồng DTO thuần (Response Contract - Pure DTO)

- **Mã HTTP thành công:** `200 OK`
- **Kiểu trả về:** `PagedData<AuctionProductListItemDto>`
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
      "status": "Draft",
      "primaryImageUrl": "https://res.cloudinary.com/nexus/image/upload/v1/chudau-vase.jpg",
      "createdAt": "2026-11-06T10:00:00Z",
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
| `401 Unauthorized` | `UNAUTHORIZED` | Authentication is required to access this resource. | Thiếu hoặc sai header `x-user-id` |
| `403 Forbidden` | `FORBIDDEN` | You do not have permission to access or modify this resource. | Người dùng không có vai trò `seller` hoặc `admin` |
| `500 Internal Error` | `INTERNAL_SERVER_ERROR` | An unexpected error occurred. | Lỗi cơ sở dữ liệu hoặc hệ thống |

