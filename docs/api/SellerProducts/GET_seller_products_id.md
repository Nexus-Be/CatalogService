# CATALOG SERVICE - GET /api/v1/seller/products/{id}

> **Feature / Use Case:** Lấy thông tin chi tiết toàn diện của một sản phẩm dành cho người bán [CAT-45] (Hiển thị đầy đủ tất cả SKU, tình trạng đơn hàng `hasOrder`, ảnh và lý do trạng thái)
> **Trạng thái:** ✅ VERIFIED
> **Ngày tạo:** 2026-11-01
> **Tác giả:** Nexus Team & AI Agent

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `GET`
- **Route Endpoint:** `/api/v1/seller/products/{id}`
- **Quyền hạn (RBAC Policy):** `RequireRole("seller", "admin")`
- **Quy tắc Sở hữu (Resource Ownership):** Bắt buộc sản phẩm phải thuộc quyền sở hữu của người bán đang đăng nhập (`product.SellerId == currentUserId`). Admin có quyền can thiệp kiểm tra.
- **Controller:** `Catalog.Api/Controllers/Public/SellerProductsController.cs`
- **Application Contract:** `Catalog.Application/Services/Product/IProductService.cs`

---

## 2. Đầu vào & Quy chuẩn Validation (Input & Validation Schema)
### 2.1 Route Parameters & Query Parameters
| Tham số | Vị trí | Kiểu C# | Bắt buộc | Ràng buộc / Giới hạn | Ghi chú |
| :--- | :--- | :--- | :---: | :--- | :--- |
| `id` | Route | `Guid` | Có | UUIDv4 format | Khóa chính của sản phẩm cần lấy chi tiết quản trị |

### 2.2 Request Body Record DTO
- Endpoint `GET` không sử dụng Request Body.

---

## 3. Đầu ra & Hợp đồng DTO thuần (Response Contract - Pure DTO)
- **Vị trí file:** `Catalog.Application/Services/Product/ProductDtos.cs`
- **Tên C# Record:** `SellerProductDetailDto`
- **Mã HTTP thành công:** `200 OK`
- **Khai báo C# Record:**
```csharp
public sealed record SellerProductVariantDto(
    Guid VariantId,
    string SkuCode,
    string Name,
    decimal? Price,
    string Currency,
    string Status,
    string? StatusReason,
    bool IsActive,
    bool HasOrder,
    IReadOnlyList<ProductVariantAttributeDto> Attributes,
    IReadOnlyList<ProductImageDto> Images);

public sealed record SellerProductDetailDto(
    Guid ProductId,
    Guid SellerId,
    Guid CategoryId,
    string? CategoryName,
    string Name,
    string? Description,
    string? Brand,
    string? Specifications,
    string SaleType,
    string Status,
    string? StatusReason,
    bool HasOrder,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    DateTimeOffset? PublishedAt,
    IReadOnlyList<ProductImageDto> Images,
    IReadOnlyList<SellerProductVariantDto> Variants);
```
- **JSON Response Body mẫu (Pure DTO - camelCase):**
```json
{
  "productId": "77777777-7777-7777-7777-777777777777",
  "sellerId": "11111111-1111-1111-1111-111111111111",
  "categoryId": "22222222-2222-2222-2222-222222222222",
  "categoryName": "Bàn Phím Cơ",
  "name": "Bàn Phím Cơ Không Dây Nexus RGB",
  "description": "Bàn phím cơ Bluetooth 5.1 hotswap 3 modes kết nối.",
  "brand": "Nexus Tech",
  "specifications": "{\"switch\": \"Gateron Yellow\", \"battery\": \"4000mAh\"}",
  "saleType": "Regular",
  "status": "Active",
  "statusReason": null,
  "hasOrder": true,
  "createdAt": "2026-11-01T08:00:00Z",
  "updatedAt": "2026-11-01T09:30:00Z",
  "publishedAt": "2026-11-01T10:00:00Z",
  "images": [
    {
      "id": "44444444-4444-4444-4444-444444444444",
      "productId": "77777777-7777-7777-7777-777777777777",
      "variantId": null,
      "url": "https://res.cloudinary.com/nexus/image/upload/v1/products/kb-primary.webp",
      "altText": "Ảnh bìa chính",
      "isActive": true,
      "sortOrder": 0
    }
  ],
  "variants": [
    {
      "variantId": "33333333-3333-3333-3333-333333333333",
      "skuCode": "KB-NEXUS-01-WHITE",
      "name": "Màu Trắng - Gateron Yellow",
      "price": 1250000,
      "currency": "VND",
      "status": "Active",
      "statusReason": null,
      "isActive": true,
      "hasOrder": true,
      "attributes": [
        { "attrName": "Color", "attrValue": "White" }
      ],
      "images": []
    }
  ]
}
```

---

## 4. Ma trận Mã lỗi RFC 9110 (Error Matrix)
| HTTP Status | Error Code | Message (Sentence case) | Điều kiện kích hoạt (Trigger Condition) |
| :---: | :--- | :--- | :--- |
| `400 Bad Request` | `VAL_INVALID_INPUT` | `Invalid product identifier format.` | Chuỗi `id` trong route không đúng định dạng UUIDv4 |
| `401 Unauthorized` | `UNAUTHORIZED` | `Authentication is required to access this resource.` | Không có Bearer token hợp lệ |
| `403 Forbidden` | `FORBIDDEN` | `You do not have permission to view details of this product.` | Người bán không sở hữu sản phẩm này |
| `404 Not Found` | `PRODUCT_NOT_FOUND` | `Product not found with the specified identifier.` | Sản phẩm không tồn tại hoặc đã bị xóa mềm |
| `500 Internal Error` | `INTERNAL_SERVER_ERROR` | `An unexpected error occurred while processing the request.` | Lỗi không mong muốn |

---

## 5. Kế hoạch Thực thi & Checklist Kiểm chứng (Pre-Code Plan & Checklist)
### 5.1 Các bước thực thi nghiệp vụ (Execution Flow Plan)
1. **Bước 1:** `SellerProductsController.GetSellerProductDetail` nhận `id`.
2. **Bước 2:** Lấy `currentUserId` từ JWT qua `TryGetUserId`.
3. **Bước 3:** Gọi `_productService.GetSellerProductDetailAsync(id, currentUserId, ct)`.
4. **Bước 4:** Nạp entity `Product` kèm `Category`, toàn bộ `Variants` (kể cả inactive) và toàn bộ `Images` (`.AsNoTracking()`).
5. **Bước 5:** Kiểm tra quyền sở hữu (`product.SellerId != currentUserId` và không phải Admin thì ném Unauthorized).
6. **Bước 6:** Ghép các biến thể với các ảnh con gắn kèm theo `VariantId`.
7. **Bước 7:** Trả về đối tượng `SellerProductDetailDto` với mã HTTP `200 OK`.

### 5.2 Checklist hoàn thành
- [x] **Pre-Code:** Hoàn tất đặc tả chi tiết quản trị sản phẩm cho Seller.
- [x] **Application:** Triển khai phương thức `GetSellerProductDetailAsync` trong `ProductService.cs`.
- [x] **Api:** `SellerProductsController.GetSellerProductDetail` trả Pure DTO.
- [x] **Verify:** `dotnet build Catalog.sln -c Release` thành công 100% với 0 lỗi.

