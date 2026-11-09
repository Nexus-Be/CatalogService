# CATALOG SERVICE - GET /api/v1/products/auctions/{id}

> **Feature / Use Case:** Xem chi tiết một sản phẩm đấu giá với DTO phẳng, unpriced SKU và thông số giám định [CAT-47]  
> **Trạng thái:** ✅ VERIFIED  
> **Ngày tạo:** 2026-11-07  
> **Tác giả:** Nexus Team & AI Agent  

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `GET`
- **Route Endpoint:** `/api/v1/products/auctions/{id:guid}`
- **Quyền hạn (RBAC Policy):** `AllowAnonymous` (Công khai cho khách hàng và Auction Service)
- **Cơ chế Gateway YARP:** Khớp pattern `"/api/v1/products/{**catch-all}"` chuyển tiếp thẳng vào `catalog-cluster` (Port `8002`).
- **Controller:** `Catalog.Api/Controllers/Public/ProductsController.cs`
- **Application Contract:** `Catalog.Application/Services/Product/IProductService.cs` (`GetAuctionDetailAsync`)

---

## 2. Đầu vào & Tham số (Input Parameters)

### 2.1 Route Parameters
| Tham số | Kiểu C# | Bắt buộc | Ràng buộc | Mô tả |
| :--- | :--- | :---: | :--- | :--- |
| `id` | `Guid` | Có | UUIDv4 format | Định danh duy nhất (`ProductId`) của sản phẩm đấu giá |

---

## 3. Đầu ra & Hợp đồng DTO thuần (Response Contract - Pure DTO)

- **Mã HTTP thành công:** `200 OK`
- **Kiểu trả về:** `AuctionProductDetailDto` (DTO phẳng, **không có mảng variants cồng kềnh**)
- **Khai báo C# Record:**
```csharp
public sealed record AuctionProductDetailDto(
    Guid ProductId,
    Guid SellerId,
    Guid CategoryId,
    string CategoryName,
    string Name,
    string? Description,
    string? Brand,
    string Status,
    string SkuCode,
    Guid VariantId,
    IReadOnlyList<ProductSpecificationDto>? Specifications,
    IReadOnlyList<ProductImageDto> Images,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PublishedAt,
    int? WeightGrams = null);
```

- **JSON Response mẫu (camelCase):**
```json
{
  "productId": "88888888-8888-8888-8888-888888888888",
  "sellerId": "11111111-1111-1111-1111-111111111111",
  "categoryId": "22222222-2222-2222-2222-222222222222",
  "categoryName": "Đồ Gốm Sưu Tầm",
  "name": "Bình Hoa Gốm Cổ Chu Đậu Thế Kỷ 15",
  "description": "Hiện vật gốm sứ hoa lam men rạn nguyên bản có chứng thư giám định cổ vật.",
  "brand": "Chu Đậu",
  "status": "Active",
  "skuCode": "AUC-CERAMIC-15TH-001",
  "variantId": "77777777-7777-7777-7777-777777777777",
  "specifications": [
    { "name": "Chứng thư giám định", "value": "GIA-998811" },
    { "name": "Niên đại", "value": "Thế kỷ 15" },
    { "name": "Tình trạng", "value": "Nguyên bản 98%" }
  ],
  "images": [
    {
      "id": "66666666-6666-6666-6666-666666666666",
      "productId": "88888888-8888-8888-8888-888888888888",
      "variantId": "77777777-7777-7777-7777-777777777777",
      "url": "https://res.cloudinary.com/nexus/image/upload/v1/chudau-vase.jpg",
      "altText": "Bình gốm góc chính diện",
      "isActive": true,
      "sortOrder": 0
    }
  ],
  "createdAt": "2026-11-06T10:00:00Z",
  "publishedAt": "2026-11-06T10:15:00Z"
}
```

---

## 4. Ma trận Mã lỗi RFC 9110 (Error Matrix)
| HTTP Status | Error Code | Detail | Điều kiện phát sinh |
| :---: | :--- | :--- | :--- |
| `404 Not Found` | `AUCTION_NOT_FOUND` | AuctionProduct with ID '{id}' was not found in the system. | Không tìm thấy sản phẩm, hoặc sản phẩm đã bị xóa, hoặc không phải loại `SaleType.Auction` |
| `500 Internal Error` | `INTERNAL_SERVER_ERROR` | An unexpected error occurred. | Lỗi cơ sở dữ liệu hoặc hệ thống |

