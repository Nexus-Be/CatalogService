# CATALOG SERVICE - POST /api/v1/variants/{id}/images

> **Feature / Use Case:** Tải lên hình ảnh riêng cho biến thể sản phẩm qua Cloudinary [CAT-29] (Tối đa 3 ảnh/biến thể, tổng ảnh sản phẩm ≤ 15, dung lượng file ≤ 5MB)
> **Trạng thái:** ✅ VERIFIED
> **Ngày tạo:** 2026-11-06
> **Tác giả:** Nexus Team & AI Agent

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `POST`
- **Route Endpoint:** `/api/v1/variants/{id}/images`
- **Content-Type:** `multipart/form-data`
- **Quyền hạn (RBAC Policy):** `RequireRole("seller")`
- **Quy tắc Sở hữu (Resource Ownership):** Bắt buộc sản phẩm chứa biến thể phải thuộc quyền sở hữu của người bán (`variant.Product.SellerId == currentUserId`).
- **Controller:** `Catalog.Api/Controllers/Public/VariantsController.cs`
- **Application Contract:** `Catalog.Application/Services/Product/IProductService.cs`
- **Dịch vụ Lưu trữ Bên ngoài:** Sử dụng `IImageStorageService` đẩy file nhị phân lên Cloudinary CDN (`products/variants`) và nhận về URL an toàn (`https`).

---

## 2. Đầu vào & Quy chuẩn Validation (Input & Validation Schema)
### 2.1 Route Parameters & Query Parameters
| Tham số | Vị trí | Kiểu C# | Bắt buộc | Ràng buộc / Giới hạn | Ghi chú |
| :--- | :--- | :--- | :---: | :--- | :--- |
| `id` | Route | `Guid` | Có | UUIDv4 format | Mã định danh biến thể cần tải ảnh |

### 2.2 Form Data Parameters (multipart/form-data)
| Tham số | Vị trí | Kiểu C# | Bắt buộc | Ràng buộc / Giới hạn | Ghi chú |
| :--- | :--- | :--- | :---: | :--- | :--- |
| `file` | Form | `IFormFile` | Có | Max: 5MB; Đuôi: `.jpg`, `.jpeg`, `.png`, `.webp` | File ảnh nhị phân |
| `altText` | Form | `string?` | Không | Max: 250 ký tự | Văn bản thay thế hỗ trợ SEO/Accessibility |
| `sortOrder` | Form | `short` | Không | Min: 0, Mặc định: `0` | Thứ tự hiển thị |

---

## 3. Đầu ra & Hợp đồng DTO thuần (Response Contract - Pure DTO)
> **LƯU Ý:** Trả về Pure DTO `ProductImageDto` với mã HTTP `201 Created`.

- **Vị trí file:** `Catalog.Application/Services/Product/ProductDtos.cs`
- **Tên C# Record:** `ProductImageDto`
- **Mã HTTP thành công:** `201 Created`
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
{
  "id": "88888888-4444-4444-4444-888888888888",
  "productId": "2bcb5321-8ef7-43d2-99ed-4f5da606caab",
  "variantId": "fafdc926-69b6-4b61-810d-3c98ac1c520c",
  "url": "https://res.cloudinary.com/u6yxkkop/image/upload/v1793813165/nexus_catalog/products/variants/kb-white-side.jpg",
  "altText": "Bàn phím cơ cạnh bên",
  "isActive": true,
  "sortOrder": 0
}
```

---

## 4. Ma trận Mã lỗi RFC 9110 (Error Matrix)
| HTTP Status | Error Code | Message (Sentence case) | Điều kiện kích hoạt (Trigger Condition) |
| :---: | :--- | :--- | :--- |
| `400 Bad Request` | `VAL_INVALID_FILE` | `Please select an image file to upload.` | Không gửi file hoặc file rỗng dung lượng 0 bytes |
| `400 Bad Request` | `VAL_LIMIT_EXCEEDED` | `Image file size exceeds the 5MB limit.` | File tải lên vượt quá dung lượng 5,242,880 bytes |
| `400 Bad Request` | `VAL_INVALID_INPUT` | `Invalid image format. Supported formats: .jpg, .jpeg, .png, .webp.` | Định dạng đuôi file không hợp lệ |
| `400 Bad Request` | `VAL_LIMIT_EXCEEDED` | `A variant cannot have more than 3 images.` | Biến thể đã đạt giới hạn tối đa 3 hình ảnh |
| `400 Bad Request` | `VAL_LIMIT_EXCEEDED` | `Total images for a product and its variants cannot exceed 15.` | Tổng số ảnh của sản phẩm cha và các biến thể vượt quá 15 |
| `401 Unauthorized` | `UNAUTHORIZED` | `Authentication is required to access this resource.` | Không gửi Bearer token hoặc token hết hạn |
| `403 Forbidden` | `PERMISSION_DENIED` | `You do not have permission to upload images for this variant.` | Người bán không sở hữu sản phẩm chứa biến thể này |
| `404 Not Found` | `VARIANT_NOT_FOUND` | `Product variant not found with the specified identifier.` | Biến thể không tồn tại hoặc đã bị xóa |
| `500 Internal Error` | `INTERNAL_SERVER_ERROR` | `An unexpected error occurred while processing the request.` | Lỗi không mong muốn trong quá trình upload Cloudinary hoặc ghi DB |

---

## 5. Kế hoạch Thực thi & Checklist Kiểm chứng (Pre-Code Plan & Checklist)
### 5.1 Các bước thực thi nghiệp vụ (Execution Flow Plan)
1. **Bước 1:** `VariantsController.UploadVariantImage` trích xuất `currentUserId` từ claims JWT.
2. **Bước 2:** Kiểm tra tính hợp lệ của file nhị phân (dung lượng <= 5MB, đuôi mở rộng hợp lệ).
3. **Bước 3:** Gọi `_productService.AddVariantImageAsync(...)`.
4. **Bước 4:** Xác thực quyền sở hữu của Seller và kiểm tra số lượng ảnh tối đa (tối đa 3 ảnh/variant, tối đa 15 ảnh/sản phẩm).
5. **Bước 5:** Đẩy file stream lên Cloudinary qua `_imageStorageService.UploadAsync` (thư mục `products/variants`).
6. **Bước 6:** Lưu bản ghi vào bảng `ProductImages` với `VariantId = id` và trả về `ProductImageDto` với HTTP `201 Created`.

### 5.2 Checklist hoàn thành
- [x] **Pre-Code:** Đặc tả đầy đủ các giới hạn file, ranh giới quyền hạn và ma trận lỗi.
- [x] **Application:** `ProductService.AddVariantImageAsync` upload Cloudinary và kiểm soát quota ảnh.
- [x] **Api:** `VariantsController.UploadVariantImage` áp dụng `[RequireRole("seller")]`.
- [x] **Verify:** `dotnet build Catalog.sln -c Release` thành công 100% với 0 lỗi.

