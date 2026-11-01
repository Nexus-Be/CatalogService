# CATALOG SERVICE - DELETE /api/v1/images/{id}

> **Feature / Use Case:** Xóa hình ảnh sản phẩm [CAT-13] (Hỗ trợ cả route trực tiếp và sub-route `/products/{id}/images/{imageId}`, tự động chuyển cờ ảnh chính cho ảnh kế tiếp)
> **Trạng thái:** ✅ VERIFIED
> **Ngày tạo:** 2026-11-01
> **Tác giả:** Nexus Team & AI Agent

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `DELETE`
- **Route Endpoint:** `/api/v1/images/{id}` (Hỗ trợ sub-route: `/api/v1/products/{id}/images/{imageId}`)
- **Quyền hạn (RBAC Policy):** `RequireRole("seller", "admin")`
- **Quy tắc Sở hữu (Resource Ownership):** Bắt buộc sản phẩm chứa hình ảnh này phải thuộc quyền sở hữu của người bán (`product.SellerId == currentUserId`). Admin có quyền can thiệp.
- **Controller:** `Catalog.Api/Controllers/Public/ImagesController.cs` & `Catalog.Api/Controllers/Public/ProductsController.cs`
- **Application Contract:** `Catalog.Application/Services/Product/IProductService.cs`

---

## 2. Đầu vào & Quy chuẩn Validation (Input & Validation Schema)
### 2.1 Route Parameters & Query Parameters
| Tham số | Vị trí | Kiểu C# | Bắt buộc | Ràng buộc / Giới hạn | Ghi chú |
| :--- | :--- | :--- | :---: | :--- | :--- |
| `id` | Route | `Guid` | Có | UUIDv4 format | Khóa chính của hình ảnh cần xóa |

### 2.2 Request Body Record DTO
- Endpoint `DELETE` không sử dụng Request Body.

---

## 3. Đầu ra & Hợp đồng DTO thuần (Response Contract - Pure DTO)
- **Mã HTTP thành công:** `204 NoContent`
- **Phản hồi:** Trống (Empty body).

---

## 4. Ma trận Mã lỗi RFC 9110 (Error Matrix)
| HTTP Status | Error Code | Message (Sentence case) | Điều kiện kích hoạt (Trigger Condition) |
| :---: | :--- | :--- | :--- |
| `401 Unauthorized` | `UNAUTHORIZED` | `Authentication is required to access this resource.` | Không có Bearer token hợp lệ |
| `403 Forbidden` | `FORBIDDEN` | `You do not have permission to delete this image.` | Người bán không sở hữu sản phẩm chứa ảnh này |
| `404 Not Found` | `IMAGE_NOT_FOUND` | `Product image not found with the specified identifier.` | Không tìm thấy hình ảnh hoặc đã bị xóa |
| `500 Internal Error` | `INTERNAL_SERVER_ERROR` | `An unexpected error occurred while processing the request.` | Lỗi không mong muốn trong quá trình thực thi |

---

## 5. Kế hoạch Thực thi & Checklist Kiểm chứng (Pre-Code Plan & Checklist)
### 5.1 Các bước thực thi nghiệp vụ (Execution Flow Plan)
1. **Bước 1:** `ImagesController.DeleteImage` nhận `id`.
2. **Bước 2:** Lấy `currentUserId` từ JWT, gọi `_productService.DeleteImageByIdAsync(id, currentUserId, ct)`.
3. **Bước 3:** Nạp entity `ProductImage` kèm `Product` cha và tất cả ảnh còn lại của sản phẩm.
4. **Bước 4:** Kiểm tra quyền sở hữu của người bán đối với sản phẩm cha.
5. **Bước 5:** Thực hiện xóa mềm ảnh hiện tại: `image.IsDeleted = true;`.
6. **Bước 6 (Tự động chuyển giao Ảnh đại diện chính):** Nếu ảnh vừa xóa đang là ảnh chính (`image.SortOrder == 0`), hệ thống tự động tìm ảnh kích hoạt còn lại có `SortOrder` nhỏ nhất và đặt lại `SortOrder = 0`.
7. **Bước 7:** Commit transaction vào database PostgreSQL và trả về `204 NoContent`.

### 5.2 Checklist hoàn thành
- [x] **Pre-Code:** Đặc tả cơ chế tự động chuyển giao ảnh chính khi xóa ảnh `sortOrder == 0`.
- [x] **Application:** Triển khai phương thức `DeleteImageByIdAsync` trong `ProductService.cs`.
- [x] **Api:** `ImagesController.DeleteImage` và `ProductsController.DeleteImage` trả `204 NoContent`.
- [x] **Verify:** `dotnet build Catalog.sln -c Release` thành công 100% với 0 lỗi.

