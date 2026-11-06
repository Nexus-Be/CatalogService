# CATALOG SERVICE - GET /api/v1/products/{id}

> **Feature / Use Case:** Lấy thông tin chi tiết sản phẩm công khai theo ID [CAT-06]
> **Trạng thái:** ✅ VERIFIED
> **Ngày tạo:** 2026-11-01
> **Tác giả:** Nexus Team & AI Agent

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `GET`
- **Route Endpoint:** `/api/v1/products/{id}`
- **Quyền hạn (RBAC Policy):** `AllowAnonymous` (Khách vãng lai và người mua hàng công khai)
- **Quy tắc Sở hữu (Resource Ownership):** Không yêu cầu (sản phẩm công khai phải ở trạng thái `ACTIVE`)
- **Controller:** `Catalog.Api/Controllers/Public/ProductsController.cs`
- **Application Contract:** `Catalog.Application/Services/Product/IProductService.cs`

---

## 2. Đầu vào & Quy chuẩn Validation (Input & Validation Schema)
### 2.1 Route Parameters & Query Parameters
| Tham số | Vị trí | Kiểu C# | Bắt buộc | Ràng buộc / Giới hạn | Ghi chú |
| :--- | :--- | :--- | :---: | :--- | :--- |
| `id` | Route | `Guid` | Có | UUIDv4 format | Định danh duy nhất của sản phẩm cần lấy chi tiết |

### 2.2 Request Body Record DTO
- Endpoint `GET` không sử dụng Request Body.

---

## 3. Đầu ra & Hợp đồng DTO thuần (Response Contract - Pure DTO)
> **LƯU Ý:** CatalogService CHỈ trả về DTO thuần, KHÔNG bọc qua `ApiResponse<T>`. Vỏ bọc phản hồi được chuẩn hóa tự động tại API Gateway.

- **Vị trí file:** `Catalog.Application/Services/Product/ProductDtos.cs`
- **Tên C# Record:** `ProductDetailDto`
- **Mã HTTP thành công:** `200 OK`
- **Khai báo C# Record:**
```csharp
public sealed record ProductDetailCategoryDto(
    Guid CategoryId,
    Guid? ParentId,
    string Name,
    int Level);

public sealed record ProductDetailVariantDto(
    Guid VariantId,
    string SkuCode,
    string Name,
    decimal? Price,
    string Status,
    bool IsActive,
    IReadOnlyList<ProductVariantAttributeDto> Attributes);

public sealed record ProductDetailImageDto(
    Guid ImageId,
    string Url,
    string? AltText,
    bool IsActive,
    int SortOrder,
    Guid? VariantId);

public sealed record ProductDetailRatingDto(
    decimal? AvgRating,
    int ReviewCount);

public sealed record ProductDetailDto(
    Guid ProductId,
    Guid SellerId,
    string Name,
    string? Description,
    string? Brand,
    string SaleType,
    string Status,
    IReadOnlyList<ProductSpecificationDto>? Specifications,
    DateTimeOffset? PublishedAt,
    ProductDetailCategoryDto? Category,
    IReadOnlyList<ProductDetailVariantDto> Variants,
    IReadOnlyList<ProductDetailImageDto> Images,
    ProductDetailRatingDto Rating);
```
- **JSON Response Body mẫu (Pure DTO - camelCase):**
```json
{
  "productId": "77777777-7777-7777-7777-777777777777",
  "sellerId": "11111111-1111-1111-1111-111111111111",
  "name": "Bàn Phím Cơ Không Dây Nexus RGB",
  "description": "Bàn phím cơ Bluetooth 5.1 hotswap 3 modes kết nối.",
  "brand": "Nexus Tech",
  "saleType": "Regular",
  "status": "Active",
  "specifications": [
    { "name": "switch", "value": "Gateron Yellow" },
    { "name": "battery", "value": "4000mAh" }
  ],
  "publishedAt": "2026-11-01T10:00:00Z",
  "category": {
    "categoryId": "22222222-2222-2222-2222-222222222222",
    "parentId": null,
    "name": "Bàn Phím Cơ",
    "level": 1
  },
  "variants": [
    {
      "variantId": "33333333-3333-3333-3333-333333333333",
      "skuCode": "KB-NEXUS-01-WHITE",
      "name": "Màu Trắng - Gateron Yellow",
      "price": 1250000,
      "status": "Active",
      "isActive": true,
      "attributes": [
        { "attrName": "Color", "attrValue": "White" }
      ]
    }
  ],
  "images": [
    {
      "imageId": "44444444-4444-4444-4444-444444444444",
      "url": "https://res.cloudinary.com/nexus/image/upload/v1/kb-white.jpg",
      "altText": "Hình chụp trực diện bàn phím trắng",
      "isActive": true,
      "sortOrder": 0,
      "variantId": "33333333-3333-3333-3333-333333333333"
    }
  ],
  "rating": {
    "avgRating": 4.9,
    "reviewCount": 18
  }
}
```

