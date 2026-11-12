# CATALOG SERVICE - GET /api/v1/seller/products/{id}

> **Feature / Use Case:** Lấy thông tin chi tiết toàn diện của một sản phẩm dành cho người bán [CAT-45] (Hiển thị đầy đủ tất cả SKU, tình trạng đơn hàng `hasOrder`, ảnh, trọng lượng vận chuyển và lý do bị từ chối duyệt `rejectionReason`)  
> **Trạng thái:** ✅ VERIFIED  
> **Ngày cập nhật:** 2026-11-12  
> **Tác giả:** Nexus Team & AI Agent  

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `GET`
- **Route Endpoint:** `/api/v1/seller/products/{id}`
- **Quyền hạn (RBAC Policy):** `RequireRole("seller", "admin")`
- **Quy tắc Sở hữu (Resource Ownership):** Bắt buộc sản phẩm phải thuộc quyền sở hữu của người bán đang đăng nhập (`product.SellerId == currentUserId`). Admin có quyền xem chi tiết của mọi người bán.
- **Controller:** `Catalog.Api/Controllers/Public/SellerProductsController.cs`
- **Application Contract:** `Catalog.Application/Services/Product/IProductService.cs` (`GetSellerProductDetailAsync`)

---

## 2. Đầu vào & Quy chuẩn Validation (Input & Validation Schema)

### 2.1 Route Parameters
| Tham số | Vị trí | Kiểu C# | Bắt buộc | Ràng buộc / Giới hạn | Ghi chú |
| :--- | :--- | :--- | :---: | :--- | :--- |
| `id` | Route | `Guid` | Có | UUIDv4 format | Khóa chính của sản phẩm cần lấy chi tiết quản trị |

### 2.2 Request Body
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
    IReadOnlyList<ProductImageDto> Images,
    int? WeightGrams = null);

public sealed record SellerProductDetailDto(
    Guid ProductId,
    Guid SellerId,
    Guid CategoryId,
    string? CategoryName,
    string Name,
    string? Description,
    string? Brand,
    IReadOnlyList<ProductSpecificationDto>? Specifications,
    string SaleType,
    string Status,
    string? StatusReason,
    bool HasOrder,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    DateTimeOffset? PublishedAt,
    IReadOnlyList<ProductImageDto> Images,
    IReadOnlyList<SellerProductVariantDto> Variants,
    int? WeightGrams = null,
    string? RejectionReason = null);
```

### 3.1 JSON Response Mẫu (Trường hợp Sản phẩm bị Từ chối duyệt):
```json
{
  "productId": "f3f47581-6036-4fa1-b9fa-803770308c71",
  "sellerId": "cccccccc-3333-3333-3333-333333333333",
  "categoryId": "250ac1a7-eef4-43f4-9d0d-310755f735c4",
  "categoryName": "Quần",
  "name": "Quần Jogger Nexus Streetwear",
  "description": "Quần jogger thể thao cao cấp co giãn 4 chiều.",
  "brand": "Nexus Wear",
  "specifications": [
    { "name": "chất_liệu", "value": "Thun nỉ da cá" }
  ],
  "saleType": "Regular",
  "status": "Draft",
  "statusReason": "Rejected",
  "hasOrder": false,
  "createdAt": "2026-11-12T02:29:02Z",
  "updatedAt": "2026-11-12T03:36:20Z",
  "publishedAt": null,
  "images": [
    {
      "id": "f311fdd7-2bf7-47e7-8499-1138c3e5701b",
      "productId": "f3f47581-6036-4fa1-b9fa-803770308c71",
      "variantId": null,
      "url": "https://res.cloudinary.com/u6yxkkop/image/upload/v1/products/jogger-1.webp",
      "altText": "Ảnh chính mặt trước",
      "isActive": true,
      "sortOrder": 0
    }
  ],
  "variants": [
    {
      "variantId": "c655af3c-7e7d-4c38-82a0-64f8ac940361",
      "skuCode": "NX-JOG-01",
      "name": "Quần Jogger Nexus Streetwear - Size L",
      "price": 350000.00,
      "currency": "VND",
      "status": "Active",
      "statusReason": null,
      "isActive": true,
      "hasOrder": false,
      "attributes": [
        { "name": "Kích cỡ", "value": "L" }
      ],
      "images": [],
      "weightGrams": 300
    }
  ],
  "weightGrams": 300,
  "rejectionReason": "Hình ảnh bị mờ và thiếu kích thước sản phẩm. Vui lòng bổ sung ảnh chụp thực tế rõ nét."
}
```

> [!NOTE]
> Khi `statusReason == "Rejected"`, trường `rejectionReason` tự động đọc từ ghi chú kiểm duyệt (`NewValues.Note`) trong bảng `AuditLogs` gần nhất để người bán hiểu lý do cần sửa trước khi nộp duyệt lại. Không yêu cầu thay đổi lược đồ database.

---

## 4. Ma trận Mã lỗi RFC 9110 (Error Matrix)

| HTTP Status | Error Code | Message (Sentence case) | Điều kiện kích hoạt |
| :---: | :--- | :--- | :--- |
| `401 Unauthorized` | `UNAUTHORIZED` | `Authentication is required to access this resource.` | Không gửi kèm Bearer token hợp lệ |
| `403 Forbidden` | `FORBIDDEN` | `You do not have permission to access or modify this resource.` | Token không có role `seller` hoặc `admin` |
| `403 Forbidden` | `PERMISSION_DENIED` | `You do not have permission to view details of this product.` | Người bán không phải chủ sở hữu của sản phẩm |
| `404 Not Found` | `PRODUCT_NOT_FOUND` | `Product not found with the specified identifier.` | Sản phẩm không tồn tại hoặc đã bị xóa mềm |
| `500 Internal Error` | `INTERNAL_SERVER_ERROR` | `An unexpected error occurred while processing the request.` | Lỗi hệ thống trong quá trình đọc dữ liệu |
