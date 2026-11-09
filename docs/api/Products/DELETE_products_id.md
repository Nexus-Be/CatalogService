# CATALOG SERVICE - DELETE /api/v1/products/{id}

> **Feature / Use Case:** Xóa mềm sản phẩm và tự động cascade sang tất cả biến thể/hình ảnh [CAT-04]
> **Trạng thái:** ✅ VERIFIED
> **Ngày tạo:** 2026-11-01
> **Tác giả:** Nexus Team & AI Agent

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `DELETE`
- **Route Endpoint:** `/api/v1/products/{id}`
- **Quyền hạn (RBAC Policy):** `RequireRole("seller", "admin")`
- **Quy tắc Sở hữu (Resource Ownership):** Bắt buộc sản phẩm phải thuộc quyền sở hữu của người bán (`product.SellerId == currentUserId`). Admin có quyền xóa cưỡng chế.
- **Controller:** `Catalog.Api/Controllers/Public/ProductsController.cs`
- **Application Contract:** `Catalog.Application/Services/Product/IProductService.cs`

---

## 2. Đầu vào & Quy chuẩn Validation (Input & Validation Schema)
### 2.1 Route Parameters & Query Parameters
| Tham số | Vị trí | Kiểu C# | Bắt buộc | Ràng buộc / Giới hạn | Ghi chú |
| :--- | :--- | :--- | :---: | :--- | :--- |
| `id` | Route | `Guid` | Có | UUIDv4 format | Khóa chính của sản phẩm cần xóa mềm |

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
| `400 Bad Request` | `PRODUCT_IN_AUCTION` | `Product is currently in an active auction and cannot be deleted.` | Sản phẩm đang trong phiên đấu giá mở (`InAuction`) |
| `400 Bad Request` | `PRODUCT_ALREADY_AUCTIONED` | `Auctioned products cannot be deleted.` | Sản phẩm đấu giá đã bán xong (`Auctioned`) |
| `401 Unauthorized` | `UNAUTHORIZED` | `Authentication is required to access this resource.` | Không có Bearer token hợp lệ |
| `403 Forbidden` | `FORBIDDEN` | `You do not have permission to delete this product.` | `currentUserId` không sở hữu sản phẩm này |
| `404 Not Found` | `PRODUCT_NOT_FOUND` | `Product not found with the specified identifier.` | Không tìm thấy sản phẩm hoặc sản phẩm đã bị xóa trước đó |
| `500 Internal Error` | `INTERNAL_SERVER_ERROR` | `An unexpected error occurred while processing the request.` | Lỗi không mong muốn trong quá trình thực thi xóa mềm |

---

## 5. Kế hoạch Thực thi & Checklist Kiểm chứng (Pre-Code Plan & Checklist)
### 5.1 Các bước thực thi nghiệp vụ (Execution Flow Plan)
1. **Bước 1:** `ProductsController.Delete` nhận `id`.
2. **Bước 2:** Lấy `currentUserId` và gọi `_productService.SoftDeleteAsync(id, currentUserId, ct)`.
3. **Bước 3:** Tìm sản phẩm theo `id` kèm toàn bộ `Variants` và `Images`.
4. **Bước 4:** Kiểm tra quyền sở hữu của người bán đối với sản phẩm.
5. **Bước 5:** Thực hiện xóa mềm:
   - `product.IsDeleted = true; product.DeletedAt = DateTimeOffset.UtcNow;`
   - Duyệt tất cả `variant in product.Variants`: đánh dấu `variant.IsDeleted = true; variant.DeletedAt = DateTimeOffset.UtcNow;`
   - Duyệt tất cả `image in product.Images`: đánh dấu `image.IsDeleted = true;`
6. **Bước 6:** Commit transaction vào database PostgreSQL.
7. **Bước 7:** Trả về `NoContent()` (HTTP `204`).

### 5.2 Checklist hoàn thành
- [x] **Pre-Code:** Đặc tả cơ chế cascade soft-delete của sản phẩm.
- [x] **Application:** Triển khai phương thức `SoftDeleteAsync` trong `ProductService.cs`.
- [x] **Api:** `ProductsController.Delete` trả `204 NoContent`.
- [x] **Verify:** `dotnet build Catalog.sln -c Release` thành công 100% với 0 lỗi.

