# CATALOG SERVICE - POST /api/v1/products/{id}/images

> **Feature / Use Case:** Tải lên hình ảnh sản phẩm qua Cloudinary [CAT-12] (Giới hạn tối đa 5 hình/sản phẩm, dung lượng file ≤ 5MB, định dạng .jpg/.jpeg/.png/.webp)
> **Trạng thái:** ✅ VERIFIED
> **Ngày tạo:** 2026-11-01
> **Tác giả:** Nexus Team & AI Agent

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `POST`
- **Route Endpoint:** `/api/v1/products/{id}/images`
- **Content-Type:** `multipart/form-data`
- **Quyền hạn (RBAC Policy):** `RequireRole("seller", "admin")`
- **Quy tắc Sở hữu (Resource Ownership):** Bắt buộc sản phẩm phải thuộc quyền sở hữu của người bán (`product.SellerId == currentUserId`). Admin có quyền can thiệp.
- **Controller:** `Catalog.Api/Controllers/Public/ProductsController.cs`
- **Application Contract:** `Catalog.Application/Services/Product/IProductService.cs`
- **Dịch vụ Lưu trữ Bên ngoài:** Sử dụng `IImageStorageService` để đẩy file nhị phân lên Cloudinary CDN và nhận về URL an toàn (`https`).

---

## 2. Đầu vào & Quy chuẩn Validation (Input & Validation Schema)
### 2.1 Route Parameters & Query Parameters
| Tham số | Vị trí | Kiểu C# | Bắt buộc | Ràng buộc / Giới hạn | Ghi chú |
| :--- | :--- | :--- | :---: | :--- | :--- |
| `id` | Route | `Guid` | Có | UUIDv4 format | Khóa chính của sản phẩm cần thêm ảnh |

### 2.2 Form Data Parameters (multipart/form-data)
| Tham số | Vị trí | Kiểu C# | Bắt buộc | Ràng buộc / Giới hạn | Ghi chú |
| :--- | :--- | :--- | :---: | :--- | :--- |
| `file` | Form | `IFormFile` | Có | Max: 5MB; Đuôi: `.jpg`, `.jpeg`, `.png`, `.webp` | File ảnh nhị phân |
| `variantId` | Form | `Guid?` | Không | UUIDv4 format | Liên kết ảnh với một SKU cụ thể |
| `altText` | Form | `string?` | Không | Max: 250 ký tự | Văn bản thay thế hỗ trợ SEO/Accessibility |
| `sortOrder` | Form | `short` | Không | Min: 0, Default: 0 | Thứ tự hiển thị (`0` là ảnh đại diện chính) |

---

## 3. Đầu ra & Hợp đồng DTO thuần (Response Contract - Pure DTO)
- **Vị trí file:** `Catalog.Application/Services/Product/ProductDtos.cs`
- **Tên C# Record:** `ProductImageDto`
- **Mã HTTP thành công:** `201 Created`
- **JSON Response Body mẫu (camelCase):**
```json
{
  "id": "44444444-4444-4444-4444-444444444444",
  "productId": "77777777-7777-7777-7777-777777777777",
  "variantId": null,
  "url": "https://res.cloudinary.com/nexus/image/upload/v1/products/kb-primary.webp",
  "altText": "Ảnh đại diện bàn phím cơ Nexus",
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
| `400 Bad Request` | `VAL_INVALID_INPUT` | `Invalid image format. Supported formats: .jpg, .jpeg, .png, .webp.` | Định dạng đuôi file không nằm trong danh sách cho phép |
| `400 Bad Request` | `VAL_LIMIT_EXCEEDED` | `A product can have a maximum of 5 images.` | Sản phẩm đã đạt giới hạn tối đa 5 hình ảnh |
| `401 Unauthorized` | `UNAUTHORIZED` | `Authentication is required to access this resource.` | Không có Bearer token hợp lệ |
| `403 Forbidden` | `FORBIDDEN` | `You do not have permission to modify this product.` | Người bán không sở hữu sản phẩm này |
| `404 Not Found` | `PRODUCT_NOT_FOUND` | `Product not found with the specified identifier.` | Không tìm thấy sản phẩm |
| `500 Internal Error` | `INTERNAL_SERVER_ERROR` | `An unexpected error occurred while processing the request.` | Lỗi trong quá trình upload lên Cloudinary hoặc lưu DB |

---

## 5. Kế hoạch Thực thi & Checklist Kiểm chứng (Pre-Code Plan & Checklist)
### 5.1 Các bước thực thi nghiệp vụ (Execution Flow Plan)
1. **Bước 1:** `ProductsController.UploadImage` nhận `id`, `IFormFile file`, `variantId`, `altText`, `sortOrder`.
2. **Bước 2:** Lấy `currentUserId`, kiểm tra file hợp lệ (kích thước ≤ 5MB, định dạng thuộc `.jpg/.jpeg/.png/.webp`).
3. **Bước 3:** Gọi `_productService.AddProductImageAsync(...)`.
4. **Bước 4:** Nạp entity `Product` kèm danh sách `Images`.
5. **Bước 5:** Kiểm tra quyền sở hữu của người bán và số lượng ảnh hiện tại `< 5`.
6. **Bước 6:** Mở Stream từ file tải lên và gọi `IImageStorageService.UploadAsync` lưu lên Cloudinary.
7. **Bước 7:** Khởi tạo entity `ProductImage` với URL trả về từ Cloudinary. Nếu `sortOrder == 0`, tự động đồng bộ cờ ảnh đại diện chính.
8. **Bước 8:** Lưu vào DB và trả về `ProductImageDto` với mã HTTP `201 Created`.

### 5.2 Checklist hoàn thành
- [x] **Pre-Code:** Đặc tả đầy đủ ràng buộc upload file (dung lượng, định dạng, Cloudinary CDN).
- [x] **Application:** Triển khai phương thức `AddProductImageAsync` trong `ProductService.cs`.
- [x] **Api:** `ProductsController.UploadImage` cấu hình `[Consumes("multipart/form-data")]`.
- [x] **Verify:** `dotnet build Catalog.sln -c Release` thành công 100% với 0 lỗi.

