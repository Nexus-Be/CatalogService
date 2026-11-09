# CATALOG SERVICE - DELETE /api/v1/variants/{id}/images/{imageId}

> **Feature / Use Case:** Xóa hình ảnh của biến thể sản phẩm [CAT-30] (Xóa mềm bản ghi trong cơ sở dữ liệu, kiểm tra không được xóa ảnh duy nhất của sản phẩm đang mở bán)
> **Trạng thái:** ✅ VERIFIED
> **Ngày tạo:** 2026-11-06
> **Tác giả:** Nexus Team & AI Agent

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `DELETE`
- **Route Endpoint:** `/api/v1/variants/{id}/images/{imageId}`
- **Quyền hạn (RBAC Policy):** `RequireRole("seller")`
- **Quy tắc Sở hữu (Resource Ownership):** Bắt buộc sản phẩm chứa hình ảnh phải thuộc quyền sở hữu của người bán (`variant.Product.SellerId == currentUserId`).
- **Controller:** `Catalog.Api/Controllers/Public/VariantsController.cs`
- **Application Contract:** `Catalog.Application/Services/Product/IProductService.cs`
- **Tương tác Liên Service:** Không có (xử lý nội bộ Catalog DB và dọn dẹp storage Cloudinary).

---

## 2. Đầu vào & Quy chuẩn Validation (Input & Validation Schema)
### 2.1 Route Parameters & Query Parameters
| Tham số | Vị trí | Kiểu C# | Bắt buộc | Ràng buộc / Giới hạn | Ghi chú |
| :--- | :--- | :--- | :---: | :--- | :--- |
| `id` | Route | `Guid` | Có | UUIDv4 format | Mã định danh biến thể (`variantId`) |
| `imageId` | Route | `Guid` | Có | UUIDv4 format | Mã định danh hình ảnh cần xóa |

### 2.2 Request Body Record DTO
- Không có Request Body (HTTP DELETE).

---

## 3. Đầu ra & Hợp đồng DTO thuần (Response Contract - Pure DTO)
> **LƯU Ý:** Trả về HTTP `204 NoContent` khi xóa thành công.

- **Vị trí file:** `Catalog.Api/Controllers/Public/VariantsController.cs`
- **Mã HTTP thành công:** `204 NoContent`
- **Response Body:** Rỗng (Empty response body).

---

## 4. Ma trận Mã lỗi RFC 9110 (Error Matrix)
| HTTP Status | Error Code | Message (Sentence case) | Điều kiện kích hoạt (Trigger Condition) |
| :---: | :--- | :--- | :--- |
| `400 Bad Request` | `VAL_INVALID_INPUT` | `Cannot delete the only image of an active product. Deactivate the product first or upload another image.` | Cố gắng xóa hình ảnh duy nhất còn lại của một sản phẩm đang ở trạng thái `ACTIVE` |
| `401 Unauthorized` | `UNAUTHORIZED` | `Authentication is required to access this resource.` | Không gửi Bearer token hoặc token hết hạn |
| `403 Forbidden` | `PERMISSION_DENIED` | `You do not have permission to delete this image.` | Người bán không sở hữu sản phẩm chứa hình ảnh này |
| `404 Not Found` | `IMAGE_NOT_FOUND` | `Product image not found with the specified identifier.` | `imageId` không tồn tại hoặc đã bị xóa mềm trước đó |
| `500 Internal Error` | `INTERNAL_SERVER_ERROR` | `An unexpected error occurred while processing the request.` | Lỗi không mong muốn trong quá trình cập nhật cơ sở dữ liệu |

---

## 5. Kế hoạch Thực thi & Checklist Kiểm chứng (Pre-Code Plan & Checklist)
### 5.1 Các bước thực thi nghiệp vụ (Execution Flow Plan)
1. **Bước 1:** `VariantsController.DeleteVariantImage` trích xuất `currentUserId` từ claims JWT.
2. **Bước 2:** Gọi `_productService.DeleteImageByIdAsync(imageId, currentUserId, ct)`.
3. **Bước 3:** Tìm ảnh theo `imageId && !IsDeleted`, xác thực `image.Product.SellerId == currentUserId`.
4. **Bước 4:** Kiểm tra ràng buộc: Nếu sản phẩm cha đang `Active` và không còn ảnh nào khác (`remainingImagesCount == 0`), từ chối với lỗi `VAL_INVALID_INPUT`.
5. **Bước 5:** Đánh dấu xóa mềm (`IsDeleted = true`, `DeletedAt = now`).
6. **Bước 6:** Nếu ảnh vừa xóa là ảnh chính (`IsActive == true`), hệ thống tự động chuyển cờ `IsActive` cho ảnh có `SortOrder` thấp nhất tiếp theo.
7. **Bước 7:** Lưu thay đổi vào DB và trả về HTTP `204 NoContent`.

### 5.2 Checklist hoàn thành
- [x] **Pre-Code:** Đặc tả đầy đủ route parameters, điều kiện bảo vệ sản phẩm active và mã lỗi.
- [x] **Application:** `ProductService.DeleteImageByIdAsync` xử lý chuyển giao ảnh đại diện thông minh.
- [x] **Api:** `VariantsController.DeleteVariantImage` áp dụng `[RequireRole("seller")]`.
- [x] **Verify:** `dotnet build Catalog.sln -c Release` thành công 100% với 0 lỗi.

