# CATALOG SERVICE - PUT /api/v1/variants/{id}

> **Feature / Use Case:** Cập nhật thông tin biến thể sản phẩm [CAT-08] (Hỗ trợ cả 2 phương thức HTTP: PUT và PATCH)
> **Trạng thái:** ✅ VERIFIED
> **Ngày tạo:** 2026-11-01
> **Tác giả:** Nexus Team & AI Agent

---

## 1. Thông tin Định tuyến & RBAC (Routing & Authorization)
- **HTTP Method:** `PUT` (Hỗ trợ alias: `PATCH`)
- **Route Endpoint:** `/api/v1/variants/{id}`
- **Quyền hạn (RBAC Policy):** `RequireRole("seller", "admin")`
- **Quy tắc Sở hữu (Resource Ownership):** Bắt buộc sản phẩm chứa biến thể này phải thuộc quyền sở hữu của người bán (`product.SellerId == currentUserId`). Admin có quyền can thiệp.
- **Controller:** `Catalog.Api/Controllers/Public/VariantsController.cs`
- **Application Contract:** `Catalog.Application/Services/Product/IProductService.cs`

---

## 2. Đầu vào & Quy chuẩn Validation (Input & Validation Schema)
### 2.1 Route Parameters & Query Parameters
| Tham số | Vị trí | Kiểu C# | Bắt buộc | Ràng buộc / Giới hạn | Ghi chú |
| :--- | :--- | :--- | :---: | :--- | :--- |
| `id` | Route | `Guid` | Có | UUIDv4 format | Khóa chính của biến thể cần cập nhật |

### 2.2 Request Body Record DTO
- **Vị trí file:** `Catalog.Application/Services/Product/ProductRequests.cs`
- **Tên C# Record:** `UpdateVariantRequest`
- **Khai báo C# Record:**
```csharp
public sealed record UpdateVariantRequest(
    string Name,
    decimal Price,
    string? Currency = "VND",
    List<CreateVariantAttributeRequest>? Attributes = null);
```
- **Bảng quy chuẩn Validation:**
| Tham số | Kiểu C# | Bắt buộc | Ràng buộc / Giới hạn | Ghi chú |
| :--- | :--- | :---: | :--- | :--- |
| `name` | `string` | Có | Độ dài: 2 - 200 ký tự | Tên phiên bản |
| `price` | `decimal` | Có | Min: 0, Max: 1,000,000,000 | Giá bán niêm yết mới |
| `currency` | `string?` | Không | Mặc định: `"VND"` | Tiền tệ |
| `attributes` | `List?` | Không | Tối đa 5 thuộc tính | Cặp thuộc tính mới thay thế danh sách cũ |

- **JSON Request Body mẫu (camelCase):**
```json
{
  "name": "Màu Trắng - Gateron Yellow Pro",
  "price": 1350000,
  "currency": "VND",
  "attributes": [
    { "attrName": "Color", "attrValue": "White" },
    { "attrName": "Switch", "attrValue": "Gateron Yellow Pro" }
  ]
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
  "name": "Màu Trắng - Gateron Yellow Pro",
  "price": 1350000,
  "currency": "VND",
  "isActive": true,
  "hasOrder": false,
  "status": "Active",
  "statusReason": null,
  "attributes": [
    { "attrName": "Color", "attrValue": "White" },
    { "attrName": "Switch", "attrValue": "Gateron Yellow Pro" }
  ]
}
```

---

## 4. Ma trận Mã lỗi RFC 9110 (Error Matrix)
| HTTP Status | Error Code | Message (Sentence case) | Điều kiện kích hoạt (Trigger Condition) |
| :---: | :--- | :--- | :--- |
| `400 Bad Request` | `VAL_INVALID_INPUT` | `Variant name is required.` | Tên biến thể để trống hoặc quá ngắn |
| `400 Bad Request` | `VAL_INVALID_PRICE` | `Variant price must be non-negative.` | Giá tiền âm |
| `401 Unauthorized` | `UNAUTHORIZED` | `Authentication is required to access this resource.` | Không có Bearer token hợp lệ |
| `403 Forbidden` | `FORBIDDEN` | `You do not have permission to modify this variant.` | Sản phẩm chứa biến thể không thuộc về `currentUserId` |
| `404 Not Found` | `VARIANT_NOT_FOUND` | `Variant not found with the specified identifier.` | Không tìm thấy biến thể hoặc đã bị xóa mềm |
| `500 Internal Error` | `INTERNAL_SERVER_ERROR` | `An unexpected error occurred while processing the request.` | Lỗi không mong muốn |

---

## 5. Kế hoạch Thực thi & Checklist Kiểm chứng (Pre-Code Plan & Checklist)
### 5.1 Các bước thực thi nghiệp vụ (Execution Flow Plan)
1. **Bước 1:** `VariantsController.UpdateVariant` nhận `id` và `UpdateVariantRequest`.
2. **Bước 2:** Lấy `currentUserId` từ JWT, gọi `_productService.UpdateVariantAsync(id, currentUserId, request, ct)`.
3. **Bước 3:** Nạp entity `ProductVariant` kèm `Product` cha và danh sách `Attributes`.
4. **Bước 4:** Kiểm tra quyền sở hữu (`variant.Product.SellerId != currentUserId` và không phải Admin thì ném Unauthorized).
5. **Bước 5:** Cập nhật `Name`, `Price`, `Currency`. Nếu có danh sách `Attributes` mới, thay thế danh sách cũ.
6. **Bước 6:** Lưu thay đổi vào DB và trả về `VariantDetailDto` với mã HTTP `200 OK`.

### 5.2 Checklist hoàn thành
- [x] **Pre-Code:** Hoàn tất tài liệu chuẩn hóa `PUT` và `PATCH`.
- [x] **Application:** Triển khai trong `ProductService.cs`.
- [x] **Api:** `VariantsController.UpdateVariant` hỗ trợ cả 2 HTTP verbs.
- [x] **Verify:** `dotnet build Catalog.sln -c Release` thành công 100% với 0 lỗi.

