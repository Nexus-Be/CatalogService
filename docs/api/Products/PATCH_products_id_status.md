# CATALOG SERVICE - PATCH /api/v1/products/{id}/status

> **Feature / Use Case:** Thay đổi trạng thái sản phẩm [CAT-03] (DRAFT ⇄ ACTIVE ⇄ INACTIVE kèm điều kiện kiểm tra biến thể và hình ảnh tối thiểu)
> **Trạng thái:** ✅ VERIFIED
> **Ngày tạo:** 2026-11-01
> **Tác giả:** Nexus Team & AI Agent

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `PATCH`
- **Route Endpoint:** `/api/v1/products/{id}/status`
- **Quyền hạn (RBAC Policy):** `RequireRole("seller", "admin")`
- **Quy tắc Sở hữu (Resource Ownership):** Người bán chỉ được phép thay đổi trạng thái sản phẩm thuộc quyền sở hữu của chính mình (`product.SellerId == currentUserId`). Admin được phép can thiệp để kiểm duyệt hoặc hạ sản phẩm vi phạm.
- **Controller:** `Catalog.Api/Controllers/Public/ProductsController.cs`
- **Application Contract:** `Catalog.Application/Services/Product/IProductService.cs`

---

## 2. Đầu vào & Quy chuẩn Validation (Input & Validation Schema)
### 2.1 Route Parameters & Query Parameters
| Tham số | Vị trí | Kiểu C# | Bắt buộc | Ràng buộc / Giới hạn | Ghi chú |
| :--- | :--- | :--- | :---: | :--- | :--- |
| `id` | Route | `Guid` | Có | UUIDv4 format | Định danh của sản phẩm cần thay đổi trạng thái |

### 2.2 Request Body Record DTO
- **Vị trí file:** `Catalog.Application/Services/Product/ProductRequests.cs`
- **Tên C# Record:** `ChangeProductStatusRequest`
- **Khai báo C# Record:**
```csharp
public sealed record ChangeProductStatusRequest(
    string Status,
    string? StatusReason = null);
```
- **Bảng quy chuẩn Validation:**
| Tham số | Kiểu C# | Bắt buộc | Ràng buộc / Giới hạn | Ghi chú |
| :--- | :--- | :---: | :--- | :--- |
| `status` | `string` | Có | `Draft`, `Active`, `Inactive` | Trạng thái mục tiêu cần chuyển đến |
| `statusReason` | `string?` | Không | Max: 500 ký tự | Lý do thay đổi (đặc biệt cần thiết khi ngưng bán) |

- **JSON Request Body mẫu (camelCase):**
```json
{
  "status": "Active",
  "statusReason": "Approved and ready for public sale."
}
```

---

## 3. Đầu ra & Hợp đồng DTO thuần (Response Contract - Pure DTO)
- **Vị trí file:** `Catalog.Application/Services/Product/ProductDtos.cs`
- **Tên C# Record:** `ProductDto`
- **Mã HTTP thành công:** `200 OK`
- **Khai báo C# Record:**
```csharp
public sealed record ProductDto(
    Guid Id,
    Guid SellerId,
    Guid CategoryId,
    string Name,
    string? Description,
    string? Brand,
    string Status);
```
- **JSON Response Body mẫu (camelCase):**
```json
{
  "id": "77777777-7777-7777-7777-777777777777",
  "sellerId": "11111111-1111-1111-1111-111111111111",
  "categoryId": "22222222-2222-2222-2222-222222222222",
  "name": "Bàn Phím Cơ Không Dây Nexus RGB",
  "description": "Bàn phím cơ Bluetooth 5.1 hotswap 3 modes.",
  "brand": "Nexus Tech",
  "status": "Active"
}
```

---

