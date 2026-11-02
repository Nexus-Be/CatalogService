# CATALOG SERVICE - PATCH /api/v1/images/{id}

> **Feature / Use Case:** Cập nhật thông tin hình ảnh sản phẩm [CAT-14] (Thiết lập ảnh chính `sortOrder = 0`, cập nhật altText và trạng thái kích hoạt)
> **Trạng thái:** ✅ VERIFIED
> **Ngày tạo:** 2026-11-01
> **Tác giả:** Nexus Team & AI Agent

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `PATCH`
- **Route Endpoint:** `/api/v1/images/{id}`
- **Quyền hạn (RBAC Policy):** `RequireRole("seller", "admin")`
- **Quy tắc Sở hữu (Resource Ownership):** Bắt buộc sản phẩm chứa hình ảnh này phải thuộc quyền sở hữu của người bán (`product.SellerId == currentUserId`). Admin có quyền can thiệp.
- **Controller:** `Catalog.Api/Controllers/Public/ImagesController.cs`
- **Application Contract:** `Catalog.Application/Services/Product/IProductService.cs`

---

## 2. Đầu vào & Quy chuẩn Validation (Input & Validation Schema)
### 2.1 Route Parameters & Query Parameters
| Tham số | Vị trí | Kiểu C# | Bắt buộc | Ràng buộc / Giới hạn | Ghi chú |
| :--- | :--- | :--- | :---: | :--- | :--- |
| `id` | Route | `Guid` | Có | UUIDv4 format | Khóa chính của hình ảnh cần cập nhật |

### 2.2 Request Body Record DTO
- **Vị trí file:** `Catalog.Application/Services/Product/ProductRequests.cs`
- **Tên C# Record:** `UpdateProductImageRequest`
- **Khai báo C# Record:**
```csharp
public sealed record UpdateProductImageRequest(
    bool? IsActive = null,
    short? SortOrder = null,
    string? AltText = null);
```
- **Bảng quy chuẩn Validation:**
| Tham số | Kiểu C# | Bắt buộc | Ràng buộc / Giới hạn | Ghi chú |
| :--- | :--- | :---: | :--- | :--- |
| `isActive` | `bool?` | Không | `true` hoặc `false` | Bật/tắt hiển thị hình ảnh |
| `sortOrder` | `short?` | Không | Min: 0 | Thứ tự sắp xếp. Nếu bằng `0`, ảnh trở thành ảnh chính (Primary) |
| `altText` | `string?` | Không | Max: 250 ký tự | Văn bản mô tả ảnh |

- **JSON Request Body mẫu (camelCase):**
```json
{
  "sortOrder": 0,
  "altText": "Ảnh bìa chính thức bàn phím cơ Nexus",
  "isActive": true
}
```

---

## 3. Đầu ra & Hợp đồng DTO thuần (Response Contract - Pure DTO)
- **Vị trí file:** `Catalog.Application/Services/Product/ProductDtos.cs`
- **Tên C# Record:** `ProductImageDto`
- **Mã HTTP thành công:** `200 OK`
- **JSON Response Body mẫu (camelCase):**
```json
{
  "id": "44444444-4444-4444-4444-444444444444",
  "productId": "77777777-7777-7777-7777-777777777777",
  "variantId": null,
  "url": "https://res.cloudinary.com/nexus/image/upload/v1/products/kb-primary.webp",
  "altText": "Ảnh bìa chính thức bàn phím cơ Nexus",
  "isActive": true,
  "sortOrder": 0
}
```

---

## 4. Ma trận Mã lỗi RFC 9110 (Error Matrix)
| HTTP Status | Error Code | Message (Sentence case) | Điều kiện kích hoạt (Trigger Condition) |
| :---: | :--- | :--- | :--- |
| `400 Bad Request` | `VAL_INVALID_INPUT` | `Sort order must be non-negative.` | `sortOrder < 0` |
| `401 Unauthorized` | `UNAUTHORIZED` | `Authentication is required to access this resource.` | Không có Bearer token hợp lệ |
| `403 Forbidden` | `FORBIDDEN` | `You do not have permission to modify this image.` | Người bán không sở hữu sản phẩm chứa ảnh |
| `404 Not Found` | `IMAGE_NOT_FOUND` | `Product image not found with the specified identifier.` | Không tìm thấy hình ảnh hoặc đã bị xóa |
| `500 Internal Error` | `INTERNAL_SERVER_ERROR` | `An unexpected error occurred while processing the request.` | Lỗi không mong muốn |

---

## 5. Kế hoạch Thực thi & Checklist Kiểm chứng (Pre-Code Plan & Checklist)
### 5.1 Các bước thực thi nghiệp vụ (Execution Flow Plan)
1. **Bước 1:** `ImagesController.UpdateImage` nhận `id` và `UpdateProductImageRequest`.
2. **Bước 2:** Lấy `currentUserId` từ JWT, gọi `_productService.UpdateImageAsync(id, request, currentUserId, ct)`.
3. **Bước 3:** Nạp entity `ProductImage` kèm `Product` cha và danh sách các ảnh khác của sản phẩm.
4. **Bước 4:** Kiểm tra quyền sở hữu của người bán đối với sản phẩm cha.
5. **Bước 5 (Ràng buộc Ảnh đại diện duy nhất):** Nếu `request.SortOrder == 0`, tự động hạ cấp các ảnh khác có `SortOrder == 0` thành `SortOrder > 0` để đảm bảo sản phẩm luôn có duy nhất 1 ảnh chính.
6. **Bước 6:** Cập nhật các trường: `IsActive`, `SortOrder`, `AltText`.
7. **Bước 7:** Lưu DB và trả về `ProductImageDto` với mã HTTP `200 OK`.

### 5.2 Checklist hoàn thành
- [x] **Pre-Code:** Đặc tả cơ chế quản lý ảnh đại diện chính duy nhất.
- [x] **Application:** Triển khai phương thức `UpdateImageAsync` trong `ProductService.cs`.
- [x] **Api:** `ImagesController.UpdateImage` ánh xạ route `PATCH /api/v1/images/{id}`.
- [x] **Verify:** `dotnet build Catalog.sln -c Release` thành công 100% với 0 lỗi.