---

## 4. Ma trận Mã lỗi RFC 9110 (Error Matrix)
| HTTP Status | Error Code | Message (Sentence case) | Điều kiện kích hoạt (Trigger Condition) |
| :---: | :--- | :--- | :--- |
| `400 Bad Request` | `VAL_INVALID_INPUT` | `Invalid product identifier format.` | Chuỗi `id` trong Route không đúng cấu trúc UUIDv4 |
| `404 Not Found` | `PRODUCT_NOT_FOUND` | `Product not found with the specified identifier.` | Không tồn tại sản phẩm, sản phẩm bị xóa mềm (`IsDeleted = true`) hoặc chưa `ACTIVE` |
| `500 Internal Error` | `INTERNAL_SERVER_ERROR` | `An unexpected error occurred while processing the request.` | Lỗi không mong muốn trong quá trình truy vấn hệ thống |

### 4.1 Bảng Ánh xạ Enum liên quan
| Tên Enum C# | Giá trị DB (Integer) | Giá trị API (PascalCase) | Ý nghĩa nghiệp vụ |
| :--- | :---: | :--- | :--- |
| `ProductStatus.Active` | `1` | `"Active"` | Chỉ cho phép truy xuất công khai sản phẩm đang Active |
| `SaleType.Regular` | `0` | `"Regular"` | Hàng thương mại bán lẻ cố định giá |
| `SaleType.Auction` | `1` | `"Auction"` | Sản phẩm phục vụ phiên đấu giá |
| `VariantStatus.Active` | `0` | `"Active"` | Biến thể đang hoạt động có thể chọn mua |

---

## 5. Kế hoạch Thực thi & Checklist Kiểm chứng (Pre-Code Plan & Checklist)
### 5.1 Các bước thực thi nghiệp vụ (Execution Flow Plan)
1. **Bước 1:** `ProductsController.GetById` nhận tham số `[FromRoute] Guid id`.
2. **Bước 2:** Gọi `_productService.GetDetailAsync(id, ct)`.
3. **Bước 3:** Truy vấn entity `Product` từ DbContext bằng `AsNoTracking()`, nạp kèm các quan hệ liên quan (`Category`, `Variants`, `Images`, `Attributes`).
4. **Bước 4:** Lọc chỉ hiển thị các biến thể và hình ảnh đang kích hoạt (`IsActive == true`) cho người mua.
5. **Bước 5:** Tổng hợp thông tin đánh giá (Rating) từ hệ thống Reviews hoặc bảng tổng hợp.
6. **Bước 6:** Trả về đối tượng `ProductDetailDto` với mã HTTP `200 OK`.

### 5.2 Checklist hoàn thành
- [x] **Pre-Code:** Đã khảo sát DTO `ProductDetailDto` và entity `Product` trong domain.
- [x] **Application:** Đã hiện thực hóa `GetDetailAsync` trong `ProductService.cs`.
- [x] **Api:** `ProductsController.GetById` trả Pure DTO với XML summary chuẩn 1 dòng.
- [x] **Verify:** `dotnet build Catalog.sln -c Release` thành công 100% với 0 cảnh báo và 0 lỗi.