## 4. Ma trận Mã lỗi RFC 9110 (Error Matrix)
| HTTP Status | Error Code | Message (Sentence case) | Điều kiện kích hoạt (Trigger Condition) |
| :---: | :--- | :--- | :--- |
| `400 Bad Request` | `VAL_INVALID_INPUT` | `Invalid product status. Allowed values: Draft, Active, Inactive.` | Giá trị `status` không nằm trong enum hợp lệ |
| `400 Bad Request` | `VAL_NO_ACTIVE_VARIANTS` | `Product must have at least one active variant before activation.` | Khi chuyển sang `Active` nhưng chưa có variant nào kích hoạt |
| `400 Bad Request` | `VAL_NO_ACTIVE_IMAGES` | `Product must have at least one active image before activation.` | Khi chuyển sang `Active` nhưng chưa có ảnh nào kích hoạt |
| `401 Unauthorized` | `UNAUTHORIZED` | `Authentication is required to access this resource.` | Không có Bearer token hợp lệ |
| `403 Forbidden` | `FORBIDDEN` | `You do not have permission to modify this product.` | Người bán không sở hữu sản phẩm này |
| `404 Not Found` | `PRODUCT_NOT_FOUND` | `Product not found with the specified identifier.` | Sản phẩm không tồn tại hoặc đã bị xóa mềm |
| `500 Internal Error` | `INTERNAL_SERVER_ERROR` | `An unexpected error occurred while processing the request.` | Lỗi không mong muốn trong quá trình lưu dữ liệu |

### 4.1 Bảng Ánh xạ Enum liên quan
| Tên Enum C# | Giá trị DB (Integer) | Giá trị API (PascalCase) | Ý nghĩa nghiệp vụ |
| :--- | :---: | :--- | :--- |
| `ProductStatus.Draft` | `0` | `"Draft"` | Bản nháp chưa hiển thị ra ngoài |
| `ProductStatus.Active` | `1` | `"Active"` | Đang mở bán công khai |
| `ProductStatus.Inactive` | `2` | `"Inactive"` | Tạm ngưng kinh doanh |

---

## 5. Kế hoạch Thực thi & Checklist Kiểm chứng (Pre-Code Plan & Checklist)
### 5.1 Các bước thực thi nghiệp vụ (Execution Flow Plan)
1. **Bước 1:** `ProductsController.ChangeStatus` nhận `id` và `ChangeProductStatusRequest`.
2. **Bước 2:** Lấy `currentUserId` từ JWT và gọi `_productService.ChangeStatusAsync(id, request, currentUserId, ct)`.
3. **Bước 3:** Truy vấn entity `Product` kèm danh sách `Variants` và `Images`. Nếu không tìm thấy ném `EntityNotFoundException`.
4. **Bước 4:** Kiểm tra quyền sở hữu của người bán đối với sản phẩm.
5. **Bước 5 (Rào chắn Bất biến):** Nếu trạng thái mới là `Active`:
   - Kiểm tra `product.Variants.Any(v => v.IsActive && !v.IsDeleted)`. Nếu không có, ném `BusinessRuleValidationException("VAL_NO_ACTIVE_VARIANTS")`.
   - Kiểm tra `product.Images.Any(i => i.IsActive && !i.IsDeleted)`. Nếu không có, ném `BusinessRuleValidationException("VAL_NO_ACTIVE_IMAGES")`.
   - Đặt `product.PublishedAt = DateTimeOffset.UtcNow` (nếu chưa từng được publish).
6. **Bước 6:** Cập nhật trạng thái `Status`, ghi nhận `StatusReason`, lưu DB.
7. **Bước 7:** Trả về `ProductDto` với mã HTTP `200 OK`.

### 5.2 Checklist hoàn thành
- [x] **Pre-Code:** Rà soát điều kiện kích hoạt sản phẩm (variant tối thiểu và image tối thiểu).
- [x] **Application:** Triển khai logic trong `ProductService.cs`.
- [x] **Api:** `ProductsController.ChangeStatus` áp dụng `[HttpPatch("{id:guid}/status")]`.
- [x] **Verify:** `dotnet build Catalog.sln -c Release` thành công 100% với 0 lỗi.

