# CATALOG SERVICE - GET /api/v1/variants/{id}/images

> **Feature / Use Case:** Lấy danh sách hình ảnh của biến thể sản phẩm [CAT-28] (Hỗ trợ cơ chế tự động fallback về hình ảnh chính của sản phẩm nếu biến thể chưa có ảnh riêng)
> **Trạng thái:** ✅ VERIFIED
> **Ngày tạo:** 2026-11-06
> **Tác giả:** Nexus Team & AI Agent

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `GET`
- **Route Endpoint:** `/api/v1/variants/{id}/images`
- **Quyền hạn (RBAC Policy):** `AllowAnonymous` (Công khai cho toàn bộ Buyer, Seller và hệ thống).
- **Quy tắc Sở hữu (Resource Ownership):** Dữ liệu công khai, không kiểm tra quyền sở hữu.
- **Controller:** `Catalog.Api/Controllers/Public/VariantsController.cs`
- **Application Contract:** `Catalog.Application/Services/Product/IProductService.cs`
- **Tương tác Liên Service:** Không có (truy vấn nội bộ database `AppDbContext.ProductImages`).

---

## 2. Đầu vào & Quy chuẩn Validation (Input & Validation Schema)
### 2.1 Route Parameters & Query Parameters
| Tham số | Vị trí | Kiểu C# | Bắt buộc | Ràng buộc / Giới hạn | Ghi chú |
| :--- | :--- | :--- | :---: | :--- | :--- |
| `id` | Route | `Guid` | Có | UUIDv4 format | Mã định danh biến thể (`variantId`) |

### 2.2 Request Body Record DTO
- Không có Request Body (HTTP GET).

---

## 3. Đầu ra & Hợp đồng DTO thuần (Response Contract - Pure DTO)
> **LƯU Ý:** Trả về danh sách thuần `IReadOnlyList<ProductImageDto>` với mã HTTP `200 OK`.

- **Vị trí file:** `Catalog.Application/Services/Product/ProductDtos.cs`
- **Tên C# Record:** `ProductImageDto`
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
- **JSON Response Body mẫu (camelCase):**
```json
[
  {
    "id": "77777777-1111-2222-3333-444444444444",
    "productId": "2bcb5321-8ef7-43d2-99ed-4f5da606caab",
    "variantId": "fafdc926-69b6-4b61-810d-3c98ac1c520c",
    "url": "https://res.cloudinary.com/u6yxkkop/image/upload/v1793813165/nexus_catalog/products/kb-white-angle.jpg",
    "altText": "Góc nghiêng bàn phím trắng",
    "isActive": true,
    "sortOrder": 0
  }
]
```

---

## 4. Ma trận Mã lỗi RFC 9110 (Error Matrix)
| HTTP Status | Error Code | Message (Sentence case) | Điều kiện kích hoạt (Trigger Condition) |
| :---: | :--- | :--- | :--- |
| `404 Not Found` | `VARIANT_NOT_FOUND` | `Product variant not found with the specified identifier.` | `id` không tồn tại hoặc đã bị xóa mềm trong DB |
| `500 Internal Error` | `INTERNAL_SERVER_ERROR` | `An unexpected error occurred while processing the request.` | Lỗi không mong muốn trong quá trình truy vấn dữ liệu |

---

## 5. Kế hoạch Thực thi & Checklist Kiểm chứng (Pre-Code Plan & Checklist)
### 5.1 Các bước thực thi nghiệp vụ (Execution Flow Plan)
1. **Bước 1:** `VariantsController.GetVariantImages` nhận `id` từ route.
2. **Bước 2:** Gọi `_productService.GetVariantImagesAsync(id, ct)`.
3. **Bước 3:** Truy vấn các ảnh có `VariantId == id && !IsDeleted && IsActive`.
4. **Bước 4:** Nếu biến thể chưa có ảnh riêng (`variantImages.Count == 0`), hệ thống tự động fallback lấy danh sách ảnh đại diện của sản phẩm cha (`variant.Product.Images`).
5. **Bước 5:** Trả về danh sách `ProductImageDto` với HTTP `200 OK`.

### 5.2 Checklist hoàn thành
- [x] **Pre-Code:** Đặc tả đầy đủ route và cơ chế fallback ảnh sản phẩm.
- [x] **Application:** `ProductService.GetVariantImagesAsync` xử lý fallback ảnh chính xác.
- [x] **Api:** `VariantsController.GetVariantImages` public không yêu cầu token.
- [x] **Verify:** `dotnet build Catalog.sln -c Release` thành công 100% với 0 lỗi.

