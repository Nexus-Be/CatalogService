# CATALOG SERVICE - PUT /api/v1/products/{id}/default-variant

> **Feature / Use Case:** Thiết lập biến thể mặc định cho sản phẩm [CAT-10]
> **Trạng thái:** ✅ VERIFIED
> **Ngày tạo:** 2026-11-01
> **Tác giả:** Nexus Team & AI Agent

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `PUT`
- **Route Endpoint:** `/api/v1/products/{id}/default-variant`
- **Quyền hạn (RBAC Policy):** `RequireRole("seller", "admin")`
- **Quy tắc Sở hữu (Resource Ownership):** Bắt buộc sản phẩm phải thuộc quyền sở hữu của người bán (`product.SellerId == currentUserId`). Admin có quyền can thiệp.
- **Controller:** `Catalog.Api/Controllers/Public/ProductsController.cs`
- **Application Contract:** `Catalog.Application/Services/Product/IProductService.cs`

---

## 2. Đầu vào & Quy chuẩn Validation (Input & Validation Schema)
### 2.1 Route Parameters & Query Parameters
| Tham số | Vị trí | Kiểu C# | Bắt buộc | Ràng buộc / Giới hạn | Ghi chú |
| :--- | :--- | :--- | :---: | :--- | :--- |
| `id` | Route | `Guid` | Có | UUIDv4 format | Khóa chính của sản phẩm cần cấu hình |

### 2.2 Request Body Record DTO
- **Vị trí file:** `Catalog.Application/Services/Product/ProductRequests.cs`
- **Tên C# Record:** `SetDefaultVariantRequest`
- **Khai báo C# Record:**
```csharp
public sealed record SetDefaultVariantRequest(
    Guid VariantId);
```
- **Bảng quy chuẩn Validation:**
| Tham số | Kiểu C# | Bắt buộc | Ràng buộc / Giới hạn | Ghi chú |
| :--- | :--- | :---: | :--- | :--- |
| `variantId` | `Guid` | Có | UUIDv4 format | Biến thể phải thuộc về sản phẩm này và đang kích hoạt |

- **JSON Request Body mẫu (camelCase):**
```json
{
  "variantId": "33333333-3333-3333-3333-333333333333"
}
```

---

## 3. Đầu ra & Hợp đồng DTO thuần (Response Contract - Pure DTO)
- **Mã HTTP thành công:** `200 OK`
- **JSON Response Body mẫu (camelCase):**
```json
{
  "defaultVariantId": "33333333-3333-3333-3333-333333333333"
}
```

---

## 4. Ma trận Mã lỗi RFC 9110 (Error Matrix)
| HTTP Status | Error Code | Message (Sentence case) | Điều kiện kích hoạt (Trigger Condition) |
| :---: | :--- | :--- | :--- |
| `400 Bad Request` | `VAL_INVALID_INPUT` | `Specified variant does not belong to this product.` | `variantId` thuộc sản phẩm khác |
| `400 Bad Request` | `VAL_INACTIVE_VARIANT` | `Cannot set an inactive variant as the default.` | Biến thể chỉ định đang ở trạng thái `Inactive` |
| `401 Unauthorized` | `UNAUTHORIZED` | `Authentication is required to access this resource.` | Không có Bearer token hợp lệ |
| `403 Forbidden` | `FORBIDDEN` | `You do not have permission to modify this product.` | Người bán không sở hữu sản phẩm này |
| `404 Not Found` | `PRODUCT_NOT_FOUND` | `Product not found with the specified identifier.` | Không tìm thấy sản phẩm cha |
| `404 Not Found` | `VARIANT_NOT_FOUND` | `Variant not found with the specified identifier.` | Không tìm thấy biến thể chỉ định |
| `500 Internal Error` | `INTERNAL_SERVER_ERROR` | `An unexpected error occurred while processing the request.` | Lỗi không mong muốn trong quá trình thực thi |

---

## 5. Kế hoạch Thực thi & Checklist Kiểm chứng (Pre-Code Plan & Checklist)
### 5.1 Các bước thực thi nghiệp vụ (Execution Flow Plan)
1. **Bước 1:** `ProductsController.SetDefaultVariant` nhận `id` và `SetDefaultVariantRequest`.
2. **Bước 2:** Lấy `currentUserId` từ JWT, gọi `_productService.SetDefaultVariantAsync(id, currentUserId, request.VariantId, ct)`.
3. **Bước 3:** Nạp entity `Product` kèm danh sách `Variants`.
4. **Bước 4:** Kiểm tra quyền sở hữu của người bán.
5. **Bước 5:** Xác thực `variantId` nằm trong danh sách `Variants` của sản phẩm và có `IsActive == true`.
6. **Bước 6:** Cập nhật biến thể mặc định cho sản phẩm.
7. **Bước 7:** Lưu DB và trả về `{ defaultVariantId = defaultId }` với mã HTTP `200 OK`.

### 5.2 Checklist hoàn thành
- [x] **Pre-Code:** Hoàn tất tài liệu đặc tả cấu hình biến thể mặc định.
- [x] **Application:** Triển khai phương thức `SetDefaultVariantAsync` trong `ProductService.cs`.
- [x] **Api:** `ProductsController.SetDefaultVariant` ánh xạ route `PUT /api/v1/products/{id}/default-variant`.
- [x] **Verify:** `dotnet build Catalog.sln -c Release` thành công 100% với 0 lỗi.

