# CATALOG SERVICE - GET /api/v1/products/{id}/images

> **Feature / Use Case:** Lấy danh sách toàn bộ hình ảnh của một sản phẩm [CAT-40]
> **Trạng thái:** ✅ VERIFIED
> **Ngày tạo:** 2026-11-01
> **Tác giả:** Nexus Team & AI Agent

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `GET`
- **Route Endpoint:** `/api/v1/products/{id}/images`
- **Quyền hạn (RBAC Policy):** `AllowAnonymous` (Khách mua hàng và người bán đều có thể xem danh sách ảnh)
- **Quy tắc Sở hữu (Resource Ownership):** Không yêu cầu
- **Controller:** `Catalog.Api/Controllers/Public/ProductsController.cs`
- **Application Contract:** `Catalog.Application/Services/Product/IProductService.cs`

---

## 2. Đầu vào & Quy chuẩn Validation (Input & Validation Schema)
### 2.1 Route Parameters & Query Parameters
| Tham số | Vị trí | Kiểu C# | Bắt buộc | Ràng buộc / Giới hạn | Ghi chú |
| :--- | :--- | :--- | :---: | :--- | :--- |
| `id` | Route | `Guid` | Có | UUIDv4 format | Khóa chính của sản phẩm cần lấy danh sách ảnh |

### 2.2 Request Body Record DTO
- Endpoint `GET` không sử dụng Request Body.

---

## 3. Đầu ra & Hợp đồng DTO thuần (Response Contract - Pure DTO)
- **Vị trí file:** `Catalog.Application/Services/Product/ProductDtos.cs`
- **Tên C# Record:** `IReadOnlyList<ProductImageDto>`
- **Mã HTTP thành công:** `200 OK`
- **Khai báo C# Record:**
```csharp
public sealed record ProductImageDto(
    Guid Id,
    Guid ProductId,
    Guid? VariantId,
    string Url,
    string? AltText,
    bool IsActive,
    short SortOrder);
```
- **JSON Response Body mẫu (Pure DTO - camelCase):**
```json
[
  {
    "id": "44444444-4444-4444-4444-444444444444",
    "productId": "77777777-7777-7777-7777-777777777777",
    "variantId": null,
    "url": "https://res.cloudinary.com/nexus/image/upload/v1/products/kb-primary.webp",
    "altText": "Ảnh đại diện bàn phím cơ Nexus",
    "isActive": true,
    "sortOrder": 0
  },
  {
    "id": "44444444-4444-4444-4444-444444444445",
    "productId": "77777777-7777-7777-7777-777777777777",
    "variantId": "33333333-3333-3333-3333-333333333333",
    "url": "https://res.cloudinary.com/nexus/image/upload/v1/products/kb-white-side.webp",
    "altText": "Ảnh góc nghiêng bản màu trắng",
    "isActive": true,
    "sortOrder": 1
  }
]
```

---

## 4. Ma trận Mã lỗi RFC 9110 (Error Matrix)
| HTTP Status | Error Code | Message (Sentence case) | Điều kiện kích hoạt (Trigger Condition) |
| :---: | :--- | :--- | :--- |
| `400 Bad Request` | `VAL_INVALID_INPUT` | `Invalid product identifier format.` | Chuỗi `id` không đúng định dạng UUIDv4 |
| `404 Not Found` | `PRODUCT_NOT_FOUND` | `Product not found with the specified identifier.` | Sản phẩm không tồn tại hoặc đã bị xóa |
| `500 Internal Error` | `INTERNAL_SERVER_ERROR` | `An unexpected error occurred while processing the request.` | Lỗi không mong muốn trong quá trình truy vấn hệ thống |

---

## 5. Kế hoạch Thực thi & Checklist Kiểm chứng (Pre-Code Plan & Checklist)
### 5.1 Các bước thực thi nghiệp vụ (Execution Flow Plan)
1. **Bước 1:** `ProductsController.GetImages` nhận `id`.
2. **Bước 2:** Gọi `_productService.GetProductImagesAsync(id, ct)`.
3. **Bước 3:** Kiểm tra sản phẩm tồn tại trong `AppDbContext.Products`.
4. **Bước 4:** Truy vấn bảng `ProductImages` lọc theo `ProductId == id` và `IsDeleted == false`, sắp xếp theo `SortOrder` tăng dần.
5. **Bước 5:** Map sang danh sách `ProductImageDto` và trả về `Ok(images)`.

### 5.2 Checklist hoàn thành
- [x] **Pre-Code:** Hoàn tất đặc tả danh sách hình ảnh sản phẩm.
- [x] **Application:** Triển khai phương thức `GetProductImagesAsync` trong `ProductService.cs`.
- [x] **Api:** `ProductsController.GetImages` ánh xạ route `GET /api/v1/products/{id}/images`.
- [x] **Verify:** `dotnet build Catalog.sln -c Release` thành công 100% với 0 lỗi.

