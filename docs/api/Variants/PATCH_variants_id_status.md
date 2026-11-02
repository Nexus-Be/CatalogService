# CATALOG SERVICE - PATCH /api/v1/variants/{id}/status

> **Feature / Use Case:** Thay đổi trạng thái biến thể sản phẩm [CAT-09] (ACTIVE ⇄ INACTIVE kèm ghi nhận lý do ngưng bán)
> **Trạng thái:** ✅ VERIFIED
> **Ngày tạo:** 2026-11-01
> **Tác giả:** Nexus Team & AI Agent

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `PATCH`
- **Route Endpoint:** `/api/v1/variants/{id}/status`
- **Quyền hạn (RBAC Policy):** `RequireRole("seller", "admin")`
- **Quy tắc Sở hữu (Resource Ownership):** Bắt buộc sản phẩm chứa biến thể này phải thuộc quyền sở hữu của người bán (`product.SellerId == currentUserId`). Admin có quyền can thiệp.
- **Controller:** `Catalog.Api/Controllers/Public/VariantsController.cs`
- **Application Contract:** `Catalog.Application/Services/Product/IProductService.cs`

---

## 2. Đầu vào & Quy chuẩn Validation (Input & Validation Schema)
### 2.1 Route Parameters & Query Parameters
| Tham số | Vị trí | Kiểu C# | Bắt buộc | Ràng buộc / Giới hạn | Ghi chú |
| :--- | :--- | :--- | :---: | :--- | :--- |
| `id` | Route | `Guid` | Có | UUIDv4 format | Khóa chính của biến thể cần đổi trạng thái |

### 2.2 Request Body Record DTO
- **Vị trí file:** `Catalog.Application/Services/Product/ProductRequests.cs`
- **Tên C# Record:** `ChangeVariantStatusRequest`
- **Khai báo C# Record:**
```csharp
public sealed record ChangeVariantStatusRequest(
    string Status,
    string? StatusReason = null);
```
- **Bảng quy chuẩn Validation:**
| Tham số | Kiểu C# | Bắt buộc | Ràng buộc / Giới hạn | Ghi chú |
| :--- | :--- | :---: | :--- | :--- |
| `status` | `string` | Có | `Active`, `Inactive` | Trạng thái mới của biến thể |
| `statusReason` | `string?` | Không | Max: 500 ký tự | Lý do thay đổi trạng thái |

- **JSON Request Body mẫu (camelCase):**
```json
{
  "status": "Inactive",
  "statusReason": "Temporary out of stock from manufacturer."
}
```

---

## 3. Đầu ra & Hợp đồng DTO thuần (Response Contract - Pure DTO)
- **Vị trí file:** `Catalog.Application/Services/Product/ProductDtos.cs`
- **Tên C# Record:** `VariantDetailDto`
- **Mã HTTP thành công:** `200 OK`
- **JSON Response Body mẫu (camelCase):**
```json
{
  "id": "33333333-3333-3333-3333-333333333333",
  "productId": "77777777-7777-7777-7777-777777777777",
  "skuCode": "KB-NEXUS-01-WHITE",
  "name": "Màu Trắng - Gateron Yellow",
  "price": 1250000,
  "currency": "VND",
  "isActive": false,
  "hasOrder": false,
  "status": "Inactive",
  "statusReason": "Temporary out of stock from manufacturer.",
  "attributes": [
    { "attrName": "Color", "attrValue": "White" }
  ]
}
```

---

## 4. Ma trận Mã lỗi RFC 9110 (Error Matrix)
| HTTP Status | Error Code | Message (Sentence case) | Điều kiện kích hoạt (Trigger Condition) |
| :---: | :--- | :--- | :--- |
| `400 Bad Request` | `VAL_INVALID_INPUT` | `Invalid variant status. Allowed values: Active, Inactive.` | Trạng thái không đúng enum quy định |
| `401 Unauthorized` | `UNAUTHORIZED` | `Authentication is required to access this resource.` | Không có Bearer token hợp lệ |
| `403 Forbidden` | `FORBIDDEN` | `You do not have permission to modify this variant.` | Người bán không sở hữu sản phẩm chứa biến thể này |
| `404 Not Found` | `VARIANT_NOT_FOUND` | `Variant not found with the specified identifier.` | Không tìm thấy biến thể hoặc đã bị xóa mềm |
| `500 Internal Error` | `INTERNAL_SERVER_ERROR` | `An unexpected error occurred while processing the request.` | Lỗi không mong muốn trong quá trình thực thi |

### 4.1 Bảng Ánh xạ Enum liên quan
| Tên Enum C# | Giá trị DB (Integer) | Giá trị API (PascalCase) | Ý nghĩa nghiệp vụ |
| :--- | :---: | :--- | :--- |
| `VariantStatus.Active` | `0` | `"Active"` | Biến thể đang hoạt động |
| `VariantStatus.Inactive` | `1` | `"Inactive"` | Biến thể tạm ngưng |

---

## 5. Kế hoạch Thực thi & Checklist Kiểm chứng (Pre-Code Plan & Checklist)
### 5.1 Các bước thực thi nghiệp vụ (Execution Flow Plan)
1. **Bước 1:** `VariantsController.ChangeVariantStatus` nhận `id` và `ChangeVariantStatusRequest`.
2. **Bước 2:** Lấy `currentUserId`, gọi `_productService.ChangeVariantStatusAsync(id, currentUserId, request, ct)`.
3. **Bước 3:** Nạp entity `ProductVariant` kèm `Product` cha.
4. **Bước 4:** Kiểm tra quyền sở hữu của người bán đối với sản phẩm.
5. **Bước 5:** Phân tích chuỗi `Status` thành enum `VariantStatus`, gán cờ `IsActive = (newStatus == VariantStatus.Active)` và ghi nhận `StatusReason`.
6. **Bước 6:** Lưu vào cơ sở dữ liệu và trả về `VariantDetailDto` với mã HTTP `200 OK`.

### 5.2 Checklist hoàn thành
- [x] **Pre-Code:** Rà soát chuyển đổi trạng thái biến thể và bảng enum.
- [x] **Application:** Triển khai phương thức `ChangeVariantStatusAsync` trong `ProductService.cs`.
- [x] **Api:** `VariantsController.ChangeVariantStatus` áp dụng `[HttpPatch("{id:guid}/status")]`.
- [x] **Verify:** `dotnet build Catalog.sln -c Release` thành công 100% với 0 lỗi.

